using MonitorBrightness.Services;

namespace MonitorBrightness;

public partial class Form1 : Form
{
    private readonly BrightnessSliderController controller;
    private Task operation = Task.CompletedTask;
    private bool busy;
    private bool stopping;
    private bool updatingControls;
    private int topologyVersion;
    private decimal? lastRead;

    public Form1()
    {
        controller = new BrightnessSliderController(() =>
        {
            var version = Volatile.Read(ref topologyVersion);
            return SdrTestMonitor.Open(new WindowsSdrDisplayApi(), () => Volatile.Read(ref topologyVersion) != version);
        });
        InitializeComponent();
        controller.Updated += OnControllerUpdate;
        brightnessSlider.ValueChanged += (_, _) =>
        {
            if (updatingControls || busy || stopping || !controller.Ready) return;
            try
            {
                controller.Request(brightnessSlider.Value);
                percentageLabel.Text = $"Requested: {brightnessSlider.Value}% | Last read: {lastRead:F1}%";
                statusBox.Text = "Applying your latest slider position...";
            }
            catch (Exception exception) { ShowError(exception.Message); }
        };
        refreshButton.Click += async (_, _) =>
        {
            if (busy || stopping || !confirmMonitor.Checked) return;
            operation = ReadAsync();
            await operation;
        };
        confirmMonitor.CheckedChanged += async (_, _) =>
        {
            if (updatingControls || stopping) return;
            if (!confirmMonitor.Checked)
            {
                busy = true;
                UpdateButtons();
                operation = controller.DisconnectAsync();
                try { await operation; }
                catch (Exception exception) { ShowError(exception.Message); }
                finally { busy = false; UpdateButtons(); }
            }
            else
            {
                statusBox.Text = "Click Read / Refresh to verify the display and enable the slider.";
                UpdateButtons();
            }
        };
        DpiChanged += (_, _) => KeepOnScreen();
    }

    private async Task ReadAsync()
    {
        busy = true;
        UpdateButtons();
        statusBox.Text = "Reading current brightness...";
        var version = Volatile.Read(ref topologyVersion);
        try
        {
            BrightnessReading actual;
            if (controller.Ready)
            {
                actual = await controller.RefreshAsync();
            }
            else
            {
                var connection = await controller.ConnectAsync(confirmMonitor.Checked);
                monitorLabel.Text = connection.Name;
                actual = connection.Reading;
            }
            if (!stopping && version == Volatile.Read(ref topologyVersion) && controller.Ready)
                ShowActual(actual);
        }
        catch (Exception exception) { if (!stopping) ShowError(exception.Message); }
        finally { busy = false; UpdateButtons(); }
    }

    // Worker notifications are posted, never synchronously invoked across the UI boundary.
    private void OnControllerUpdate(SliderUpdate update)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (stopping || IsDisposed || update.Revision != controller.Revision) return;
                if (update.Error is not null) ShowError(update.Error);
                else if (update.Reading is BrightnessReading reading) ShowActual(reading);
                UpdateButtons();
            }));
        }
        catch (InvalidOperationException) { /* Handle is being destroyed during shutdown. */ }
    }

    private void ShowActual(BrightnessReading actual)
    {
        lastRead = actual.Percent;
        updatingControls = true;
        try { brightnessSlider.Value = Math.Clamp((int)decimal.Round(actual.Percent), 10, 100); }
        finally { updatingControls = false; }
        percentageLabel.Text = $"Brightness: {actual.Percent:F1}%";
        statusBox.Text = actual.Percent < 10m
            ? "Current value is below the slider's 10% minimum. It has not been changed. Moving the slider applies 10% or higher."
            : "Current value read successfully. Move the slider to adjust desktop brightness.";
    }

    private void ShowError(string message)
    {
        updatingControls = true;
        try { confirmMonitor.Checked = false; }
        finally { updatingControls = false; }
        brightnessSlider.Enabled = false;
        percentageLabel.Text = lastRead is decimal value
            ? $"Brightness unavailable | Last confirmed: {value:F1}%" : "Brightness unavailable";
        statusBox.Text = message + "\r\nConfirm the AOC and click Read / Refresh to try again.";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (IsDisposed) return;
        confirmMonitor.Enabled = !busy && !stopping;
        refreshButton.Enabled = !busy && !stopping && confirmMonitor.Checked;
        brightnessSlider.Enabled = !busy && !stopping && confirmMonitor.Checked && controller.Ready;
    }

    protected override async void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && !busy && !stopping && confirmMonitor.Checked && controller.Ready)
        {
            operation = ReadAsync();
            await operation;
        }
    }

    internal void ShowNearTray()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Max(area.Left, area.Right - Width - 12), Math.Max(area.Top, area.Bottom - Height - 12));
        Show();
        KeepOnScreen();
        Activate();
    }

    private void KeepOnScreen()
    {
        if (!Visible) return;
        var area = Screen.FromControl(this).WorkingArea;
        Location = new Point(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }

    internal async Task StopAsync()
    {
        stopping = true;
        UpdateButtons();
        await controller.StopAsync();
        await operation;
        controller.Updated -= OnControllerUpdate;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x007E || (m.Msg == 0x0219 && m.WParam.ToInt64() is 0x0007 or 0x0018 or 0x8000 or 0x8003 or 0x8004))
        {
            Interlocked.Increment(ref topologyVersion);
            controller?.Invalidate("Display configuration changed. Control is disabled until you confirm the target again.");
            if (confirmMonitor is not null)
            {
                updatingControls = true;
                try { confirmMonitor.Checked = false; }
                finally { updatingControls = false; }
                UpdateButtons();
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }
}
