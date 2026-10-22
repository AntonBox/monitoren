using MonitorBrightness.UI;

namespace MonitorBrightness;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Form1 popup = new();
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;
    private DiagnosticsForm? diagnostics;
    private bool exiting;

    public TrayApplicationContext()
    {
        menu.Items.Add("Open", null, (_, _) => ShowPopup());
        menu.Items.Add("Read-only diagnostics", null, (_, _) => ShowDiagnostics());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Monitor Brightness",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.MouseClick += OnTrayMouseClick;
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
        menu.Enabled = false;
        popup.Hide();
        trayIcon.Text = "Monitor Brightness - finishing current operation";
        await popup.StopAsync();

        if (diagnostics is not null)
        {
            await diagnostics.StopAsync();
        }

        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        trayIcon.Visible = false;
        popup.Hide();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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
