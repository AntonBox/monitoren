using MonitorBrightness.Settings;
using MonitorBrightness.UI;

namespace MonitorBrightness;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Form1 popup = new();
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;
    private readonly Icon trayArtwork = RetroTheme.CreateTrayIcon();
    private HotkeyWindow? hotkeys;
    private string hotkeyStatus = "Hotkeys are starting...";
    private readonly SettingsStore store = new(SettingsStore.DefaultPath);
    private readonly StartupSettings startup = new(new WindowsStartupEntry(), () => Environment.ProcessPath, File.Exists);
    private AppSettings settings;
    private string? settingsWarning;
    private DiagnosticsForm? diagnostics;
    private bool exiting;
    private bool hotkeyNoticeShown;
    private bool settingsOpen;

    public TrayApplicationContext()
    {
        // Load bounded local JSON before showing any window; this does not contact a display.
        var loaded = store.Load();
        settings = loaded.Settings;
        settingsWarning = loaded.Warning;
        popup.ApplySettings(settings);
        menu.Items.Add("Open", null, (_, _) => ShowPopup());
        menu.Items.Add("Settings...", null, async (_, _) => await ShowSettingsAsync());
        menu.Items.Add("Read-only diagnostics", null, (_, _) => ShowDiagnostics());
        menu.Items.Add("Hotkeys...", null, (_, _) => RetroMessage.Show(hotkeyStatus,
            "Monitoren / Hotkeys"));
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());
        RetroTheme.StyleMenu(menu);

        trayIcon = new NotifyIcon
        {
            Icon = trayArtwork,
            Text = "Monitor Brightness",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.MouseClick += OnTrayMouseClick;
        RegisterCurrentHotkeys();
        if (settingsWarning is not null)
            trayIcon.ShowBalloonTip(5000, "Settings need attention", "Safe defaults are in use. Open Settings for details.", ToolTipIcon.Warning);
    }

    private void RegisterCurrentHotkeys()
    {
        try
        {
            hotkeys = new HotkeyWindow(OnHotkeyStep, settings);
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

    private async Task ShowSettingsAsync()
    {
        if (exiting || settingsOpen) return;
        settingsOpen = true;
        menu.Enabled = false;
        popup.Hide();
        hotkeys?.Dispose();
        if (!string.IsNullOrEmpty(hotkeys?.ShutdownError)) settingsWarning = hotkeys.ShutdownError;
        hotkeys = null;
        try
        {
            await popup.BeginSettingsAsync();
            var startupEnabled = await Task.Run(() => startup.Enabled);
            using var dialog = new SettingsForm(settings, SaveSettingsAsync, settingsWarning, startupEnabled);
            dialog.ShowDialog();
        }
        catch (Exception exception)
        {
            RetroMessage.Show(exception.Message, "Settings could not be opened");
        }
        finally
        {
            if (!exiting)
            {
                if (hotkeys is null) RegisterCurrentHotkeys(); // Cancel restores the previous pair.
                popup.SetHotkeyStatus(hotkeys?.Enabled == true);
                popup.EndSettings();
                menu.Enabled = true;
            }
            settingsOpen = false;
        }
    }

    private async Task SaveSettingsAsync(AppSettings candidate, bool startWithWindows)
    {
        candidate.Validate();
        HotkeyWindow? proposed = null;
        try
        {
            proposed = new HotkeyWindow(OnHotkeyStep, candidate);
            if (!proposed.Enabled) throw new InvalidOperationException(proposed.Status);
            await Task.Run(() => startup.Save(startWithWindows, () => store.Save(candidate)));
            // Shortcuts, startup preference and disk save succeeded. No brightness write is involved.
            settings = candidate;
            settingsWarning = null;
            hotkeys = proposed;
            proposed = null;
            hotkeyStatus = hotkeys.Status;
            popup.ApplySettings(settings);
            popup.SetHotkeyStatus(true);
        }
        finally { proposed?.Dispose(); }
    }

    private async void OnHotkeyStep(int step)
    {
        if (exiting || settingsOpen) return;
        var reason = await popup.RequestHotkeyStepAsync(step);
        if (exiting || settingsOpen) return;
        if (reason is null) hotkeyNoticeShown = false;
        else if (!hotkeyNoticeShown)
        {
            hotkeyNoticeShown = true;
            trayIcon.ShowBalloonTip(5000, "Brightness shortcut did not change the display", reason, ToolTipIcon.Info);
        }
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (exiting || settingsOpen || e.Button != MouseButtons.Left)
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
        if (!exiting && !settingsOpen) popup.ShowNearTray();
    }

    private void ShowDiagnostics()
    {
        if (exiting || settingsOpen) return;
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
        var errors = new List<string>();
        try { await popup.StopAsync(); }
        catch (Exception exception) { errors.Add(exception.Message); }
        try
        {
            if (diagnostics is not null) await diagnostics.StopAsync();
        }
        catch (Exception exception) { errors.Add(exception.Message); }

        if (!string.IsNullOrEmpty(hotkeys?.ShutdownError))
            errors.Add(hotkeys.ShutdownError);
        if (errors.Count != 0)
            RetroMessage.Show("Shutdown cleanup reported:\r\n" + string.Join("\r\n", errors), "Monitor Brightness");

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
            trayArtwork.Dispose();
            menu.Dispose();
            popup.Dispose();
            diagnostics?.Dispose();
        }

        base.Dispose(disposing);
    }
}
