using System.Drawing.Drawing2D;

namespace MonitorBrightness.UI;

// Original, code-drawn Winamp-inspired skin. No downloaded assets or packages.
internal static class RetroTheme
{
    internal static readonly Color Panel = Color.FromArgb(40, 41, 57);
    internal static readonly Color Face = Color.FromArgb(65, 67, 85);
    internal static readonly Color Light = Color.FromArgb(133, 136, 153);
    internal static readonly Color Shadow = Color.FromArgb(14, 15, 23);
    internal static readonly Color Ink = Color.FromArgb(222, 222, 208);
    internal static readonly Color Display = Color.FromArgb(12, 20, 16);
    internal static readonly Color Green = Color.FromArgb(184, 234, 104);
    internal static readonly Color Gold = Color.FromArgb(226, 181, 91);

    internal static void Apply(Form form, string title)
    {
        if (SystemInformation.HighContrast) return;
        SetFont(form, "Tahoma", 9, FontStyle.Regular);
        StyleControls(form);
        var oldSize = form.ClientSize;
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Panel };
        foreach (Control child in form.Controls.Cast<Control>().ToArray()) body.Controls.Add(child);
        form.FormBorderStyle = FormBorderStyle.None;
        form.BackColor = Panel;
        form.Padding = new Padding(4);
        form.ClientSize = new Size(oldSize.Width + 8, oldSize.Height + 42);
        var header = new RetroHeader(form, title) { Dock = DockStyle.Top, Height = 28 };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 6, BackColor = Face };
        form.Controls.Add(body);
        form.Controls.Add(header);
        form.Controls.Add(bottom);
        form.Paint += (_, e) => Bevel(e.Graphics, form.ClientRectangle, false);
        form.Resize += (_, _) => form.Invalidate();
        // Dialogs were fixed-size already. Diagnostics retain a visible resize grip.
        if (form is DiagnosticsForm)
        {
            var grip = new Label { Text = "◢", ForeColor = Light, BackColor = Panel,
                AutoSize = false, Size = new Size(20, 20), Cursor = Cursors.SizeNWSE,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right, AccessibleName = "Resize diagnostics" };
            grip.Location = new Point(form.ClientSize.Width - 24, form.ClientSize.Height - 24);
            Point origin = default;
            Size initial = default;
            grip.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { origin = Cursor.Position; initial = form.Size; grip.Capture = true; } };
            grip.MouseMove += (_, _) =>
            {
                if (grip.Capture) form.Size = new Size(Math.Max(form.MinimumSize.Width, initial.Width + Cursor.Position.X - origin.X),
                    Math.Max(form.MinimumSize.Height, initial.Height + Cursor.Position.Y - origin.Y));
            };
            grip.MouseUp += (_, _) => grip.Capture = false;
            form.Controls.Add(grip);
            grip.BringToFront();
        }
    }

    internal static void SetFont(Control control, string family, float size, FontStyle style)
    {
        var font = new Font(family, size, style);
        control.Font = font;
        control.Disposed += (_, _) => font.Dispose();
    }

    private static void StyleControls(Control control)
    {
        control.BackColor = Panel;
        control.ForeColor = Ink;
        switch (control)
        {
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Light;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(83, 85, 103);
                button.FlatAppearance.MouseDownBackColor = Shadow;
                button.BackColor = Face;
                button.Padding = new Padding(5, 2, 5, 2);
                button.Paint += (_, e) =>
                {
                    if (!button.Enabled)
                    {
                        using var fill = new SolidBrush(Face);
                        e.Graphics.FillRectangle(fill, button.ClientRectangle);
                        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle,
                            Light, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                    Bevel(e.Graphics, button.ClientRectangle, false);
                };
                break;
            case TextBox box:
                box.BackColor = Display;
                box.ForeColor = Green;
                box.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox combo:
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Display;
                combo.ForeColor = Green;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.ItemHeight = Math.Max(combo.ItemHeight, 18);
                combo.DrawItem += (_, e) =>
                {
                    using var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Face : Display);
                    e.Graphics.FillRectangle(fill, e.Bounds);
                    var text = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
                    TextRenderer.DrawText(e.Graphics, text, e.Font, Rectangle.Inflate(e.Bounds, -3, 0),
                        combo.Enabled ? Green : Light, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    e.DrawFocusRectangle();
                };
                break;
            case NumericUpDown number:
                number.BackColor = Display;
                number.ForeColor = Green;
                number.BorderStyle = BorderStyle.FixedSingle;
                break;
            case CheckBox check:
                check.FlatStyle = FlatStyle.Flat;
                check.ForeColor = Gold;
                break;
        }
        // Native numeric editors own child controls with their own paint behavior.
        if (control is NumericUpDown) return;
        foreach (Control child in control.Controls) StyleControls(child);
    }

    internal static void Bevel(Graphics graphics, Rectangle bounds, bool inset)
    {
        if (bounds.Width < 2 || bounds.Height < 2) return;
        using var bright = new Pen(inset ? Shadow : Light);
        using var dark = new Pen(inset ? Light : Shadow);
        graphics.DrawLine(bright, bounds.Left, bounds.Bottom - 1, bounds.Left, bounds.Top);
        graphics.DrawLine(bright, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        graphics.DrawLine(dark, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        graphics.DrawLine(dark, bounds.Right - 1, bounds.Bottom - 1, bounds.Left, bounds.Bottom - 1);
    }

    internal static void StyleMenu(ContextMenuStrip menu)
    {
        if (SystemInformation.HighContrast) return;
        menu.BackColor = Panel;
        menu.ForeColor = Ink;
        menu.Renderer = new ToolStripProfessionalRenderer(new RetroMenuColors());
        menu.ShowImageMargin = false;
        menu.Padding = new Padding(4);
    }

    internal static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            using var face = new SolidBrush(Panel);
            using var screen = new SolidBrush(Display);
            using var gold = new Pen(Gold, 2);
            using var led = new SolidBrush(Green);
            graphics.FillRectangle(face, 2, 3, 28, 25);
            graphics.DrawRectangle(gold, 2, 3, 27, 24);
            graphics.FillRectangle(screen, 5, 7, 22, 13);
            for (var x = 7; x < 26; x += 4) graphics.FillRectangle(led, x, 10, 2, 7);
            graphics.DrawLine(gold, 6, 24, 25, 24);
        }
        using var png = new MemoryStream();
        bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
            writer.Write((byte)32); writer.Write((byte)32); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write((uint)png.Length); writer.Write(22u); writer.Write(png.ToArray());
        }
        stream.Position = 0;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private sealed class RetroMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Panel;
        public override Color MenuBorder => Light;
        public override Color MenuItemSelected => Face;
        public override Color MenuItemBorder => Gold;
        public override Color SeparatorDark => Shadow;
        public override Color SeparatorLight => Light;
    }

    private sealed class RetroHeader : Control
    {
        private readonly Form owner;
        private readonly string title;
        private readonly Font titleFont = new("Tahoma", 9, FontStyle.Bold);
        private Point dragStart;
        private Point windowStart;

        internal RetroHeader(Form owner, string title)
        {
            this.owner = owner;
            this.title = title;
            DoubleBuffered = true;
            var close = new Button { Text = "×", Dock = DockStyle.Right, Width = 27,
                TabStop = false, AccessibleName = "Close window", FlatStyle = FlatStyle.Flat,
                BackColor = Face, ForeColor = Ink, Margin = Padding.Empty };
            close.FlatAppearance.BorderColor = Light;
            close.Click += (_, _) => owner.Close(); // Retains popup/diagnostics hide-on-close.
            Controls.Add(close);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            using var background = new LinearGradientBrush(ClientRectangle, Light, Panel, LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(background, ClientRectangle);
            using var stripe = new Pen(Shadow);
            for (var y = 3; y < Height - 2; y += 4) e.Graphics.DrawLine(stripe, 5, y, Width - 33, y);
            var size = TextRenderer.MeasureText(title, titleFont);
            var box = new Rectangle(Math.Max(9, (Width - 28 - size.Width) / 2), 3, size.Width, Height - 6);
            using var plate = new SolidBrush(Panel);
            e.Graphics.FillRectangle(plate, box);
            TextRenderer.DrawText(e.Graphics, title, titleFont, box, Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            Bevel(e.Graphics, ClientRectangle, false);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragStart = Cursor.Position;
            windowStart = owner.Location;
            Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Capture) owner.Location = new Point(windowStart.X + Cursor.Position.X - dragStart.X,
                windowStart.Y + Cursor.Position.Y - dragStart.Y);
        }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
        protected override void Dispose(bool disposing) { if (disposing) titleFont.Dispose(); base.Dispose(disposing); }
    }
}
