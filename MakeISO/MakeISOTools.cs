using DragAndDrop;
using IMAPI2.Interop;
using IMAPI2.MediaItem;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Win32;

namespace MakeISO
{
    public class MakeISOTools : NotifyPropertyChanges, IFileDragDropTarget
    {
        private readonly CommonOpenFileDialog openFileDialog = new CommonOpenFileDialog
        {
            EnsureFileExists = true,
            EnsurePathExists = true,
            EnsureValidNames = true,
            Multiselect = true,
            CookieIdentifier = new Guid("250DD459-2378-4530-B2C2-EA42364EF080")
        };
        private readonly CommonOpenFileDialog openFolderDialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            EnsureFileExists = true,
            EnsurePathExists = true,
            EnsureValidNames = true,
            CookieIdentifier = new Guid("E4BC886C-1E2E-45A0-8386-334EE5A4EEE0")
        };
        private readonly CommonSaveFileDialog saveFileDialog = new CommonSaveFileDialog
        {
            EnsureValidNames = true,
            DefaultExtension = "iso",
            AlwaysAppendDefaultExtension = true,
            CookieIdentifier = new Guid("E3D939C0-68D8-4CBD-B863-ED09B59EFC13")
        };

        private readonly Subject<IMediaItem> fileListNotifier = new Subject<IMediaItem>();
        private readonly ObservableCollection<IMediaItem> fileList = new ObservableCollection<IMediaItem>();

        private CancellationTokenSource tokenSource;

        public ICollectionView FileList { get; }

        private WriterStatus writerStatus = WriterStatus.Idle;
        public WriterStatus WriterStatus
        {
            get => writerStatus;
            set => SetProperty(ref writerStatus, value);
        }

        private long totalBytesWritten;
        public long TotalBytesWritten
        {
            get => totalBytesWritten;
            set => SetProperty(ref totalBytesWritten, value, nameof(TotalBytesWritten), nameof(FriendlyTotalBytesWritten));
        }

        public string FriendlyTotalBytesWritten => Utilities.GetBytesReadable(TotalBytesWritten);

        private long totalBytesToWrite;
        public long TotalBytesToWrite
        {
            get => totalBytesToWrite;
            set => SetProperty(ref totalBytesToWrite, value, nameof(TotalBytesToWrite), nameof(FriendlyTotalBytesToWrite));
        }

        public string FriendlyTotalBytesToWrite => Utilities.GetBytesReadable(TotalBytesToWrite);

        private long totalSpaceRequired;
        public long TotalSpaceRequired
        {
            get => totalSpaceRequired;
            set => SetProperty(ref totalSpaceRequired, value, nameof(TotalSpaceRequired), nameof(FriendlyTotalSpaceRequired));
        }

        public string FriendlyTotalSpaceRequired => Utilities.GetBytesReadable(TotalSpaceRequired);

