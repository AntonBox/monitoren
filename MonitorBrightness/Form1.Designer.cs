namespace MonitorBrightness;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null!;
    private TrackBar brightnessSlider = null!;
    private Label percentageLabel = null!;

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
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            TickFrequency = 10,
            SmallChange = 1,
            LargeChange = 10,
            Dock = DockStyle.Fill,
            AccessibleName = "Demo brightness",
            TabIndex = 0
        };
        percentageLabel = new Label
        {
            Text = "Demo brightness: 50%",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        var demoLabel = new Label
        {
            Text = "DEMO - Monitor brightness is unchanged.",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(demoLabel, 0, 0);
        layout.Controls.Add(brightnessSlider, 0, 1);
        layout.Controls.Add(percentageLabel, 0, 2);

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(340, 150);
        Controls.Add(layout);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Monitor Brightness - DEMO";
    }

    #endregion
}
