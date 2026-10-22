namespace MonitorBrightness;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null!;
    private TrackBar brightnessSlider = null!;
    private Label percentageLabel = null!;
    private Label monitorLabel = null!;
    private TextBox statusBox = null!;
    private CheckBox confirmMonitor = null!;
    private Button refreshButton = null!;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        brightnessSlider = new TrackBar
        {
            Minimum = 10,
            Maximum = 100,
            Value = 10,
            Enabled = false,
            TickFrequency = 10,
            SmallChange = 1,
            LargeChange = 10,
            Dock = DockStyle.Fill,
            AccessibleName = "Desktop brightness, 10 to 100 percent",
            TabIndex = 2
        };
        percentageLabel = new Label
        {
            Text = "Brightness: not read",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        monitorLabel = new Label
        {
            Text = "AOC Q27G42XE - confirmation required",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        var explanation = new Label
        {
            AutoSize = true,
            Text = "Desktop brightness | HDR stays on | 10-100%\r\nSDR content only; percentage may differ from Windows."
        };
        confirmMonitor = new CheckBox
        {
            AutoSize = true,
            Text = "Only my AOC HDMI display is active; laptop screen is off.",
            TabIndex = 0
        };
        refreshButton = new Button { Text = "Read / Refresh", AutoSize = true, Enabled = false, TabIndex = 1 };
        statusBox = new TextBox
        {
            ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, TabIndex = 3,
            Text = "Confirm the external display and click Read / Refresh. Brightness changes only when you move the slider."
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(monitorLabel, 0, 0);
        layout.Controls.Add(explanation, 0, 1);
        layout.Controls.Add(confirmMonitor, 0, 2);
        layout.Controls.Add(refreshButton, 0, 3);
        layout.Controls.Add(brightnessSlider, 0, 4);
        layout.Controls.Add(percentageLabel, 0, 5);
        layout.Controls.Add(statusBox, 0, 6);

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 310);
        Controls.Add(layout);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Monitor Brightness";
    }

    #endregion
}
