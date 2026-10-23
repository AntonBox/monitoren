namespace MonitorBrightness;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null!;
    private UI.RetroTrackBar brightnessSlider = null!;
    private Label percentageLabel = null!;
    private Label monitorLabel = null!;
    private Label hotkeyLabel = null!;
    private TextBox errorBox = null!;
    private TableLayoutPanel popupLayout = null!;

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
        brightnessSlider = new UI.RetroTrackBar
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
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 8, 0),
            Height = 58
        };
        monitorLabel = new Label
        {
            Text = "No monitor selected - open Settings",
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            Anchor = AnchorStyles.Left
        };
        hotkeyLabel = new Label
        {
            AutoSize = true,
            Text = "Hotkeys are starting...",
            MaximumSize = new Size(430, 0)
        };
        errorBox = new TextBox
        {
            ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, TabIndex = 3,
            Visible = false
        };
        popupLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        popupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        popupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model
        popupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); // LCD
        popupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Slider
        popupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Shortcuts
        popupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0)); // Errors only
        popupLayout.Controls.Add(monitorLabel, 0, 0);
        popupLayout.Controls.Add(percentageLabel, 0, 1);
        popupLayout.Controls.Add(brightnessSlider, 0, 2);
        popupLayout.Controls.Add(hotkeyLabel, 0, 3);
        popupLayout.Controls.Add(errorBox, 0, 4);

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 185);
        Controls.Add(popupLayout);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Monitor Brightness";
        UI.RetroTheme.Apply(this, "MONITOREN  /  DESKTOP AMP");
        if (!SystemInformation.HighContrast)
        {
            percentageLabel.BackColor = UI.RetroTheme.Display;
            percentageLabel.ForeColor = UI.RetroTheme.Green;
            UI.RetroTheme.SetFont(percentageLabel, "Consolas", 13, FontStyle.Bold);
            percentageLabel.Paint += (_, e) => UI.RetroTheme.Bevel(e.Graphics, percentageLabel.ClientRectangle, true);
            monitorLabel.ForeColor = UI.RetroTheme.Gold;
        }
    }

    #endregion
}
