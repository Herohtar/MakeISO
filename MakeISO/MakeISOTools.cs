using DragAndDrop;
using IMAPI2.Interop;
using IMAPI2.MediaItem;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveComponentModel;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

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

        private readonly Subject<IMediaItem> addFile = new Subject<IMediaItem>();
        private readonly ObservableCollection<IMediaItem> fileList = new ObservableCollection<IMediaItem>();
        public ICollectionView FileList { get; }

        private long totalBytesWritten;
        public long TotalBytesWritten
        {
            get => totalBytesWritten;
            set => SetProperty(ref totalBytesWritten, value);
        }

        private long totalBytesToWrite;
        public long TotalBytesToWrite
        {
            get => totalBytesToWrite;
            set => SetProperty(ref totalBytesToWrite, value);
        }

        private long totalSpaceRequired;
        public long TotalSpaceRequired
        {
            get => totalSpaceRequired;
            set => SetProperty(ref totalSpaceRequired, value);
        }

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
            FileList = (new CollectionViewSource { Source = fileList }).View;
            FileList.SortDescriptions.Add(new SortDescription("Type", ListSortDirection.Ascending));
            FileList.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));

            saveFileDialog.Filters.Add(new CommonFileDialogFilter("Disk Image", "iso"));

            addFile.ObserveOnDispatcher().Subscribe(item =>
            {
                fileList.Add(item);
                FileCount += item.FileCount;
                TotalSpaceRequired += item.SizeOnDisc;
            });
        }

        public bool AddFolder(string path)
        {
            if (isDuplicate(path, true))
            {
                return false;
            }

            var item = new DirectoryItem(path);
            addFile.OnNext(item);
            return true;
        }

        public bool AddFile(string path)
        {
            if (isDuplicate(path, false))
            {
                return false;
            }

            var item = new FileItem(path);
            addFile.OnNext(item);
            return true;
        }

        public void WriteIso(string path)
        {
            FilesStaged = 0;
            TotalBytesWritten = 0;

            var iso = new MsftFileSystemImage();
            iso.Update += Iso_Update;
            iso.ChooseImageDefaultsForMediaType(IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DISK);
            iso.FileSystemsToCreate = FsiFileSystems.FsiFileSystemUDF;
            iso.UDFRevision = 0x250;
            if (!string.IsNullOrWhiteSpace(VolumeName))
            {
                iso.VolumeName = VolumeName;
            }

            /*
            // Make bootable ISO (WinPE)
            var biosBootOptions = new BootOptions
            {
                Manufacturer = "Herohtar",
                PlatformId = PlatformId.PlatformX86,
                Emulation = EmulationType.EmulationNone
            };

            var biosBootFile = new ManagedIStream(File.OpenRead(@"D:\WinPE\fwfiles\etfsboot.com"));
            biosBootOptions.AssignBootImage(biosBootFile);

            var uefiBootOptions = new BootOptions
            {
                Manufacturer = "Herohtar",
                PlatformId = PlatformId.PlatformEFI,
                Emulation = EmulationType.EmulationNone
            };

            var uefiBootFile = new ManagedIStream(File.OpenRead(@"D:\WinPE\fwfiles\efisys.bin"));
            uefiBootOptions.AssignBootImage(uefiBootFile);

            ((IFileSystemImage2)iso).BootImageOptionsArray = new object[] { biosBootOptions, uefiBootOptions };
            */

            foreach (var item in fileList)
            {
                item.AddToFileSystem(iso.Root);
            }

            var resultImage = iso.CreateResultImage();
            var imageStream = resultImage.ImageStream;

            imageStream.Stat(out var stat, 0x01);
            TotalBytesToWrite = stat.cbSize;

            var bytesReadPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(long)));
            var bytesRead = 0L;
            Marshal.WriteInt64(bytesReadPtr, bytesRead);

            using (var outStream = File.Create(path))
            {
                var buffer = new byte[1024 * 1024];

                do
                {
                    imageStream.Read(buffer, buffer.Length, bytesReadPtr);
                    bytesRead = Marshal.ReadInt64(bytesReadPtr);
                    TotalBytesWritten += bytesRead;
                    outStream.Write(buffer, 0, (int)bytesRead);
                } while (bytesRead > 0);
            }

            Marshal.FreeHGlobal(bytesReadPtr);
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

                    if (!await Task.Run(() => AddFolder(openFolderDialog.FileName)))
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

                    var duplicates = 0;
                    await Task.Run(() =>
                    {
                        foreach (var fileName in openFileDialog.FileNames)
                        {
                            if (!AddFile(fileName))
                            {
                                duplicates++;
                            }
                        }
                    });

                    if (duplicates > 0)
                    {
                        MessageBox.Show($"{duplicates} duplicate file(s) were not added.", "Duplicate files", MessageBoxButton.OK, MessageBoxImage.Information);
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

        public ICommand WriteIsoCommand => new Command
        {
            ExecuteDelegate = async p =>
            {
                if (saveFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    WritingIso = true;
                    await Task.Run(() => WriteIso(saveFileDialog.FileName));
                    WritingIso = false;
                }
            },
            CanExecuteDelegate = p => !(AddingFiles || WritingIso) && !FileList.IsEmpty
        };

        private void Iso_Update(object sender, string currentFile, int copiedSectors, int totalSectors)
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
                    if (!await Task.Run(() => AddFolder(path)))
                    {
                        duplicates++;
                    }
                }
                else
                {
                    if (!await Task.Run(() => AddFile(path)))
                    {
                        duplicates++;
                    }
                }
            }

            if (duplicates > 0)
            {
                MessageBox.Show($"{duplicates} duplicate item(s) were not added.", "Duplicate items", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            AddingFiles = false;
        }
    }
}
