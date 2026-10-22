namespace MonitorBrightness;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Form1 popup = new();
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;

    public TrayApplicationContext()
    {
        menu.Items.Add("Open", null, (_, _) => ShowPopup());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Monitor Brightness - DEMO",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.MouseClick += OnTrayMouseClick;
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
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
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        popup.Location = new Point(
            Math.Max(workingArea.Left, workingArea.Right - popup.Width - 12),
            Math.Max(workingArea.Top, workingArea.Bottom - popup.Height - 12));
        popup.Show();
        popup.Activate();
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
        }

        base.Dispose(disposing);
    }
}
