using MonitorBrightness.Services;
using MonitorBrightness.Settings;

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
    private AppSettings settings = new();
    private bool configuring;

    public Form1()
    {
        controller = new BrightnessSliderController(() =>
        {
            var version = Volatile.Read(ref topologyVersion);
            var selected = settings.Monitor ?? throw new InvalidOperationException("Select the external monitor in Settings first.");
            return SdrTestMonitor.Open(new WindowsSdrDisplayApi(), () => Volatile.Read(ref topologyVersion) != version, selected.Fingerprint);
        });
        InitializeComponent();
        controller.Updated += OnControllerUpdate;
        brightnessSlider.ValueChanged += (_, _) =>
        {
            if (updatingControls || busy || configuring || stopping || !controller.Ready) return;
            try
            {
                controller.Request(brightnessSlider.Value);
                percentageLabel.Text = $"SET {brightnessSlider.Value}%  /  READ {lastRead:F1}%";
                SetNotice("");
            }
            catch (Exception exception) { ShowError(exception.Message); }
        };
        DpiChanged += (_, _) => KeepOnScreen();
    }

    internal async Task BeginSettingsAsync()
    {
        configuring = true;
        Interlocked.Increment(ref topologyVersion); // Discard key presses waiting on an older connection.
        UpdateButtons();
        controller.Invalidate("Settings are open. Control will reconnect to the saved monitor when next used.");
        await operation;
        await controller.DisconnectAsync();
        await controller.WaitForIdleAsync();
    }

    internal void ApplySettings(AppSettings configuration)
    {
        configuration.Validate();
        controller.SetSafetyMinimum(configuration.SafetyMinimum);
        settings = configuration;
        updatingControls = true;
        try
        {
            brightnessSlider.Minimum = settings.SafetyMinimum;
            brightnessSlider.Value = Math.Clamp(brightnessSlider.Value, settings.SafetyMinimum, 100);
            brightnessSlider.AccessibleName = $"Desktop brightness, {settings.SafetyMinimum} to 100 percent";
        }
        finally { updatingControls = false; }
        monitorLabel.Text = settings.Monitor is null ? "No monitor selected - open Settings" : settings.Monitor.Name;
        percentageLabel.Text = "--.- %  /  NOT READ";
        lastRead = null;
        SetNotice("");
        UpdateButtons();
    }

    internal void EndSettings() { configuring = false; UpdateButtons(); }

    private async Task ReadAsync()
    {
        busy = true;
        UpdateButtons();
        percentageLabel.Text = "READING...";
        SetNotice("");
        var version = Volatile.Read(ref topologyVersion);
        try
        {
            // Keep receiving topology broadcasts even if a hotkey is the first
            // interaction and the popup has never been shown.
            _ = Handle;
            BrightnessReading actual;
            if (controller.Ready)
            {
                actual = await controller.RefreshAsync();
            }
            else
            {
                var connection = await controller.ConnectAsync(settings.Monitor is not null);
                monitorLabel.Text = connection.Name;
                actual = connection.Reading;
            }
            if (!stopping && !configuring && version == Volatile.Read(ref topologyVersion) && controller.Ready)
                ShowActual(actual);
        }
        catch (Exception exception) { if (!stopping) ShowError(exception.Message); }
        finally { busy = false; UpdateButtons(); }
    }

    internal void SetHotkeyStatus(bool registered)
    {
        hotkeyLabel.Text = registered
            ? $"{settings.Increase} / {settings.Decrease} (+{settings.StepPercent} / -{settings.StepPercent})\r\nUses your saved monitor selection."
            : "Hotkeys unavailable. Open 'Hotkeys...' in the tray menu for details.\r\nThe slider can still be used.";
    }

    internal async Task<string?> RequestHotkeyStepAsync(int step)
    {
        if (stopping) return null;
        if (configuring) return "Brightness control is paused while Settings is open.";
        if (settings.Monitor is null) return "Select your external monitor in Settings first.";
        var version = Volatile.Read(ref topologyVersion);
        try
        {
            // Concurrent presses share the read already in progress, then add
            // their steps to the controller's latest intent. A failure is not retried.
            if (busy) await operation;
            else if (!controller.Ready)
            {
                operation = ReadAsync();
                await operation;
            }
            if (stopping || configuring || version != Volatile.Read(ref topologyVersion)) return null;
            if (!controller.Ready) return errorBox.Text;
            var requested = controller.RequestStep(step);
            updatingControls = true;
            try { brightnessSlider.Value = Math.Clamp((int)decimal.Round(requested), settings.SafetyMinimum, 100); }
            finally { updatingControls = false; }
            percentageLabel.Text = $"SET {requested:F1}%  /  READ {lastRead:F1}%";
            SetNotice("");
            return null;
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return exception.Message;
        }
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
        try { brightnessSlider.Value = Math.Clamp((int)decimal.Round(actual.Percent), settings.SafetyMinimum, 100); }
        finally { updatingControls = false; }
        percentageLabel.Text = $"{actual.Percent:F1} %   /   SDR LEVEL";
        SetNotice(actual.Percent < settings.SafetyMinimum
            ? $"Current value is below the configured {settings.SafetyMinimum}% minimum. It has not been changed. Your next slider or hotkey request applies that minimum or higher."
            : "");
    }

    private void ShowError(string message)
    {
        brightnessSlider.Enabled = false;
        percentageLabel.Text = lastRead is decimal value
            ? $"OFFLINE / LAST {value:F1}%" : "--.- %  /  UNAVAILABLE";
        SetNotice(message + "\r\nReopen this popup or use a shortcut to retry. Choose Settings if your monitor selection changed.");
        UpdateButtons();
    }

    private void SetNotice(string message)
    {
        // Normal operation has no status section. Expand only for an error or
        // safety warning; retain selectable text and an explicit recovery path.
        errorBox.Text = message;
        var height = message.Length == 0 ? 0 : LogicalToDeviceUnits(88);
        var difference = height - (int)popupLayout.RowStyles[4].Height;
        errorBox.Visible = height != 0;
        popupLayout.RowStyles[4].Height = height;
        if (difference != 0) ClientSize = new Size(ClientSize.Width, ClientSize.Height + difference);
        KeepOnScreen();
    }

    private void UpdateButtons()
    {
        if (IsDisposed) return;
        brightnessSlider.Enabled = !busy && !configuring && !stopping && settings.Monitor is not null && controller.Ready;
    }

    protected override async void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && !busy && !configuring && !stopping && settings.Monitor is not null)
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
            controller?.Invalidate("Display configuration changed. The saved monitor will be checked again on your next request.");
            if (brightnessSlider is not null) UpdateButtons();
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }
}
