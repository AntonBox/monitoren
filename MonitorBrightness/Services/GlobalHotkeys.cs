using System.ComponentModel;

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
    internal bool Enabled { get; private set; }
    internal string Status { get; private set; } = "Hotkeys are not registered.";

    internal void Start()
    {
        if (started) return;
        started = true;
        foreach (var (id, key, name) in new[]
        {
            (IncreaseId, PageUp, "Ctrl+Alt+Shift+PageUp"),
            (DecreaseId, PageDown, "Ctrl+Alt+Shift+PageDown")
        })
        {
            if (!api.Register(window, id, Modifiers | NoRepeat, key, out var error))
            {
                var cleanup = Stop();
                Status = $"Hotkeys unavailable: {name} could not be registered. {DescribeError(error)}\r\n"
                    + "Another app or another copy of Monitor Brightness may own this shortcut. "
                    + "Release the shortcut or exit the other copy, then restart this app. The slider still works.\r\n"
                    + "If these keys must stay assigned elsewhere, ask to use Ctrl+Alt+Shift+Up/Down instead."
                    + cleanup;
                return;
            }
            registered.Add(id);
        }
        Enabled = true;
        Status = "Ctrl+Alt+Shift+PageUp: +5 percentage points\r\n"
            + "Ctrl+Alt+Shift+PageDown: -5 percentage points\r\n"
            + "Confirm the AOC and click Read / Refresh first. Works with the popup closed. "
            + "Tap for each step; holding a key does not repeat. Range: 10-100%. HDR stays on.";
    }

    internal int StepForMessage(nint id, nint keyData)
    {
        if (!Enabled) return 0;
        var data = unchecked((uint)keyData.ToInt64());
        if ((data & 0xFFFF) != Modifiers) return 0;
        var key = data >> 16;
        if (id == IncreaseId && key == PageUp) return 5;
        if (id == DecreaseId && key == PageDown) return -5;
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
