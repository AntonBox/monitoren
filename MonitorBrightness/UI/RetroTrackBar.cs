using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MonitorBrightness.UI;

// Paint only: native TrackBar still owns input, bounds, focus and accessibility.
internal sealed class RetroTrackBar : TrackBar
{
    private int paintValue;

    internal RetroTrackBar()
    {
        AutoSize = false;
        Height = 44;
        TickStyle = TickStyle.None;
        BackColor = SystemInformation.HighContrast ? SystemColors.Control : RetroTheme.Panel;
    }

    protected override void OnValueChanged(EventArgs e)
    {
        // Value's getter updates WinForms' internal change-detection cache.
        // Read it only after the change is detected, never during native paint:
        // a repaint can happen before the corresponding scroll notification.
        paintValue = Value;
        base.OnValueChanged(e);
        Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        // WinForms reflects WM_NOTIFY to the originating control.
        if (!SystemInformation.HighContrast && m.Msg == 0x204E && m.LParam != 0
            && Marshal.PtrToStructure<NotifyHeader>(m.LParam).Code == -12) // NM_CUSTOMDRAW
        {
            var draw = Marshal.PtrToStructure<CustomDraw>(m.LParam);
            if (draw.Stage == 1) { m.Result = 0x20; return; } // CDDS_PREPAINT -> item notifications
            if (draw.Stage == 0x10001 && draw.Item is 2 or 3) // CDDS_ITEMPREPAINT; thumb/channel
            {
                var bounds = Rectangle.FromLTRB(draw.Left, draw.Top, draw.Right, draw.Bottom);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    using var graphics = Graphics.FromHdc(draw.Hdc);
                    if (draw.Item == 2) DrawThumb(graphics, bounds);
                    else DrawChannel(graphics, bounds);
                }
                m.Result = 4; // CDRF_SKIPDEFAULT
                return;
            }
        }
        base.WndProc(ref m);
    }

    private void DrawChannel(Graphics graphics, Rectangle bounds)
    {
        var scale = DeviceDpi / 96f;
        var height = Math.Max(bounds.Height, (int)(10 * scale));
        bounds = new Rectangle(bounds.X, bounds.Y + bounds.Height / 2 - height / 2, bounds.Width, height);
        using var dark = new SolidBrush(RetroTheme.Shadow);
        graphics.FillRectangle(dark, bounds);
        RetroTheme.Bevel(graphics, bounds, true);
        var inner = Rectangle.Inflate(bounds, -2, -2);
        var fraction = Maximum > Minimum
            ? (double)(Math.Clamp(paintValue, Minimum, Maximum) - Minimum) / (Maximum - Minimum) : 0;
        var filled = (int)(inner.Width * fraction);
        var segmentWidth = Math.Max(2, (int)(4 * scale));
        for (var x = 0; x < inner.Width; x += segmentWidth + 2)
        {
            var color = Enabled && x < filled ? RetroTheme.Green : Color.FromArgb(43, 54, 39);
            using var segment = new SolidBrush(color);
            graphics.FillRectangle(segment, inner.X + x, inner.Y, Math.Min(segmentWidth, inner.Width - x), Math.Max(1, inner.Height));
        }
    }

    private void DrawThumb(Graphics graphics, Rectangle bounds)
    {
        using var fill = new LinearGradientBrush(bounds, Enabled ? Color.FromArgb(255, 221, 147) : RetroTheme.Light,
            Enabled ? Color.FromArgb(149, 106, 42) : RetroTheme.Face, LinearGradientMode.Vertical);
        graphics.FillRectangle(fill, bounds);
        RetroTheme.Bevel(graphics, bounds, false);
        using var grip = new Pen(RetroTheme.Shadow);
        var center = bounds.X + bounds.Width / 2;
        for (var x = center - 2; x <= center + 2; x += 2)
            graphics.DrawLine(grip, x, bounds.Top + 4, x, bounds.Bottom - 5);
        if (Focused) ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -2, -2));
    }

    // Microsoft NMCUSTOMDRAW / NMHDR. Pointer-sized members preserve x64 alignment.
    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyHeader { internal nint Window; internal nuint Id; internal int Code; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CustomDraw
    {
        internal NotifyHeader Header;
        internal uint Stage;
        internal nint Hdc;
        internal int Left, Top, Right, Bottom;
        internal nuint Item;
        internal uint State;
        internal nint Parameter;
    }
}
