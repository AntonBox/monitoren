using System.Runtime.InteropServices;

namespace MonitorBrightness.Native;

// Test-only replacement for the P/Invoke class. Production acquisition, identity,
// read, and release code runs against these methods without loading native DLLs.
internal static class PhysicalMonitorNative
{
    internal const uint BrightnessCapability = 2;
    internal static nint ReturnedHandle;
    internal static bool AcquireSucceeds;
    internal static bool FailBrightnessRead;
    internal static bool FailCapabilities;
    internal static bool Acquired;
    internal static uint Current;
    internal static readonly List<nint> ReadHandles = new();
    internal static readonly List<nint> ReleasedHandles = new();
    internal static readonly List<(nint Handle, uint Value)> Writes = new();

    internal static void Reset()
    {
        ReturnedHandle = 0;
        AcquireSucceeds = true;
        FailBrightnessRead = false;
        FailCapabilities = false;
        Acquired = false;
        Current = 45;
        ReadHandles.Clear();
        ReleasedHandles.Clear();
        Writes.Clear();
    }

    internal struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal NativeRect Monitor;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayDevice
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string Description;
        internal uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceId;
    }

    internal struct PhysicalMonitor
    {
        internal nint Handle;
        internal string Description;
    }

    internal delegate bool MonitorEnumCallback(nint monitor, nint hdc, nint rect, nint data);

    internal static bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumCallback callback, nint data)
        => callback(123, hdc, clip, data);

    internal static bool GetMonitorInfoW(nint monitor, ref MonitorInfo info)
    {
        info.DeviceName = @"\\.\DISPLAY4";
        info.Monitor = new NativeRect { Left = 0, Top = 0, Right = 2560, Bottom = 1440 };
        info.Flags = 1;
        return true;
    }

    internal static bool EnumDisplayDevicesW(string deviceName, uint index, ref DisplayDevice device, uint flags)
    {
        if (index != 0) return false;
        device.DeviceName = @"\\.\DISPLAY4\Monitor0";
        device.Description = "Fake Generic PnP Monitor";
        device.DeviceId = "FAKE-DEVICE-IDENTITY";
        device.StateFlags = 3;
        return true;
    }

    internal static bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count)
    {
        count = 1;
        return true;
    }

    internal static bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, PhysicalMonitor[] monitors)
    {
        monitors[0] = new PhysicalMonitor { Handle = ReturnedHandle, Description = "Fake Generic PnP Monitor" };
        Acquired = AcquireSucceeds;
        if (!AcquireSucceeds) Marshal.SetLastPInvokeError(31);
        return AcquireSucceeds;
    }

    internal static bool GetMonitorCapabilities(nint monitor, out uint capabilities, out uint temperatures)
    {
        capabilities = BrightnessCapability;
        temperatures = 0;
        if (FailCapabilities) Marshal.SetLastPInvokeError(unchecked((int)0xC0262587));
        return !FailCapabilities;
    }

    internal static bool GetMonitorBrightness(nint monitor, out uint minimum, out uint current, out uint maximum)
    {
        ReadHandles.Add(monitor);
        minimum = 0;
        current = Current;
        maximum = 100;
        if (FailBrightnessRead) Marshal.SetLastPInvokeError(31);
        return !FailBrightnessRead;
    }

    internal static bool SetMonitorBrightness(nint monitor, uint value)
    {
        Writes.Add((monitor, value));
        Current = value;
        return true;
    }

    internal static bool DestroyPhysicalMonitor(nint monitor)
    {
        ReleasedHandles.Add(monitor);
        return true;
    }
}