        private bool addingFiles;
        public bool AddingFiles
        {
            get => addingFiles;
            set
            {
                SetProperty(ref addingFiles, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool writingIso;
        public bool WritingIso
        {
            get => writingIso;
            set
            {
                SetProperty(ref writingIso, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private long fileCount;
        public long FileCount
        {
            get => fileCount;
            set => SetProperty(ref fileCount, value);
        }

        private long filesStaged;
        public long FilesStaged
        {
            get => filesStaged;
            set => SetProperty(ref filesStaged, value);
        }

        private string volumeName;
        public string VolumeName
        {
            get => volumeName;
            set => SetProperty(ref volumeName, value);
        }

        private string biosBootFile;
        public string BiosBootFile
        {
            get => biosBootFile;
            set => SetProperty(ref biosBootFile, value);
        }

        private string uefiBootFile;
        public string UefiBootFile
        {
            get => uefiBootFile;
            set => SetProperty(ref uefiBootFile, value);
        }

        public MakeISOTools()
        {
            BiosBootFile = "None";
            UefiBootFile = "None";

            FileList = (new CollectionViewSource { Source = fileList }).View;
            FileList.SortDescriptions.Add(new SortDescription("Type", ListSortDirection.Ascending));
            FileList.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));

            saveFileDialog.Filters.Add(new CommonFileDialogFilter("Disk Image", "iso"));

            fileListNotifier.ObserveOnDispatcher().Subscribe(item =>
            {
                fileList.Add(item);
                FileCount += item.FileCount;
                TotalSpaceRequired += item.SizeOnDisc;
            });
        }

        private bool addFolder(string path)
        {
            if (isDuplicate(path, true))
            {
                return false;
            }

            var item = new DirectoryItem(path);
            fileListNotifier.OnNext(item);
            return true;
        }

        private bool addFile(string path)
        {
            if (isDuplicate(path, false))
            {
                return false;
            }

            var item = new FileItem(path);
            fileListNotifier.OnNext(item);
            return true;
        }

        private List<string> addFiles(IEnumerable<string> paths)
        {
            var duplicates = new List<string>();

            foreach (var path in paths)
            {
                if (!addFile(path))
                {
                    duplicates.Add(path);
                }
            }

            return duplicates;
        }

        private void writeIso(string path, CancellationToken cancellationToken)
        {
            FilesStaged = 0;
            TotalBytesWritten = 0;
            WriterStatus = WriterStatus.Staging;

            var iso = new MsftFileSystemImage();
            iso.Update += isoUpdate;
            iso.ChooseImageDefaultsForMediaType(IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DISK);
            iso.FileSystemsToCreate = FsiFileSystems.FsiFileSystemUDF;
            iso.UDFRevision = 0x250;
            if (!string.IsNullOrWhiteSpace(VolumeName))
            {
                iso.VolumeName = VolumeName;
            }

            var bootImageOptions = new List<object>();

            if (File.Exists(BiosBootFile))
            {
                var biosBootOptions = new BootOptions
                {
                    Manufacturer = "Herohtar",
                    PlatformId = PlatformId.PlatformX86,
                    Emulation = EmulationType.EmulationNone
                };

                var biosBootFile = new ManagedIStream(File.OpenRead(BiosBootFile));
                biosBootOptions.AssignBootImage(biosBootFile);
                bootImageOptions.Add(biosBootOptions);
            }

            if (File.Exists(UefiBootFile))
            {
                var uefiBootOptions = new BootOptions
                {
                    Manufacturer = "Herohtar",
                    PlatformId = PlatformId.PlatformEFI,
                    Emulation = EmulationType.EmulationNone
                };

                var uefiBootFile = new ManagedIStream(File.OpenRead(UefiBootFile));
                uefiBootOptions.AssignBootImage(uefiBootFile);
                bootImageOptions.Add(uefiBootOptions);
            }

            if (bootImageOptions.Count > 0)
            {
                iso.UDFRevision = 0x150; // Boot images don't work with later revisions
                ((IFileSystemImage2)iso).BootImageOptionsArray = bootImageOptions.ToArray();
            }

            foreach (var item in fileList)
            {
                item.AddToFileSystem(iso.Root, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var resultImage = iso.CreateResultImage();
            var imageStream = resultImage.ImageStream;

            imageStream.Stat(out var stat, 0x01);
            TotalBytesToWrite = stat.cbSize;
            WriterStatus = WriterStatus.Writing;

            var bytesReadPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(long)));
            var bytesRead = 0L;
            Marshal.WriteInt64(bytesReadPtr, bytesRead);

            try
            {
                using (var outStream = File.Create(path))
                {
                    var buffer = new byte[1024 * 1024];

                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        imageStream.Read(buffer, buffer.Length, bytesReadPtr);
                        bytesRead = Marshal.ReadInt64(bytesReadPtr);
                        TotalBytesWritten += bytesRead;
                        outStream.Write(buffer, 0, (int)bytesRead);
                    } while (bytesRead > 0);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bytesReadPtr);

                // For any file >= 128 kB, the streams are not automatically cleaned up, meaning their handles will remain open indefinitely
                // This results in all files that were added to the ISO being locked if you try to modify them while the program is still running
                disposeStreams(iso.Root);
            }
        }

        private void disposeStreams(IFsiDirectoryItem directory)
        {
            // Go through all the file items and dispose their streams
            var itemEnumerator = directory.GetEnumerator();
            while (itemEnumerator.MoveNext())
            {
                var currentItem = itemEnumerator.Current;
                if (currentItem is IFsiFileItem fileItem)
                {
                    // All files are added using ManagedIStream; however, any file under 128 kB is treated differently by the file system image
                    // The small files are changed to be added using some COM implementation of IStream which gets cleaned up automatically
                    if (fileItem.Data is ManagedIStream dataStream)
                    {
                        dataStream.Dispose();
                    }
                }
                else if (currentItem is IFsiDirectoryItem directoryItem)
                {
                    disposeStreams(directoryItem);
                }
            }
        }

        private bool isDuplicate(string path, bool isFolder)
        {
            var name = Path.GetFileName(path);
            if (isFolder)
            {
                return fileList.Where(item => item is DirectoryItem).Any(item => item.DisplayName.Equals(name));
            }
            else
            {
                return fileList.Where(item => item is FileItem).Any(item => item.DisplayName.Equals(name));
            }
        }

        public ICommand AddFolderCommand => new Command
        {
            ExecuteDelegate = async p =>
            {
                if (openFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    AddingFiles = true;

                    if (!await Task.Run(() => addFolder(openFolderDialog.FileName)))
                    {
                        MessageBox.Show("A folder with this name already exists.", "Duplicate folder", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    AddingFiles = false;
                }
            },
            CanExecuteDelegate = p => !(AddingFiles || WritingIso)
        };

        public ICommand AddFileCommand => new Command
        {
            ExecuteDelegate = async p =>
            {
                if (openFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    AddingFiles = true;

                    var duplicates = await Task.Run(() => addFiles(openFileDialog.FileNames));

                    if (duplicates.Count > 0)
                    {
                        MessageBox.Show($"{duplicates.Count} duplicate file(s) were skipped.", "Duplicate files", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    AddingFiles = false;
                }
            },
            CanExecuteDelegate = p => !(AddingFiles || WritingIso)
        };

        public ICommand RemoveItemCommand => new Command
        {
            ExecuteDelegate = p =>
            {
                if (p is IEnumerable itemsToRemove)
                {
                    foreach (var item in itemsToRemove.Cast<IMediaItem>().ToArray())
                    {
                        FileCount -= item.FileCount;
                        TotalSpaceRequired -= item.SizeOnDisc;
                        fileList.Remove(item);
                    }
                }
            },
            CanExecuteDelegate = p =>
            {
                var hasItemsToRemove = (p is IList itemsToRemove) ? (itemsToRemove.Count > 0) : false;

                return !(AddingFiles || WritingIso) && hasItemsToRemove;
            }
        };

        public ICommand AddBootImagesCommand => new Command
        {
            ExecuteDelegate = p =>
            {
                if (openFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var folder = openFolderDialog.FileName;

                    var biosFile = Path.Combine(folder, "etfsboot.com");
                    BiosBootFile = File.Exists(biosFile) ? biosFile : "None";

                    var uefiFile = Path.Combine(folder, "efisys.bin");
                    UefiBootFile = File.Exists(uefiFile) ? uefiFile : "None";

                    if (BiosBootFile.Equals("None") && UefiBootFile.Equals("None"))
                    {
                        MessageBox.Show($"No boot images were found in the selected folder:\n\n{folder}", "No images found", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (BiosBootFile.Equals("None"))
                    {
                        MessageBox.Show($"No BIOS boot image was found in the selected folder:\n\n{folder}\n\nOnly UEFI boot will be available.", "No BIOS Boot", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (UefiBootFile.Equals("None"))
                    {
                        MessageBox.Show($"No UEFI boot image was found in the selected folder:\n\n{folder}\n\nOnly BIOS boot will be available.", "No UEFI Boot", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            },
            CanExecuteDelegate = p => !(AddingFiles || WritingIso)
        };

        public ICommand WriteIsoCommand => new Command
        {
            ExecuteDelegate = async p =>
            {
                if (saveFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    WritingIso = true;
                    tokenSource = new CancellationTokenSource();

                    try
                    {
                        await Task.Run(() => writeIso(saveFileDialog.FileName, tokenSource.Token));
                    }
                    catch (OperationCanceledException)
                    {
                        MessageBox.Show(Application.Current.MainWindow, "Write canceled!", "Canceled", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    WriterStatus = WriterStatus.Idle;
                    WritingIso = false;
                }
            },
            CanExecuteDelegate = p => !(AddingFiles || WritingIso) && !FileList.IsEmpty
        };

        public ICommand CancelWriteCommand => new Command
        {
            ExecuteDelegate = p =>
            {
                tokenSource.Cancel();
            },
            CanExecuteDelegate = p => WritingIso
        };

        private void isoUpdate(object sender, string currentFile, int copiedSectors, int totalSectors)
        {
            if (copiedSectors == totalSectors)
            {
                FilesStaged++;
            }
        }

        public DragDropEffects GetFileDragDropEffects(string[] paths) => DragDropEffects.Copy;

        // async void because this is a non-awaitable event handler
        public async void OnFileDrop(string[] paths)
        {
            AddingFiles = true;

            var duplicates = 0;

            foreach (var path in paths)
            {
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    if (!await Task.Run(() => addFolder(path)))
                    {
                        duplicates++;
                    }
                }
                else
                {
                    if (!await Task.Run(() => addFile(path)))
                    {
                        duplicates++;
                    }
                }
            }

            if (duplicates > 0)
            {
                MessageBox.Show($"{duplicates} duplicate item(s) were skipped.", "Duplicate items", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            AddingFiles = false;
        }
    }
}
