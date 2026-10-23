namespace MonitorBrightness.Settings;

internal sealed record HotkeyChord(uint Modifiers, uint Key)
{
    internal static readonly uint[] SupportedKeys =
        Enumerable.Range(0x30, 10).Concat(Enumerable.Range(0x41, 26))
        .Concat(Enumerable.Range(0x70, 11)).Concat(new[] { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28 })
        .Select(value => (uint)value).ToArray();

    internal void Validate()
    {
        if ((Modifiers & ~7u) != 0 || (Modifiers & 3u) == 0 || !SupportedKeys.Contains(Key))
            throw new InvalidOperationException("Use Ctrl and/or Alt, optional Shift, and a letter, digit, F1-F11, arrow, Home, End, PageUp or PageDown.");
    }

    public override string ToString() => ((Modifiers & 2) != 0 ? "Ctrl+" : "")
        + ((Modifiers & 1) != 0 ? "Alt+" : "") + ((Modifiers & 4) != 0 ? "Shift+" : "") + KeyName(Key);

    internal static string KeyName(uint key) => key switch
    {
        0x21 => "PageUp", 0x22 => "PageDown", 0x23 => "End", 0x24 => "Home",
        0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
        >= 0x70 and <= 0x7A => $"F{key - 0x6F}",
        _ => ((char)key).ToString()
    };
}

// A hash binds the preference to a Windows display path without saving that path.
// It is a local identifier, not a claim that Windows provides an immutable serial.
internal sealed record MonitorPreference(string Fingerprint, string Name)
{
    internal void Validate()
    {
        if (Fingerprint is null || Fingerprint.Length != 64 || !Fingerprint.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Name.Any(char.IsControl))
            throw new InvalidOperationException("Saved monitor identity is invalid. Select the monitor again in Settings.");
    }
}

internal sealed record AppSettings
{
    public int Version { get; init; } = 1;
    public MonitorPreference? Monitor { get; init; }
    public int StepPercent { get; init; } = 5;
    public int SafetyMinimum { get; init; } = 10;
    public HotkeyChord Increase { get; init; } = new(7, 0x21);
    public HotkeyChord Decrease { get; init; } = new(7, 0x22);

    internal void Validate()
    {
        if (Version != 1) throw new InvalidOperationException("Unsupported settings version.");
        if (StepPercent is < 1 or > 20) throw new InvalidOperationException("Step size must be 1-20 percentage points.");
        if (SafetyMinimum is < 0 or > 95) throw new InvalidOperationException("Brightness minimum must be 0-95%.");
        if (Increase is null || Decrease is null) throw new InvalidOperationException("Both shortcuts are required.");
        Increase.Validate();
        Decrease.Validate();
        if (Increase == Decrease) throw new InvalidOperationException("Increase and decrease shortcuts must differ.");
        Monitor?.Validate();
    }
}
