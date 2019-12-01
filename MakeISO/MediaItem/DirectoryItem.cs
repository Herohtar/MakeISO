// Modified version of DirectoryItem from Eric Haddan (https://www.codeproject.com/Articles/24544/Burning-and-Erasing-CD-DVD-Blu-ray-Media-with-C-an)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IMAPI2.Interop;

namespace IMAPI2.MediaItem
{
    internal class DirectoryItem : IMediaItem
    {
        private readonly List<IMediaItem> mediaItems = new List<IMediaItem>();

        public DirectoryItem(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new FileNotFoundException("The directory added to DirectoryItem was not found!", directoryPath);
            }

            Path = directoryPath;
            var fileInfo = new FileInfo(Path);
            DisplayName = fileInfo.Name;

            // Get all the files in the directory
            var files = Directory.GetFiles(Path);
            foreach (var file in files)
            {
                mediaItems.Add(new FileItem(file));
            }

            // Get all the subdirectories
            var directories = Directory.GetDirectories(Path);
            foreach (var directory in directories)
            {
                mediaItems.Add(new DirectoryItem(directory));
            }
        }

        public string Type => "Directory";

        public string Path { get; }

        public string DisplayName { get; }

        public long SizeOnDisc => mediaItems.Sum(item => item.SizeOnDisc);

        public long FileCount => mediaItems.Sum(item => item.FileCount);

        public ImageSource FileIconImage
        {
            get
            {
                if (fileIconImage == null)
                {
                    // Get the Directory icon
                    var shinfo = new SHFILEINFO();
                    Win32.SHGetFileInfo(Path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), Win32.SHGFI_ICON | Win32.SHGFI_SMALLICON);

                    if (shinfo.hIcon != IntPtr.Zero)
                    {
                        //The icon is returned in the hIcon member of the shinfo struct
                        fileIconImage = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                        Win32.DestroyIcon(shinfo.hIcon);
                    }
                }

                return fileIconImage;
            }
        }
        private ImageSource fileIconImage = null;

        public bool AddToFileSystem(IFsiDirectoryItem rootItem)
        {
            try
            {
                rootItem.AddTree(Path, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
