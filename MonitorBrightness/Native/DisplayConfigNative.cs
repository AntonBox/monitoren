using System.Runtime.InteropServices;

namespace MonitorBrightness.Native;

// Read structures/signatures: Microsoft wingdi.h and Microsoft Learn.
// The SDR setter packet (type -18, flag 1) is UNDOCUMENTED, as used by Monitorian.
// User approved this limited M3 exception. See docs/M3-HDR-test.md for references.
internal static class DisplayConfigNative
{
    internal const uint ActivePaths = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        internal uint Low;
        internal int High;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Header
    {
        internal uint Type;
        internal uint Size;
        internal Luid Adapter;
        internal uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathSource
    {
        internal Luid Adapter;
        internal uint Id;
        internal uint ModeIndex;
        internal uint Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathTarget
    {
        internal Luid Adapter;
        internal uint Id;
        internal uint ModeIndex;
        internal uint OutputTechnology;
        internal uint Rotation;
        internal uint Scaling;
        internal uint RefreshNumerator;
        internal uint RefreshDenominator;
        internal uint ScanLineOrdering;
        internal int Available; // Win32 BOOL, four bytes.
        internal uint Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayPath
    {
        internal PathSource Source;
        internal PathTarget Target;
        internal uint Flags;
    }

    // DISPLAYCONFIG_MODE_INFO is a 16-byte header followed by a 48-byte union.
    // QueryDisplayConfig requires this output buffer; M3 does not interpret it.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ModeInfo
    {
        [FieldOffset(16)] internal ulong UnionAlignment;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SourceName
    {
        internal Header Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct TargetName
    {
        internal Header Header;
        internal uint Flags;
        internal uint OutputTechnology;
        internal ushort Manufacturer;
        internal ushort Product;
        internal uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdvancedColorInfo
    {
        internal Header Header;
        internal uint Flags;
        internal uint Encoding;
        internal uint BitsPerChannel;
        internal uint ActiveColorMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WhiteLevel
    {
        internal Header Header;
        internal uint Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SetWhiteLevel
    {
        internal Header Header;
        internal uint Value;
        internal byte Flag;
    }

    // These functions return Win32 error codes directly; do not use GetLastError.
    [DllImport("User32.dll", ExactSpelling = true)]
    internal static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);

    [DllImport("User32.dll", ExactSpelling = true)]
    internal static extern int QueryDisplayConfig(uint flags, ref uint paths,
        [Out] DisplayPath[] pathBuffer, ref uint modes, [Out] ModeInfo[] modeBuffer, nint topology);

    [DllImport("User32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int GetSourceName(ref SourceName packet);

    [DllImport("User32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int GetTargetName(ref TargetName packet);

    [DllImport("User32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    internal static extern int GetAdvancedColorInfo(ref AdvancedColorInfo packet);

    [DllImport("User32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    internal static extern int GetWhiteLevel(ref WhiteLevel packet);

    [DllImport("User32.dll", EntryPoint = "DisplayConfigSetDeviceInfo", ExactSpelling = true)]
    internal static extern int SetSdrWhiteLevel(ref SetWhiteLevel packet);
}
