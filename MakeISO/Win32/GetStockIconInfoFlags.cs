using System;

namespace Win32
{
    [Flags]
    public enum GetStockIconInfoFlags : uint
    {
        IconLocation = 0x0,    // you always get the icon location
        Icon = 0x100,          // get icon
        SysIconIndex = 0x4000, // get system icon index
        LinkOverlay = 0x8000,  // put a link overlay on icon
        Selected = 0x10000,    // show icon in selected state
        LargeIcon = 0x0,       // get large icon
        SmallIcon = 0x1,       // get small icon
        ShellIconSize = 0x4    // get shell size icon
    }
}
