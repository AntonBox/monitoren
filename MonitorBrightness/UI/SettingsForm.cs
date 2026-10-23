using MonitorBrightness.Services;
using MonitorBrightness.Settings;

namespace MonitorBrightness.UI;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox monitors = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox confirm = new() { Text = "I confirm the selected monitor is my external HDMI display.", AutoSize = true };
    private readonly NumericUpDown step = new() { Minimum = 1, Maximum = 20, Width = 80 };
    private readonly NumericUpDown floor = new() { Minimum = 0, Maximum = 95, Width = 80 };
    private readonly HotkeyEditor increase;
    private readonly HotkeyEditor decrease;
    private readonly Button scan = new() { Text = "Scan displays (read-only)", AutoSize = true };
    private readonly Button save = new() { Text = "Save", AutoSize = true };
    private readonly Button cancel = new() { Text = "Cancel", AutoSize = true };
    private readonly TextBox status = new() { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Func<AppSettings, Task> apply;
    private readonly MonitorPreference? originalMonitor;
    private readonly TableLayoutPanel layout;
    private bool working;

    internal SettingsForm(AppSettings current, Func<AppSettings, Task> apply, string? warning)
    {
        this.apply = apply;
        originalMonitor = current.Monitor;
        Text = "Monitor Brightness - Settings";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(610, 540);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        step.Value = current.StepPercent;
        floor.Value = current.SafetyMinimum;
        increase = new HotkeyEditor(current.Increase);
        decrease = new HotkeyEditor(current.Decrease);
        monitors.Items.Add(new MonitorChoice(null, "None - brightness control disabled"));
        if (current.Monitor is not null)
            monitors.Items.Add(new MonitorChoice(current.Monitor, current.Monitor.Name + " (saved; checked when connecting)"));
        monitors.SelectedIndex = current.Monitor is null ? 0 : 1;
        confirm.Visible = false;
        monitors.SelectedIndexChanged += (_, _) =>
        {
            confirm.Checked = false;
            var selected = ((MonitorChoice)monitors.SelectedItem!).Preference;
            confirm.Visible = selected is not null && selected != originalMonitor;
        };
        status.Text = warning ?? "Choose a monitor and preferences, then Save. Saving or loading settings never changes brightness. Control is paused while this window is open.";

        layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 12 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 10; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(570, 0), Text =
            "HDR stays on. Select one external HDMI display; keep the laptop panel disabled.\r\nA changed display path requires explicit selection again. No automatic brightness restore." }, 0, 0);
        layout.Controls.Add(monitors, 0, 1);
        layout.Controls.Add(scan, 0, 2);
        layout.Controls.Add(confirm, 0, 3);
        layout.Controls.Add(Row("Step (percentage points, 1-20):", step), 0, 4);
        layout.Controls.Add(Row("Brightness minimum (0-95%):", floor), 0, 5);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Default minimum: 10%. Setting 0 removes this extra safety floor." }, 0, 6);
        layout.Controls.Add(Row("Increase:", increase), 0, 7);
        layout.Controls.Add(Row("Decrease:", decrease), 0, 8);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Shortcuts need Ctrl and/or Alt. Holding a key does not repeat." }, 0, 9);
        layout.Controls.Add(status, 0, 10);
        layout.Controls.Add(Row("", save, cancel), 0, 11);
        Controls.Add(layout);
        cancel.Click += (_, _) => { if (!working) { DialogResult = DialogResult.Cancel; Close(); } };
        scan.Click += async (_, _) => await ScanAsync();
        save.Click += async (_, _) => await SaveAsync();
        RetroTheme.Apply(this, "MONITOREN  /  PREFERENCES");
    }

    private static FlowLayoutPanel Row(string label, params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        if (label.Length != 0) row.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        row.Controls.AddRange(controls);
        return row;
    }

    private async Task ScanAsync()
    {
        SetWorking(true);
        status.Text = "Reading display identity and HDR status...";
        try
        {
            var display = await MonitorAccess.RunAsync(() => SdrTestMonitor.ReadSoleExternal(new WindowsSdrDisplayApi()));
            var name = new string(display.Model.Where(c => !char.IsControl(c)).Take(128).ToArray()).Trim();
            if (name.Length == 0) name = "External HDMI monitor";
            var preference = new MonitorPreference(SdrTestMonitor.Fingerprint(display), name);
            var existing = monitors.Items.Cast<MonitorChoice>().FirstOrDefault(item => item.Preference?.Fingerprint == preference.Fingerprint);
            if (existing is null) monitors.Items.Add(new MonitorChoice(preference, $"{name} | HDMI | HDR on (detected)"));
            status.Text = $"Detected: {name} | HDMI | HDR on. Choose this display from the list and confirm it before saving. No brightness read or write was sent.";
        }
        catch (Exception exception) { status.Text = exception.Message; }
        finally { SetWorking(false); }
    }

    private async Task SaveAsync()
    {
        SetWorking(true);
        try
        {
            var selected = ((MonitorChoice)monitors.SelectedItem!).Preference;
            if (selected is not null && selected != originalMonitor && !confirm.Checked)
                throw new InvalidOperationException("Confirm the selected external HDMI monitor before saving.");
            var candidate = new AppSettings
            {
                Monitor = selected, StepPercent = (int)step.Value, SafetyMinimum = (int)floor.Value,
                Increase = increase.Chord, Decrease = decrease.Chord
            };
            candidate.Validate();
            status.Text = "Checking shortcuts and saving preferences...";
            await apply(candidate);
            working = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception) { status.Text = "Settings were not applied. " + exception.Message; }
        finally { if (!IsDisposed) SetWorking(false); }
    }

    private void SetWorking(bool value)
    {
        working = value;
        foreach (Control control in layout.Controls) if (control != status) control.Enabled = !value;
        UseWaitCursor = value;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (working && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    private sealed record MonitorChoice(MonitorPreference? Preference, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class HotkeyEditor : FlowLayoutPanel
    {
        private readonly CheckBox ctrl = new() { Text = "Ctrl", AutoSize = true };
        private readonly CheckBox alt = new() { Text = "Alt", AutoSize = true };
        private readonly CheckBox shift = new() { Text = "Shift", AutoSize = true };
        private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        internal HotkeyChord Chord => new((ctrl.Checked ? 2u : 0) | (alt.Checked ? 1u : 0) | (shift.Checked ? 4u : 0),
            ((KeyChoice)key.SelectedItem!).Value);

        internal HotkeyEditor(HotkeyChord chord)
        {
            AutoSize = true;
            WrapContents = false;
            ctrl.Checked = (chord.Modifiers & 2) != 0;
            alt.Checked = (chord.Modifiers & 1) != 0;
            shift.Checked = (chord.Modifiers & 4) != 0;
            foreach (var value in HotkeyChord.SupportedKeys) key.Items.Add(new KeyChoice(value));
            key.SelectedItem = key.Items.Cast<KeyChoice>().Single(item => item.Value == chord.Key);
            Controls.AddRange([ctrl, alt, shift, key]);
        }
        private sealed record KeyChoice(uint Value)
        {
            public override string ToString() => HotkeyChord.KeyName(Value);
        }
    }
}
