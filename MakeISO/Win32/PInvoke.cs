using System;
using System.Runtime.InteropServices;

namespace Win32
{

    internal static class PInvoke
    {

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref ShellFileInfo psfi, uint cbSizeFileInfo, GetFileInfoFlags uFlags);

        public static IntPtr GetFileInfo(string filePath, uint fileAttributes, ref ShellFileInfo fileInfo, GetFileInfoFlags flags)
        {
            return SHGetFileInfo(filePath, fileAttributes, ref fileInfo, (uint)Marshal.SizeOf(fileInfo), flags);
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetStockIconInfo(StockIconId siid, GetStockIconInfoFlags uFlags, [In, Out] StockIconInfo psii);

        public static int GetStockIconInfo(StockIconId stockIconId, GetStockIconInfoFlags flags, ref StockIconInfo stockIconInfo)
        {
            return SHGetStockIconInfo(stockIconId, flags, stockIconInfo);
        }

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}
