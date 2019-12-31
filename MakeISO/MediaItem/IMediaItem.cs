// Modified version of IMediaItem from Eric Haddan (https://www.codeproject.com/Articles/24544/Burning-and-Erasing-CD-DVD-Blu-ray-Media-with-C-an)

using System.Threading;
using System.Windows.Media;
using IMAPI2.Interop;

namespace IMAPI2.MediaItem
{
    public interface IMediaItem
    {
        /// <summary>
        /// Returns the full path of the file or directory
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Returns the name of the file or directory
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Returns the size of the file or directory to the next largest sector
        /// </summary>
        long SizeOnDisc { get; }

        /// <summary>
        /// Returns the number of files represented by this item
        /// </summary>
        long FileCount { get; }

        /// <summary>
        /// Returns the Icon of the file or directory
        /// </summary>
        ImageSource FileIconImage { get; }

        /// <summary>
        /// Returns the type of this item
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Adds the file or directory to the directory item, usually the root.
        /// </summary>
        /// <param name="rootItem"></param>
        /// <returns></returns>
        bool AddToFileSystem(IFsiDirectoryItem rootItem, CancellationToken cancellationToken, string basePath = "");
    }
}
