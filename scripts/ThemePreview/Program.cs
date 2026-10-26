using System.Reflection;
using System.Text.Json;
using System.Runtime.InteropServices;

// Off-screen visual review only. Never show a form, run the tray context, click
// controls, load real settings, register hotkeys or invoke a display service.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        var output = Path.GetFullPath(args.Length == 0 ? "scripts/ThemePreview/bin/previews" : args[0]);
        Directory.CreateDirectory(output);
        using var popup = new MonitorBrightness.Form1();
        Field<Label>(popup, "monitorLabel").Text = "Q27G42XE  |  HDMI  |  HDR on";
        Field<Label>(popup, "percentageLabel").Text = "73.0 %   /   SDR LEVEL";
        Field<Label>(popup, "hotkeyLabel").Text = "Ctrl+Alt+Shift+PageUp / PageDown (+5 / -5)\r\nUses your saved monitor selection.";
        var slider = Field<TrackBar>(popup, "brightnessSlider");
        slider.Value = 73; // Controller is disconnected; its event cannot request a write.
        slider.Enabled = true;
        Render(popup, Path.Combine(output, "popup.png"));
        typeof(MonitorBrightness.Form1).GetMethod("ShowError", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(popup, ["Sample error: selected monitor is disconnected."]);
        Render(popup, Path.Combine(output, "popup-error.png"));

        var assembly = typeof(MonitorBrightness.Form1).Assembly;
        using (var icon = (Icon)assembly.GetType("MonitorBrightness.UI.RetroTheme")!
            .GetMethod("CreateTrayIcon", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!)
        using (var bitmap = icon.ToBitmap())
            bitmap.Save(Path.Combine(output, "tray-icon.png"), System.Drawing.Imaging.ImageFormat.Png);
        var customDraw = assembly.GetType("MonitorBrightness.UI.RetroTrackBar")!.GetNestedType("CustomDraw", BindingFlags.NonPublic)!;
        if (Marshal.SizeOf(customDraw) != (IntPtr.Size == 8 ? 80 : 48)
            || Marshal.OffsetOf(customDraw, "Hdc").ToInt32() != (IntPtr.Size == 8 ? 32 : 16))
            throw new InvalidOperationException("Custom-draw native layout is incorrect.");
        var settingsType = assembly.GetType("MonitorBrightness.Settings.AppSettings")!;
        var settings = JsonSerializer.Deserialize("{}", settingsType)!;
        var applyType = typeof(Func<,,>).MakeGenericType(settingsType, typeof(bool), typeof(Task));
        var method = typeof(Program).GetMethod(nameof(NoSave), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(settingsType);
        var apply = method.CreateDelegate(applyType);
        using var preferences = (Form)Activator.CreateInstance(assembly.GetType("MonitorBrightness.UI.SettingsForm")!,
            BindingFlags.Instance | BindingFlags.NonPublic, null, [settings, apply, null, false], null)!;
        Render(preferences, Path.Combine(output, "settings.png"));
        using var diagnostics = (Form)Activator.CreateInstance(assembly.GetType("MonitorBrightness.UI.DiagnosticsForm")!, nonPublic: true)!;
        Field<TextBox>(diagnostics, "output").Text = "READ-ONLY DIAGNOSTICS\r\n\r\nNo scan has been run.\r\nClick Run read-only scan to inspect your display.";
        Render(diagnostics, Path.Combine(output, "diagnostics.png"));
        // Layout enlargement is a visual aid, not a substitute for real per-monitor DPI testing.
        preferences.Scale(new SizeF(1.5f, 1.5f));
        Render(preferences, Path.Combine(output, "settings-layout-150.png"));
        Console.WriteLine($"Rendered five previews to {output}. No native display calls, settings changes or hotkey registrations.");
    }
    private static Task NoSave<T>(T _, bool startup) => throw new InvalidOperationException("Preview must never save settings.");
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
    private static void Render(Form form, string path)
    {
        _ = form.Handle;
        // DrawToBitmap skips children whose parent is logically invisible. Set
        // only that managed flag for rendering; never Show/SetVisibleCore (which
        // would trigger the popup's live read). Its HWND stays hidden throughout.
        var setState = typeof(Control).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var visible = Enum.Parse(setState.GetParameters()[0].ParameterType, "Visible");
        setState.Invoke(form, [visible, true]);
        try
        {
            form.CreateControl();
            form.PerformLayout();
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            // Native combo boxes omit their selected text in WM_PRINT. Replay the
            // actual owner-draw callback into the artifact for that region only.
            using (var graphics = Graphics.FromImage(image)) DrawComboText(form, form, graphics);
            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally { setState.Invoke(form, [visible, false]); }
    }

    private static void DrawComboText(Form form, Control parent, Graphics graphics)
    {
        foreach (Control control in parent.Controls)
        {
            if (!control.Visible) continue;
            if (control is ComboBox combo && combo.DrawMode == DrawMode.OwnerDrawFixed)
            {
                var point = form.PointToClient(combo.PointToScreen(Point.Empty));
                var bounds = new Rectangle(point.X + 2, point.Y + 2, combo.Width - 21, combo.Height - 4);
                var args = new DrawItemEventArgs(graphics, combo.Font, bounds, combo.SelectedIndex, DrawItemState.ComboBoxEdit);
                typeof(ComboBox).GetMethod("OnDrawItem", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(combo, [args]);
            }
            DrawComboText(form, control, graphics);
        }
    }
}
