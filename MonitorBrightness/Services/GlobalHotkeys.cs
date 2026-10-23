using System.ComponentModel;
using MonitorBrightness.Settings;

namespace MonitorBrightness.Services;

internal interface IHotkeyApi
{
    bool Register(nint window, int id, uint modifiers, uint key, out int error);
    bool Unregister(nint window, int id, out int error);
}

// Owned and called only by the UI thread. Registration itself never accesses a display.
internal sealed class GlobalHotkeys(IHotkeyApi api, nint window)
{
    internal const int IncreaseId = 1;
    internal const int DecreaseId = 2;
    internal const uint Modifiers = 0x0007; // Ctrl + Alt + Shift
    internal const uint NoRepeat = 0x4000;
    internal const uint PageUp = 0x21;
    internal const uint PageDown = 0x22;
    private readonly HashSet<int> registered = [];
    private bool started;
    private AppSettings settings = new();
    internal bool Enabled { get; private set; }
    internal string Status { get; private set; } = "Hotkeys are not registered.";

    internal void Start(AppSettings? configuration = null)
    {
        if (started) return;
        settings = configuration ?? new();
        settings.Validate();
        started = true;
        foreach (var (id, chord) in new[]
        {
            (IncreaseId, settings.Increase),
            (DecreaseId, settings.Decrease)
        })
        {
            if (!api.Register(window, id, chord.Modifiers | NoRepeat, chord.Key, out var error))
            {
                var cleanup = Stop();
                Status = $"Hotkeys unavailable: {chord} could not be registered. {DescribeError(error)}\r\n"
                    + "Another app or another copy of Monitor Brightness may own this shortcut. "
                    + "Release the shortcut or exit the other copy, then restart this app. The slider still works.\r\n"
                    + "You can choose different combinations in Settings."
                    + cleanup;
                return;
            }
            registered.Add(id);
        }
        Enabled = true;
        Status = $"{settings.Increase}: +{settings.StepPercent} percentage points\r\n"
            + $"{settings.Decrease}: -{settings.StepPercent} percentage points\r\n"
            + "Uses your saved monitor selection, including with the popup closed. "
            + $"Tap for each step; holding a key does not repeat. Range: {settings.SafetyMinimum}-100%. HDR stays on.";
    }

    internal int StepForMessage(nint id, nint keyData)
    {
        if (!Enabled) return 0;
        var data = unchecked((uint)keyData.ToInt64());
        var modifiers = data & 0xFFFF;
        var key = data >> 16;
        if (id == IncreaseId && key == settings.Increase.Key && modifiers == settings.Increase.Modifiers) return settings.StepPercent;
        if (id == DecreaseId && key == settings.Decrease.Key && modifiers == settings.Decrease.Modifiers) return -settings.StepPercent;
        return 0;
    }

    internal string Stop()
    {
        Enabled = false; // Queued WM_HOTKEY messages must not act during cleanup.
        var errors = new List<string>();
        foreach (var id in registered.ToArray())
        {
            if (api.Unregister(window, id, out var error)) registered.Remove(id);
            else errors.Add($"UnregisterHotKey({id}): {DescribeError(error)}");
        }
        return errors.Count == 0 ? "" : "\r\n" + string.Join("\r\n", errors);
    }

    private static string DescribeError(int error) =>
        $"Win32={error} (0x{error:X8}): " + (error == 0
            ? "The API returned failure without an extended error."
            : new Win32Exception(error).Message);
}
