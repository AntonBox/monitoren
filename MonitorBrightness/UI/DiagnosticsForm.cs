using System.Runtime.InteropServices;
using MonitorBrightness.Services;

namespace MonitorBrightness.UI;

internal sealed class DiagnosticsForm : Form
{
    private readonly Button scanButton = new() { Text = "Run read-only scan", AutoSize = true };
    private readonly Button copyButton = new() { Text = "Copy Diagnostics", AutoSize = true, Enabled = false };
    private readonly ComboBox candidatePicker = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly Label status = new() { Text = "Ready. Scanning starts only when you click Run read-only scan.", AutoSize = true };
    private readonly TextBox output = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        MaxLength = 0
    };
    private Task scanTask = Task.CompletedTask;
    private CancellationTokenSource? scanCancellation;
    private bool stopping;

    internal DiagnosticsForm()
    {
        Text = "Monitor Brightness - Read-only diagnostics";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 580);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(scanButton);
        buttons.Controls.Add(copyButton);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "READ-ONLY: monitor identity may be ambiguous. The brightness slider remains DEMO.", AutoSize = true }, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        layout.Controls.Add(new Label { Text = "Optional: choose the entry you recognize as external for the copied report (this scan only).", AutoSize = true }, 0, 2);
        layout.Controls.Add(candidatePicker, 0, 3);
        layout.Controls.Add(status, 0, 4);
        layout.Controls.Add(output, 0, 5);
        Controls.Add(layout);

        scanButton.Click += async (_, _) =>
        {
            if (!scanTask.IsCompleted || stopping)
            {
                return;
            }

            scanTask = RunScanAsync();
            await scanTask;
        };
        copyButton.Click += (_, _) => CopyDiagnostics();
    }

    private async Task RunScanAsync()
    {
        scanButton.Enabled = false;
        copyButton.Enabled = false;
        candidatePicker.Enabled = false;
        candidatePicker.Items.Clear();
        output.Clear();
        status.Text = "Reading monitors sequentially. This may take several seconds; the tray remains responsive.";
        scanCancellation = new CancellationTokenSource();
        try
        {
            var token = scanCancellation.Token;
            var result = await MonitorAccess.RunAsync(() => MonitorDiagnostics.Collect(token));
            // The await resumes on the WinForms UI thread.
            output.Text = result.Report;
            candidatePicker.Items.Add("Not selected / identity ambiguous");
            candidatePicker.Items.AddRange(result.Candidates);
            candidatePicker.SelectedIndex = 0;
            status.Text = "Scan finished. Review errors and identity, then copy the report.";
        }
        catch (Exception exception)
        {
            output.Text = $"Diagnostics failed: {exception.GetType().Name}: {exception.Message}";
            status.Text = "Scan failed. Copy the error for review.";
        }
        finally
        {
            scanCancellation.Dispose();
            scanCancellation = null;
            scanButton.Enabled = !stopping;
            copyButton.Enabled = !stopping && output.TextLength > 0;
            candidatePicker.Enabled = !stopping && candidatePicker.Items.Count > 1;
        }
    }

    private void CopyDiagnostics()
    {
        var selection = candidatePicker.SelectedIndex > 0
            ? $"User-selected external candidate (not independently verified): {candidatePicker.SelectedItem}"
            : "External candidate: NOT SELECTED; identity ambiguous.";
        try
        {
            Clipboard.SetText(selection + Environment.NewLine + Environment.NewLine + output.Text);
            status.Text = "Copied. Paste into the chat with Ctrl+V.";
        }
        catch (ExternalException)
        {
            status.Text = "Clipboard unavailable. Select report text with Ctrl+A, then Ctrl+C, or try Copy again.";
        }
    }

    internal async Task StopAsync()
    {
        stopping = true;
        scanButton.Enabled = false;
        status.Text = "Exiting: waiting for the current native read and handle cleanup...";
        scanCancellation?.Cancel();
        // Native reads cannot be interrupted safely; keep the UI responsive until cleanup finishes.
        await scanTask;
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
