using MonitorBrightness.UI;

namespace MonitorBrightness;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Form1 popup = new();
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;
    private readonly HotkeyWindow? hotkeys;
    private readonly string hotkeyStatus;
    private DiagnosticsForm? diagnostics;
    private bool exiting;
    private bool hotkeyNoticeShown;

    public TrayApplicationContext()
    {
        menu.Items.Add("Open", null, (_, _) => ShowPopup());
        menu.Items.Add("Read-only diagnostics", null, (_, _) => ShowDiagnostics());
        menu.Items.Add("Hotkeys...", null, (_, _) => MessageBox.Show(hotkeyStatus,
            "Monitor Brightness - Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Monitor Brightness",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.MouseClick += OnTrayMouseClick;
        try
        {
            hotkeys = new HotkeyWindow(OnHotkeyStep);
            hotkeyStatus = hotkeys.Status;
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            hotkeyStatus = $"Hotkey window could not be created. Win32={exception.NativeErrorCode}: {exception.Message}. The slider still works.";
        }
        popup.SetHotkeyStatus(hotkeys?.Enabled == true);
        if (hotkeys?.Enabled != true)
            trayIcon.ShowBalloonTip(5000, "Brightness hotkeys unavailable",
                "Open Hotkeys... in the tray menu for the error and recovery steps. The slider still works.", ToolTipIcon.Warning);
    }

    private void OnHotkeyStep(int step)
    {
        if (exiting) return;
        var reason = popup.RequestHotkeyStep(step);
        if (reason is null) hotkeyNoticeShown = false;
        else if (!hotkeyNoticeShown)
        {
            hotkeyNoticeShown = true;
            trayIcon.ShowBalloonTip(5000, "Brightness shortcut did not change the display", reason, ToolTipIcon.Info);
        }
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (exiting || e.Button != MouseButtons.Left)
        {
            return;
        }

        if (popup.Visible)
        {
            popup.Hide();
        }
        else
        {
            ShowPopup();
        }
    }

    private void ShowPopup()
    {
        if (!exiting) popup.ShowNearTray();
    }

    private void ShowDiagnostics()
    {
        diagnostics ??= new DiagnosticsForm();
        diagnostics.Show();
        diagnostics.Activate();
    }

    private async Task ExitAsync()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        hotkeys?.Dispose();
        menu.Enabled = false;
        popup.Hide();
        trayIcon.Text = "Monitor Brightness - finishing current operation";
        await popup.StopAsync();

        if (diagnostics is not null)
        {
            await diagnostics.StopAsync();
        }

        if (!string.IsNullOrEmpty(hotkeys?.ShutdownError))
            MessageBox.Show("Hotkey cleanup reported:" + hotkeys.ShutdownError,
                "Monitor Brightness", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        hotkeys?.Dispose();
        trayIcon.Visible = false;
        popup.Hide();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hotkeys?.Dispose();
            trayIcon.Visible = false;
            trayIcon.MouseClick -= OnTrayMouseClick;
            trayIcon.Dispose();
            menu.Dispose();
            popup.Dispose();
            diagnostics?.Dispose();
        }

        base.Dispose(disposing);
    }
}
