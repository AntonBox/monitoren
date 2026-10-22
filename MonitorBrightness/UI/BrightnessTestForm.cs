using MonitorBrightness.Services;

namespace MonitorBrightness.UI;

internal sealed class BrightnessTestForm : Form
{
    private readonly CheckBox externalConfirmed = new()
    {
        AutoSize = true,
        Text = "I confirm the ONLY active display is my AOC Q27G42XE over HDMI; the laptop panel is disabled."
    };
    private readonly Button testButton = new() { Text = "Test brightness...", AutoSize = true, Enabled = false };
    private readonly Button restoreButton = new() { Text = "Restore original...", AutoSize = true, Enabled = false };
    private readonly TextBox output = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Both, WordWrap = false, MaxLength = 0
    };
    private BrightnessTestSession? session;
    private Task operation = Task.CompletedTask;
    private bool busy;
    private bool stopping;
    private bool testConsumed;
    private int topologyVersion;

    internal BrightnessTestForm()
    {
        Text = "Monitor Brightness - M3 HDR desktop brightness test";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 420);
        MinimumSize = new Size(700, 340);
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "HDR stays on. One SDR test of up to 5 points (10-100% limits), then Restore. Keep this monitor connected.\r\n"
                + "First note your Windows Settings > Display > HDR > SDR content brightness value for manual recovery.\r\n"
                + "Test reads first and asks for approval. The tray slider remains DEMO. Nothing changes on startup."
        }, 0, 0);
        layout.Controls.Add(externalConfirmed, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(testButton);
        buttons.Controls.Add(restoreButton);
        layout.Controls.Add(buttons, 0, 2);
        layout.Controls.Add(output, 0, 3);
        Controls.Add(layout);
        Log("Waiting for your external-monitor confirmation. No display reads or writes have been sent by this window.\r\n"
            + "SDR content brightness only; HDR video/games may not dim. Uses the user-approved undocumented Windows SDR setter.");

        externalConfirmed.CheckedChanged += (_, _) => UpdateButtons();
        testButton.Click += async (_, _) =>
        {
            if (busy || stopping || testConsumed || !externalConfirmed.Checked)
            {
                return;
            }

            operation = TestAsync();
            await operation;
        };
        restoreButton.Click += async (_, _) =>
        {
            if (busy || stopping || session is null)
            {
                return;
            }

            operation = RestoreAsync();
            await operation;
        };
    }

    private async Task TestAsync()
    {
        busy = true;
        UpdateButtons();
        var version = Volatile.Read(ref topologyVersion);
        try
        {
            Log("Reading the sole active display; no write yet...");
            session = await MonitorAccess.RunAsync(() => BrightnessTestSession.Prepare(
                SdrTestMonitor.Open(new WindowsSdrDisplayApi(), () => Volatile.Read(ref topologyVersion) != version),
                externalConfirmed: true, expectedStartingPercent: null, changePercentagePoints: 5));
            Log(session.Description);
            Log($"Read original: {SdrTestMonitor.FormatValue(session.Original.Current)}.");
            if (stopping || !externalConfirmed.Checked || Volatile.Read(ref topologyVersion) != version)
            {
                Log("Cancelled before approval; no write made.");
                return;
            }

            var approved = MessageBox.Show(this,
                $"Confirmed by you: AOC Q27G42XE over HDMI, laptop panel disabled.\r\n{session.Description}\r\n\r\n"
                + $"Approve ONE Windows SDR-content brightness change? HDR stays enabled.\r\n"
                + $"FROM: {SdrTestMonitor.FormatValue(session.Original.Current)}\r\n"
                + $"TO: {SdrTestMonitor.FormatValue(session.Target)}\r\n\r\n"
                + "Restore will offer the exact saved raw value. Choose No if the monitor identity is uncertain.",
                "Approve first SDR brightness change?", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;

            if (!approved || stopping || !externalConfirmed.Checked)
            {
                Log("Not approved; no brightness write made.");
                return;
            }

            Log(await MonitorAccess.RunAsync(() => session.Test(approved)));
        }
        catch (Exception exception)
        {
            Log($"Test stopped: {exception.Message}");
        }
        finally
        {
            testConsumed |= session?.TestAttempted == true;
            if (session?.NeedsRestore == true)
            {
                Log($"Original retained for Restore: {SdrTestMonitor.FormatValue(session.Original.Current)}. Keep this app open until restored.");
            }
            else if (session is not null && !session.TestAttempted)
            {
                await ReleaseSessionAsync();
            }

            busy = false;
            UpdateButtons();
        }
    }

    private async Task RestoreAsync()
    {
        busy = true;
        UpdateButtons();
        try
        {
            var retained = session!;
            var approved = MessageBox.Show(this,
                $"Restore Windows SDR content brightness on the SAME confirmed external monitor?\r\n"
                + $"Original: {SdrTestMonitor.FormatValue(retained.Original.Current)}.\r\n"
                + "HDR stays enabled. No other monitor will be selected if it disconnected.",
                "Approve Restore?", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            if (!approved || stopping)
            {
                Log("Restore not approved; no write made.");
                return;
            }

            Log(await MonitorAccess.RunAsync(() => retained.Restore(approved)));
        }
        catch (Exception exception)
        {
            Log($"Restore stopped: {exception.Message}");
        }
        finally
        {
            if (session?.Restored == true)
            {
                await ReleaseSessionAsync();
            }
            else if (session?.NeedsRestore == true)
            {
                Log("If Restore is unavailable, use Windows Settings > System > Display > AOC > HDR > SDR content brightness "
                    + "and return to the Windows value you noted before the test. The test scale may differ. No automatic retry.");
            }

            busy = false;
            UpdateButtons();
        }
    }

    // Called by tray Exit. Never restores automatically.
    internal async Task<bool> RequestExitAsync()
    {
        stopping = true;
        UpdateButtons();
        await operation;
        if (session?.NeedsRestore == true)
        {
            Show();
            Activate();
            var exit = MessageBox.Show(this,
                $"Restoration to the original {SdrTestMonitor.FormatValue(session.Original.Current)} has not been confirmed.\r\n"
                + "Exit without restoring? Choosing No keeps Restore available. Exiting never changes brightness automatically.",
                "Exit with restoration pending?", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            if (!exit)
            {
                stopping = false;
                UpdateButtons();
                return false;
            }
        }

        await ReleaseSessionAsync();
        return true;
    }

    private async Task ReleaseSessionAsync()
    {
        var retained = session;
        session = null;
        if (retained is null)
        {
            return;
        }

        try
        {
            await MonitorAccess.RunAsync(() => { retained.Dispose(); return true; });
            Log("SDR test session released. DisplayConfig uses IDs, so there are no owned physical handles to release. No automatic brightness change.");
        }
        catch (Exception exception)
        {
            Log($"Session cleanup failed: {exception.Message}");
        }
    }

    private void UpdateButtons()
    {
        externalConfirmed.Enabled = !busy && !stopping && !testConsumed;
        testButton.Enabled = !busy && !stopping && !testConsumed && externalConfirmed.Checked;
        restoreButton.Enabled = !busy && !stopping && session is { NeedsRestore: true, RestoreAttempted: false };
    }

    private void Log(string message) => output.AppendText(message + Environment.NewLine + Environment.NewLine);

    protected override void WndProc(ref Message m)
    {
        const int WmDisplayChange = 0x007E;
        const int WmDeviceChange = 0x0219;
        if (m.Msg == WmDisplayChange || (m.Msg == WmDeviceChange && m.WParam.ToInt64() is 0x0007 or 0x0018 or 0x8000 or 0x8003 or 0x8004))
        {
            Interlocked.Increment(ref topologyVersion);
            externalConfirmed.Checked = false;
            Log("Display/device configuration changed. Any retained test session is invalid; no retargeting is allowed.");
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }
}
