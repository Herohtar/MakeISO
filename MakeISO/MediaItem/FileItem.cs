// Modified version of FileItem from Eric Haddan (https://www.codeproject.com/Articles/24544/Burning-and-Erasing-CD-DVD-Blu-ray-Media-with-C-an)

using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IMAPI2.Interop;
using MakeISO;
using Win32;

namespace IMAPI2.MediaItem
{
    internal class FileItem : IMediaItem
    {
        private const long SECTOR_SIZE = 2048;

        private readonly long fileLength = 0;

        public FileItem(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The file added to FileItem was not found!", path);
            }

            Path = path;

            var fileInfo = new FileInfo(Path);
            DisplayName = fileInfo.Name;
            fileLength = fileInfo.Length;
        }

        public long SizeOnDisc => (fileLength > 0) ? ((fileLength / SECTOR_SIZE) + 1) * SECTOR_SIZE : 0;

        public string FriendlySize => Utilities.GetBytesReadable(SizeOnDisc);

        public long FileCount => 1;

        public string Type => "File";

        public string Path { get; }

        public ImageSource FileIconImage
        {
            get
            {
                if (fileIconImage == null)
                {
                    // Get the File icon
                    var shinfo = new ShellFileInfo();
                    PInvoke.GetFileInfo(Path, 0, ref shinfo, GetFileInfoFlags.Icon | GetFileInfoFlags.SmallIcon);

                    if (shinfo.hIcon != IntPtr.Zero)
                    {
                        //The icon handle is returned in the hIcon member of the shinfo struct
                        fileIconImage = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                        PInvoke.DestroyIcon(shinfo.hIcon);
                    }
                }

                return fileIconImage;
            }
        }
        private ImageSource fileIconImage = null;

        public string DisplayName { get; }

        public bool AddToFileSystem(IFsiDirectoryItem rootItem, CancellationToken cancellationToken, string basePath = "")
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                var stream = new ManagedIStream(File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read));
                rootItem.AddFile(System.IO.Path.Combine(basePath, DisplayName), stream);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
