using System.Security.Cryptography;
using System.Text;

namespace MonitorBrightness.Services;

internal readonly record struct DisplayTargetId(uint AdapterLow, int AdapterHigh, uint Id);

// DevicePath is a private identity token, held in memory only and never logged.
internal sealed record SdrDisplay(DisplayTargetId Target, DisplayTargetId Source, string DevicePath,
    string DisplayName, string Model, uint OutputTechnology, bool Available, bool HdrActive);

internal interface ISdrDisplayApi
{
    IReadOnlyList<SdrDisplay> GetActiveDisplays();
    uint ReadWhiteLevel(DisplayTargetId target);
    void WriteWhiteLevel(DisplayTargetId target, uint value);
}

internal sealed class SdrTestMonitor : ITestMonitor
{
    // Monitorian's conventional 80-480 nit SDR reference-white range. These are
    // application limits, NOT discovered panel limits or a measured luminance.
    internal const uint Minimum = 1000;
    internal const uint Maximum = 6000;
    private readonly ISdrDisplayApi api;
    private readonly SdrDisplay identity;
    private readonly Func<bool> topologyChanged;
    private bool invalidated;
    private bool disposed;

    private SdrTestMonitor(ISdrDisplayApi api, SdrDisplay identity, Func<bool> topologyChanged)
    {
        this.api = api;
        this.identity = identity;
        this.topologyChanged = topologyChanged;
    }

    public string DisplayName => $"{identity.Model} | HDMI | HDR on";

    public string Description => $"{identity.DisplayName} -> {identity.Model}; HDMI; HDR active.\r\n"
        + "AOC Q27G42XE is your confirmed identity; the name above is reported by Windows.\r\n"
        + "This test adjusts Windows SDR content brightness with HDR left on, not DDC/CI hardware brightness.\r\n"
        + "Test scale: 0-100% = reference white 80-480 nits (raw 1000-6000), following Monitorian.\r\n"
        + "This is an application range, not detected panel limits. Percent may differ from Windows Settings.\r\n"
        + "No device paths/serial numbers are logged. No physical monitor handles are acquired.";

    internal static SdrTestMonitor Open(ISdrDisplayApi api, Func<bool> topologyChanged, string? expectedFingerprint = null)
    {
        if (topologyChanged()) throw new InvalidOperationException("Display configuration changed before preparation.");
        var identity = ReadSoleExternal(api);
        if (expectedFingerprint is not null && !string.Equals(Fingerprint(identity), expectedFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active HDMI display does not match your saved selection. No brightness read or write sent. Open Settings and select the intended monitor explicitly.");
        var monitor = new SdrTestMonitor(api, identity, topologyChanged);
        monitor.ValidateIdentity();
        return monitor;
    }

    internal static string Fingerprint(SdrDisplay display) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(display.DevicePath.ToUpperInvariant())));

    internal static SdrDisplay ReadSoleExternal(ISdrDisplayApi api)
    {
        var displays = api.GetActiveDisplays();
        if (displays.Count != 1)
            throw new InvalidOperationException("This controller requires exactly one active display path. Disable the laptop panel and confirm the external AOC; no write allowed.");
        var display = displays[0];
        // Only HDMI is approved in this user's M3 setup. Embedded/unknown paths fail closed.
        if (!display.Available || display.OutputTechnology != 5 || string.IsNullOrWhiteSpace(display.DevicePath)
            || string.IsNullOrWhiteSpace(display.DisplayName))
            throw new InvalidOperationException("Cannot establish a unique available HDMI target. Internal, unknown, and ambiguous targets are blocked.");
        if (!display.HdrActive)
            throw new InvalidOperationException("HDR is not active on the selected display. No automatic HDR toggle or DDC fallback was attempted.");
        return display;
    }

    public void ValidateIdentity()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (invalidated) throw new InvalidOperationException("This brightness session is invalid after a display/HDR change; confirm the target again.");
        try
        {
            if (topologyChanged() || ReadSoleExternal(api) != identity)
                throw new InvalidOperationException("Display identity/configuration changed. Brightness control is blocked; use Windows HDR settings if manual adjustment is needed.");
        }
        catch
        {
            invalidated = true;
            throw;
        }
    }

    public BrightnessReading Read()
    {
        ValidateIdentity();
        var value = api.ReadWhiteLevel(identity.Target);
        ValidateIdentity();
        var reading = new BrightnessReading(Minimum, value, Maximum);
        reading.Validate(); // Unknown values are never clamped into a fabricated reading.
        return reading;
    }

    public void Write(uint value)
    {
        if (value < Minimum || value > Maximum)
            throw new InvalidOperationException("Requested SDR white level is outside the supported test scale.");
        ValidateIdentity();
        api.WriteWhiteLevel(identity.Target, value);
    }

    public void Dispose() => disposed = true; // DisplayConfig IDs are not owned native handles.

    internal static string FormatValue(uint raw) => $"{100m * (raw - Minimum) / (Maximum - Minimum):F1}% on the test scale; "
        + $"SDR reference white {raw * 0.08m:F2} nits; raw {raw}";
}
