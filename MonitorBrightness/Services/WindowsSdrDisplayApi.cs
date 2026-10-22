using System.ComponentModel;
using System.Runtime.InteropServices;
using N = MonitorBrightness.Native.DisplayConfigNative;

namespace MonitorBrightness.Services;

internal sealed class WindowsSdrDisplayApi : ISdrDisplayApi
{
    public IReadOnlyList<SdrDisplay> GetActiveDisplays()
    {
        // INFO_2 distinguishes actual HDR from wide-color SDR. Do not guess on older OS versions.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
            throw new InvalidOperationException("This brightness control requires Windows 11 24H2 (build 26100) or newer to verify active HDR.");
        Check(N.GetDisplayConfigBufferSizes(N.ActivePaths, out var pathCount, out var modeCount), "GetDisplayConfigBufferSizes");
        if (pathCount is 0 or > 64 || modeCount is 0 or > 256)
            throw new InvalidOperationException("No active display, or unexpected DisplayConfig buffer sizes. No write allowed.");
        var paths = new N.DisplayPath[pathCount];
        var modes = new N.ModeInfo[modeCount];
        Check(N.QueryDisplayConfig(N.ActivePaths, ref pathCount, paths, ref modeCount, modes, 0), "QueryDisplayConfig");
        // No retry even on ERROR_INSUFFICIENT_BUFFER: a topology race must stop this test.
        if (pathCount != 1 || (paths[0].Flags & 1) == 0)
            throw new InvalidOperationException("Brightness control requires exactly one active display path, with the internal laptop panel disabled.");
        var path = paths[0];
        var target = Id(path.Target.Adapter, path.Target.Id);
        var source = Id(path.Source.Adapter, path.Source.Id);
        var name = new N.TargetName { Header = Header<N.TargetName>(2, target) };
        Check(N.GetTargetName(ref name), "DisplayConfigGetDeviceInfo(target name)");
        var sourceName = new N.SourceName { Header = Header<N.SourceName>(1, source) };
        Check(N.GetSourceName(ref sourceName), "DisplayConfigGetDeviceInfo(source name)");
        if (name.OutputTechnology != path.Target.OutputTechnology)
            throw new InvalidOperationException("Conflicting connector identities; no write allowed.");
        var color = new N.AdvancedColorInfo { Header = Header<N.AdvancedColorInfo>(15, target) };
        Check(N.GetAdvancedColorInfo(ref color), "DisplayConfigGetDeviceInfo(HDR status)");
        return new[] { new SdrDisplay(target, source, name.DevicePath, sourceName.Name,
            string.IsNullOrWhiteSpace(name.Name) ? "Unnamed monitor" : name.Name,
            name.OutputTechnology, path.Target.Available != 0,
            color.ActiveColorMode == 2 && (color.Flags & 0x32) == 0x32) };
    }

    public uint ReadWhiteLevel(DisplayTargetId target)
    {
        var packet = new N.WhiteLevel { Header = Header<N.WhiteLevel>(11, target) };
        Check(N.GetWhiteLevel(ref packet), "DisplayConfigGetDeviceInfo(SDR white level)");
        return packet.Value;
    }

    public void WriteWhiteLevel(DisplayTargetId target, uint value)
    {
        var packet = CreateWritePacket(target, value);
        Check(N.SetSdrWhiteLevel(ref packet), "DisplayConfigSetDeviceInfo(SDR white level; undocumented)");
    }

    internal static N.SetWhiteLevel CreateWritePacket(DisplayTargetId target, uint value)
    {
        if (value < SdrTestMonitor.Minimum || value > SdrTestMonitor.Maximum)
            throw new InvalidOperationException("SDR request outside the M3 test range.");
        return new N.SetWhiteLevel
        {
            Header = Header<N.SetWhiteLevel>(0xFFFFFFEE, target),
            Value = value,
            Flag = 1
        };
    }

    private static DisplayTargetId Id(N.Luid adapter, uint id) => new(adapter.Low, adapter.High, id);

    private static N.Header Header<T>(uint type, DisplayTargetId target) where T : struct => new()
    {
        Type = type, Size = (uint)Marshal.SizeOf<T>(),
        Adapter = new N.Luid { Low = target.AdapterLow, High = target.AdapterHigh }, Id = target.Id
    };

    internal static void Check(int error, string operation)
    {
        if (error != 0)
            throw new Win32Exception(error, $"{operation}: FAILED; Win32={error} (0x{error:X8}): {new Win32Exception(error).Message} No automatic retry or fallback.");
    }
}
