using MonitorBrightness.Native;
using MonitorBrightness.Services;

namespace MonitorBrightness.UI;

// A separate message-only window survives popup hiding and DPI handle recreation.
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly Action<int> onStep;
    private readonly GlobalHotkeys hotkeys;
    private bool disposed;
    internal bool Enabled => hotkeys.Enabled;
    internal string Status => hotkeys.Status;
    internal string ShutdownError { get; private set; } = "";

    internal HotkeyWindow(Action<int> onStep)
    {
        this.onStep = onStep;
        CreateHandle(new CreateParams { Caption = "MonitorBrightness.Hotkeys", Parent = new nint(-3) });
        hotkeys = new GlobalHotkeys(new HotkeyNative(), Handle);
        try { hotkeys.Start(); }
        catch { Dispose(); throw; }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && !disposed && hotkeys is not null)
        {
            var step = hotkeys.StepForMessage(m.WParam, m.LParam);
            if (step != 0) onStep(step);
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { ShutdownError = hotkeys.Stop(); }
        finally { DestroyHandle(); }
    }
}
