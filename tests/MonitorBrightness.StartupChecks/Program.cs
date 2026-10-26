using MonitorBrightness.Settings;

var passed = 0;
const string path = @"C:\Apps With Spaces\MonitorBrightness.exe";
const string expected = "\"" + path + "\"";
const string oldCommand = "\"" + @"C:\Old\MonitorBrightness.exe" + "\"";
void Check(string name, Action action)
{
    action();
    Console.WriteLine("PASS: " + name);
    passed++;
}
static void Require(bool condition)
{
    if (!condition) throw new Exception("Startup assertion failed.");
}
static Exception Fails(Action action)
{
    try { action(); }
    catch (Exception exception) { return exception; }
    throw new Exception("Expected a failure.");
}
StartupSettings Create(FakeEntry entry) => new(entry, () => path, _ => true);

Check("construction and reading do not enable startup", () =>
{
    var entry = new FakeEntry();
    var startup = Create(entry);
    Require(!startup.Enabled && entry.Writes.Count == 0);
    entry.Command = oldCommand;
    Require(startup.Enabled && entry.Writes.Count == 0);
});
Check("enable quotes the actual executable path and then saves preferences", () =>
{
    var entry = new FakeEntry();
    var saved = false;
    Create(entry).Save(true, () => { Require(entry.Command == expected); saved = true; });
    Require(saved && entry.Writes.SequenceEqual(new[] { expected }));
});
Check("disable removes the entry without needing a valid executable", () =>
{
    var entry = new FakeEntry { Command = oldCommand };
    var startup = new StartupSettings(entry, () => throw new Exception("Must not resolve EXE"), _ => false);
    startup.Save(false, () => { });
    Require(entry.Command is null && entry.Writes.Count == 1);
});
Check("unchanged selection saves preferences without touching registration", () =>
{
    foreach (var enabled in new[] { false, true })
    {
        var entry = new FakeEntry { Command = enabled ? expected : null };
        var saved = false;
        Create(entry).Save(enabled, () => saved = true);
        Require(saved && entry.Writes.Count == 0);
    }
});
Check("old copy is replaced only by explicit Save", () =>
{
    var entry = new FakeEntry { Command = oldCommand };
    var startup = Create(entry);
    Require(startup.Enabled && entry.Command == oldCommand);
    startup.Save(true, () => { });
    Require(entry.Command == expected);
});
Check("failed JSON save restores absent or previous startup entry", () =>
{
    foreach (var original in new[] { null, oldCommand })
    {
        var entry = new FakeEntry { Command = original };
        Require(Fails(() => Create(entry).Save(true, () => throw new IOException("disk full"))) is IOException);
        Require(entry.Command == original && entry.Writes.Count == 2);
    }
});
Check("failed JSON save after disable restores enabled startup", () =>
{
    var entry = new FakeEntry { Command = oldCommand };
    Fails(() => Create(entry).Save(false, () => throw new IOException("disk full")));
    Require(entry.Command == oldCommand);
});
Check("registry write refusal does not save JSON or lose the previous entry", () =>
{
    var entry = new FakeEntry { Command = oldCommand, FailWriteNumber = 1 };
    var saved = false;
    Require(Fails(() => Create(entry).Save(false, () => saved = true)) is UnauthorizedAccessException);
    Require(!saved && entry.Command == oldCommand);
});
Check("registry read refusal prevents any mutation", () =>
{
    var entry = new FakeEntry { FailRead = true };
    var saved = false;
    Fails(() => Create(entry).Save(true, () => saved = true));
    Require(!saved && entry.Writes.Count == 0);
});
Check("failed registration read-back prevents preference save", () =>
{
    var entry = new FakeEntry { IgnoreWrites = true };
    var saved = false;
    Fails(() => Create(entry).Save(true, () => saved = true));
    Require(!saved && entry.Command is null);
});
Check("rollback failure is reported explicitly", () =>
{
    var entry = new FakeEntry { FailWriteNumber = 2 };
    var failure = Fails(() => Create(entry).Save(true, () => throw new IOException("disk full")));
    Require(failure.Message.Contains("could not be restored") && entry.Command == expected);
});
Check("unsafe, missing, relative or host executable paths are refused before writing", () =>
{
    foreach (var invalid in new string?[] { null, "", "MonitorBrightness.exe", @"C:\dotnet.exe",
        "C:\\bad\"folder\\MonitorBrightness.exe", "C:\\bad\nfolder\\MonitorBrightness.exe",
        "C:\\" + new string('x', 250) + "\\MonitorBrightness.exe" })
    {
        var entry = new FakeEntry();
        var startup = new StartupSettings(entry, () => invalid, _ => true);
        Fails(() => startup.Save(true, () => throw new Exception("Must not save")));
        Require(entry.Writes.Count == 0);
    }
    Fails(() => StartupSettings.BuildCommand(path, _ => false));
});
Console.WriteLine($"All {passed} startup checks passed. Fake entry only; no registry or startup settings were changed.");

sealed class FakeEntry : IStartupEntry
{
    internal string? Command;
    internal readonly List<string?> Writes = [];
    internal int FailWriteNumber;
    internal bool FailRead;
    internal bool IgnoreWrites;
    public string? Read() => FailRead ? throw new UnauthorizedAccessException("read denied") : Command;
    public void Write(string? command)
    {
        Writes.Add(command);
        if (Writes.Count == FailWriteNumber) throw new UnauthorizedAccessException("write denied");
        if (!IgnoreWrites) Command = command;
    }
}
