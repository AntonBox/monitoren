using System.ComponentModel;
using System.Runtime.InteropServices;
using MonitorBrightness.Native;

namespace MonitorBrightness.Services;

// M3 deliberately supports only the user's confirmed single-active-display setup.
// Retain the acquired handle through Restore; never retarget by an array index.
internal sealed class PhysicalTestMonitor : ITestMonitor
{
    private sealed record Identity(nint LogicalHandle, string LogicalName, string DevicePath,
        string DeviceName, int Left, int Top, int Right, int Bottom);

    private readonly Identity identity;
    private readonly Func<bool> topologyChanged;
    private readonly nint handle;
    private bool disposed;
    public string Description { get; }

    private PhysicalTestMonitor(Identity identity, PhysicalMonitorNative.PhysicalMonitor physical, Func<bool> topologyChanged)
    {
        this.identity = identity;
        this.topologyChanged = topologyChanged;
        handle = physical.Handle;
        Description = $"{identity.LogicalName} -> {physical.Description}; {identity.Right - identity.Left} x {identity.Bottom - identity.Top}.\r\n"
            + "AOC Q27G42XE / HDMI is YOUR identification, not a model detected by this API.";
    }

    internal static PhysicalTestMonitor Open(Func<bool> topologyChanged)
    {
        if (topologyChanged())
        {
            throw new InvalidOperationException("Display setup changed. Repeat read-only diagnostics and confirmation.");
        }

        var identity = ReadIdentity();
        var monitors = new PhysicalMonitorNative.PhysicalMonitor[1];
        var acquired = false;
        var transferred = false;
        try
        {
            acquired = PhysicalMonitorNative.GetPhysicalMonitorsFromHMONITOR(identity.LogicalHandle, 1, monitors);
            if (!acquired)
            {
                throw NativeFailure("GetPhysicalMonitorsFromHMONITOR");
            }

            if (topologyChanged() || identity != ReadIdentity())
            {
                throw new InvalidOperationException("Display setup changed during acquisition. No write allowed.");
            }

            var result = new PhysicalTestMonitor(identity, monitors[0], topologyChanged);
            transferred = true;
            return result;
        }
        finally
        {
            // A successfully acquired physical handle can be zero. On failure,
            // do not confuse an untouched zero-initialized output slot with ownership.
            if (!transferred && (acquired || monitors[0].Handle != 0))
            {
                Release(monitors[0].Handle);
            }
        }
    }

    public void ValidateIdentity()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (topologyChanged() || identity != ReadIdentity())
        {
            throw new InvalidOperationException("Display identity/configuration changed. Test and Restore are blocked; no retargeting. Use the AOC's own menu to restore the saved original if needed.");
        }
    }

    public BrightnessReading Read()
    {
        ValidateIdentity();
        if (!PhysicalMonitorNative.GetMonitorBrightness(handle, out var minimum, out var current, out var maximum))
        {
            throw NativeFailure("GetMonitorBrightness");
        }

        return new BrightnessReading(minimum, current, maximum);
    }

    public void Write(uint value)
    {
        ValidateIdentity();
        if (!PhysicalMonitorNative.SetMonitorBrightness(handle, value))
        {
            throw NativeFailure("SetMonitorBrightness");
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            Release(handle);
        }
    }

    private static Identity ReadIdentity()
    {
        nint logicalHandle = 0;
        var count = 0;
        PhysicalMonitorNative.MonitorEnumCallback callback = (monitor, _, _, _) =>
        {
            logicalHandle = monitor;
            count++;
            return true;
        };
        var enumerated = PhysicalMonitorNative.EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        if (!enumerated || count != 1)
        {
            throw new InvalidOperationException("M3 requires exactly one active display, explicitly confirmed as the external AOC. No write allowed.");
        }

        var info = new PhysicalMonitorNative.MonitorInfo { Size = (uint)Marshal.SizeOf<PhysicalMonitorNative.MonitorInfo>() };
        if (!PhysicalMonitorNative.GetMonitorInfoW(logicalHandle, ref info))
        {
            throw new InvalidOperationException("GetMonitorInfoW failed; identity unavailable. No documented extended error.");
        }

        var device = new PhysicalMonitorNative.DisplayDevice { Size = (uint)Marshal.SizeOf<PhysicalMonitorNative.DisplayDevice>() };
        // EDD_GET_DEVICE_INTERFACE_NAME: a private identity token, kept only in memory, never logged.
        if (!PhysicalMonitorNative.EnumDisplayDevicesW(info.DeviceName, 0, ref device, 1)
            || (device.StateFlags & 1) == 0 || string.IsNullOrWhiteSpace(device.DeviceId))
        {
            throw new InvalidOperationException("Cannot establish an active monitor device identity. No write allowed.");
        }

        var extra = new PhysicalMonitorNative.DisplayDevice { Size = (uint)Marshal.SizeOf<PhysicalMonitorNative.DisplayDevice>() };
        if (PhysicalMonitorNative.EnumDisplayDevicesW(info.DeviceName, 1, ref extra, 1))
        {
            throw new InvalidOperationException("Multiple Windows monitor entries make this mapping ambiguous. No write allowed.");
        }

        if (!PhysicalMonitorNative.GetNumberOfPhysicalMonitorsFromHMONITOR(logicalHandle, out var physicalCount))
        {
            throw NativeFailure("GetNumberOfPhysicalMonitorsFromHMONITOR");
        }

        if (physicalCount != 1)
        {
            throw new InvalidOperationException("Expected exactly one physical monitor. No write allowed.");
        }

        var bounds = info.Monitor;
        return new Identity(logicalHandle, info.DeviceName, device.DeviceId, device.DeviceName,
            bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    private static void Release(nint handle)
    {
        if (!PhysicalMonitorNative.DestroyPhysicalMonitor(handle))
        {
            throw NativeFailure("DestroyPhysicalMonitor");
        }
    }

    private static Win32Exception NativeFailure(string api)
    {
        var error = Marshal.GetLastPInvokeError();
        var detail = error == 0 ? "No extended error supplied." : new Win32Exception(error).Message;
        return new Win32Exception(error, $"{api}: FAILED; Win32={error} (0x{error:X8}): {detail}");
    }
}
