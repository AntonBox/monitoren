namespace MonitorBrightness;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
        brightnessSlider.ValueChanged += (_, _) =>
            percentageLabel.Text = $"Demo brightness: {brightnessSlider.Value}%";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The tray context owns the application lifetime, not this popup.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }
}
