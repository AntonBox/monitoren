using System.Reflection;

// Hidden TrackBar controls only: no forms, tray context, display services,
// real settings or global hotkeys are created by these checks.
internal static class Program
{
    private static readonly Type SliderType = typeof(MonitorBrightness.Form1).Assembly
        .GetType("MonitorBrightness.UI.RetroTrackBar")!;

    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        var passed = 0;
        try
        {
            using (var native = new TrackBar())
            {
                CheckInput(native, 5, 65, paint: false);
                passed++;
            }
            // The native trackbar can repaint its new position BEFORE sending
            // WM_HSCROLL. The paint callback must not swallow ValueChanged.
            foreach (var (name, code, target) in new[]
            {
                ("drag", 5, 65), ("end track", 8, 65),
                ("left arrow", 0, 49), ("right arrow", 1, 51),
                ("page up", 2, 40), ("page down", 3, 60),
                ("home", 6, 10), ("end", 7, 100)
            })
            {
                using var slider = CreateSlider();
                CheckInput(slider, code, target, paint: true);
                Console.WriteLine($"PASS: repaint before {name} preserves one ValueChanged event.");
                passed++;
            }
            using (var slider = CreateSlider())
            {
                _ = slider.Handle;
                slider.Maximum = 100;
                var events = 0;
                slider.ValueChanged += (_, _) => events++;
                slider.Value = 73;
                PaintChannel(slider);
                PaintChannel(slider);
                if (events != 1 || slider.Value != 73)
                    throw new InvalidOperationException("Programmatic value/repaint generated extra changes.");
                passed++;
            }
            Console.WriteLine($"All {passed} UI checks passed. No monitor commands or global input were sent.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL after {passed} passed: {exception.Message}");
            return 1;
        }
    }

    private static TrackBar CreateSlider() => (TrackBar)Activator.CreateInstance(SliderType, nonPublic: true)!;

    private static void CheckInput(TrackBar slider, int scrollCode, int target, bool paint)
    {
        slider.Minimum = 10;
        slider.Maximum = 100;
        slider.Value = 50;
        _ = slider.Handle;
        var values = new List<int>();
        slider.ValueChanged += (_, _) => values.Add(slider.Value);
        // TBM_SETPOS changes native position without changing WinForms' cached
        // Value. This models the first part of a native mouse/keyboard action.
        Dispatch(slider, 0x0405, 0, target);
        if (paint) PaintChannel(slider);
        // WinForms-reflected WM_HSCROLL delivers the second part of that action.
        Dispatch(slider, 0x2114, scrollCode | (scrollCode == 5 ? target << 16 : 0), slider.Handle);
        if (values.Count != 1 || values[0] != target)
            throw new InvalidOperationException($"Scroll {scrollCode}: expected one change to {target}, got [{string.Join(", ", values)}].");
    }

    private static void PaintChannel(TrackBar slider)
    {
        using var bitmap = new Bitmap(300, 44);
        using var graphics = Graphics.FromImage(bitmap);
        SliderType.GetMethod("DrawChannel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(slider, [graphics, new Rectangle(8, 10, 280, 10)]);
    }

    private static void Dispatch(TrackBar slider, int message, nint wParam, nint lParam)
    {
        var packet = Message.Create(slider.Handle, message, wParam, lParam);
        slider.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(slider, [packet]);
    }
}
