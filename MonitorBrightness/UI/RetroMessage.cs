namespace MonitorBrightness.UI;

internal static class RetroMessage
{
    // Informational messages only. Archived hardware approval dialogs stay native.
    internal static void Show(string text, string title)
    {
        using var dialog = new Form
        {
            Text = title, ClientSize = new Size(540, 270), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi
        };
        var content = new TextBox { Text = text, ReadOnly = true, Multiline = true,
            ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(ok);
        var body = new Panel { Padding = new Padding(12), Dock = DockStyle.Fill };
        body.Controls.Add(content);
        body.Controls.Add(footer);
        dialog.Controls.Add(body);
        dialog.AcceptButton = ok;
        dialog.CancelButton = ok;
        RetroTheme.Apply(dialog, title.ToUpperInvariant());
        dialog.ShowDialog();
    }
}
