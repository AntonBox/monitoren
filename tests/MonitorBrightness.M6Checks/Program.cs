using System.Text.Json;
using System.Threading.Channels;
using MonitorBrightness.Services;
using MonitorBrightness.Settings;

var passed = 0;
var root = Path.Combine(AppContext.BaseDirectory, "TestResults", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
async Task Check(string name, Func<Task> action)
{
    await action().WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine($"PASS: {name}");
    passed++;
}
static void Require(bool condition)
{
    if (!condition) throw new Exception("M6 assertion failed.");
}
static void Refuses(Action action)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Unsafe configuration accepted.");
}
string NewPath() => Path.Combine(root, Guid.NewGuid().ToString("N"), "settings.json");
var selectedDisplay = FakeApi.Display;
var configured = new AppSettings
{
    Monitor = new(SdrTestMonitor.Fingerprint(selectedDisplay), "Q27G42XE"),
    StepPercent = 3, SafetyMinimum = 20, Increase = new(3, 0x26), Decrease = new(3, 0x28)
};

await Check("missing settings give safe defaults without creating files", () =>
{
    var path = NewPath();
    var loaded = new SettingsStore(path).Load();
    Require(loaded.Warning is null && loaded.Settings == new AppSettings() && !File.Exists(path));
    Require(loaded.Settings.Monitor is null);
    return Task.CompletedTask;
});
await Check("round trip retains preferences, not paths, serials or brightness", () =>
{
    var path = NewPath();
    var store = new SettingsStore(path);
    store.Save(configured);
    var loaded = store.Load();
    Require(loaded.Warning is null && loaded.Settings == configured);
    var text = File.ReadAllText(path);
    Require(!text.Contains(selectedDisplay.DevicePath) && !text.Contains("Brightness") && !text.Contains("Current"));
    Require(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").Any());
    return Task.CompletedTask;
});
await Check("replacement keeps a backup of the previous complete document", () =>
{
    var path = NewPath();
    var store = new SettingsStore(path);
    store.Save(configured);
    store.Save(configured with { StepPercent = 7 });
    Require(store.Load().Settings.StepPercent == 7);
    Require(new SettingsStore(path + ".bak").Load().Settings == configured);
    return Task.CompletedTask;
});
foreach (var invalid in new[]
{
    "{broken", "null", "[]", "{\"Version\":2}", "{\"StepPercent\":0}",
    "{\"SafetyMinimum\":100}", "{\"Increase\":null}", "{\"Surprise\":true}",
    "{\"Monitor\":{\"Fingerprint\":\"bad\",\"Name\":\"AOC\"}}"
})
{
    await Check($"invalid JSON/settings are ignored and preserved: {invalid}", () =>
    {
        var path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, invalid);
        var store = new SettingsStore(path);
        var loaded = store.Load();
        Require(loaded.Warning is not null && loaded.Settings == new AppSettings());
        Require(File.ReadAllText(path) == invalid);
        store.Save(configured);
        Require(File.ReadAllText(path + ".bak") == invalid && store.Load().Settings == configured);
        return Task.CompletedTask;
    });
}
await Check("oversized and locked settings fail closed without a crash", () =>
{
    var path = NewPath();
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, new string(' ', 65537));
    Require(new SettingsStore(path).Load().Warning is not null);
    using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Require(new SettingsStore(path).Load().Warning is not null);
    return Task.CompletedTask;
});
await Check("failed save preserves the prior file and cleans its temporary file", () =>
{
    var path = NewPath();
    var store = new SettingsStore(path);
    store.Save(configured);
    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        var failed = false;
        try { store.Save(configured with { StepPercent = 8 }); }
        catch (IOException) { failed = true; }
        Require(failed);
    }
    Require(store.Load().Settings == configured);
    Require(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").Any());
    return Task.CompletedTask;
});
await Check("invalid edits cannot overwrite existing settings", () =>
{
    var path = NewPath();
    var store = new SettingsStore(path);
    store.Save(configured);
    foreach (var candidate in new[]
    {
        configured with { StepPercent = 21 }, configured with { SafetyMinimum = -1 },
        configured with { Increase = configured.Decrease }, configured with { Increase = new(4, 0x41) },
        configured with { Increase = new(8, 0x41) }, configured with { Increase = new(3, 0x7B) },
        configured with { Monitor = new(new string('A', 64), "bad\nname") }
    }) Refuses(() => store.Save(candidate));
    Require(store.Load().Settings == configured);
    return Task.CompletedTask;
});
await Check("custom shortcuts route the configured step and reject old combinations", () =>
{
    var api = new FakeHotkeys();
    var keys = new GlobalHotkeys(api, 123);
    keys.Start(configured);
    Require(keys.Enabled && api.Calls.SequenceEqual(new[] { (1, 0x4003u, 0x26u), (2, 0x4003u, 0x28u) }));
    Require(keys.StepForMessage(1, (nint)((0x26 << 16) | 3)) == 3);
    Require(keys.StepForMessage(2, (nint)((0x28 << 16) | 3)) == -3);
    Require(keys.StepForMessage(1, (nint)((0x21 << 16) | 7)) == 0);
    Require(keys.Status.Contains("20-100%"));
    keys.Stop();
    return Task.CompletedTask;
});
await Check("custom shortcut conflict rolls back the successfully registered half", () =>
{
    var api = new FakeHotkeys { FailSecond = true };
    var keys = new GlobalHotkeys(api, 123);
    keys.Start(configured);
    Require(!keys.Enabled && keys.Status.Contains("Ctrl+Alt+Down") && keys.Status.Contains("1409"));
    Require(api.Released.SequenceEqual(new[] { 1 }));
    Require(keys.StepForMessage(1, (nint)((0x26 << 16) | 3)) == 0);
    return Task.CompletedTask;
});
await Check("a saved selection opens only its matching target; reads never write", () =>
{
    var api = new FakeApi();
    using var selected = SdrTestMonitor.Open(api, () => false, configured.Monitor!.Fingerprint);
    Require(selected.Read().Current == 4650 && api.Writes.Count == 0);
    return Task.CompletedTask;
});
await Check("a replacement HDMI target is rejected before brightness access", () =>
{
    var api = new FakeApi { Current = selectedDisplay with { DevicePath = "different-monitor" } };
    Refuses(() => SdrTestMonitor.Open(api, () => false, configured.Monitor!.Fingerprint));
    Require(api.Reads == 0 && api.Writes.Count == 0);
    return Task.CompletedTask;
});
await Check("internal or HDR-off targets cannot use even a matching saved hash", () =>
{
    foreach (var display in new[] { selectedDisplay with { OutputTechnology = 0x80000000 }, selectedDisplay with { HdrActive = false } })
    {
        var api = new FakeApi { Current = display };
        Refuses(() => SdrTestMonitor.Open(api, () => false, SdrTestMonitor.Fingerprint(display)));
        Require(api.Reads == 0 && api.Writes.Count == 0);
    }
    return Task.CompletedTask;
});
await Check("identity and topology changes invalidate an existing saved connection", () =>
{
    var api = new FakeApi();
    using var selected = SdrTestMonitor.Open(api, () => false, configured.Monitor!.Fingerprint);
    api.Current = selectedDisplay with { DevicePath = "replacement" };
    Refuses(() => selected.Write(4400));
    api.Current = selectedDisplay;
    Refuses(() => selected.Write(4400)); // Reappearance does not revive an invalid session.
    Require(api.Writes.Count == 0);
    return Task.CompletedTask;
});
await Check("custom minimum never clamps a read or writes on settings application", async () =>
{
    var fake = new FakeApi { Value = 1500 }; // 10%, below the configured 20% floor.
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => SdrTestMonitor.Open(fake, () => false), delay.Delay);
    controller.SetSafetyMinimum(20);
    Require(fake.Reads == 0 && fake.Writes.Count == 0);
    Require((await controller.ConnectAsync(true)).Reading.Percent == 10);
    Require(fake.Writes.Count == 0);
    Require(controller.RequestStep(3) == 20);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 2000);
    await controller.StopAsync();
});
await Check("changing settings cancels pending work and requires a fresh connection", async () =>
{
    var fake = new FakeApi();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => SdrTestMonitor.Open(fake, () => false), delay.Delay);
    await controller.ConnectAsync(true);
    Refuses(() => controller.SetSafetyMinimum(20));
    controller.RequestStep(-5);
    await controller.DisconnectAsync();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    controller.SetSafetyMinimum(20);
    Refuses(() => controller.RequestStep(3));
    Require(fake.Writes.Count == 0);
    await controller.ConnectAsync(true);
    Require(controller.RequestStep(-3) == 70);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 4500);
    await controller.StopAsync();
});
await Check("configurable floor math honors zero, coarse ranges and large raw values", () =>
{
    Require(BrightnessSliderController.ToRaw(0, 1000, 6000, 0) == 1000);
    Require(BrightnessSliderController.ToRaw(0, 0, 3, 20) == 1);
    Require(BrightnessSliderController.ToRaw(0, 0, uint.MaxValue, 95) == 4080218931);
    Require(BrightnessSliderController.ToRaw(100, 100, uint.MaxValue, 95) == uint.MaxValue);
    Refuses(() => BrightnessSliderController.ToRaw(50, 1000, 6000, 100));
    return Task.CompletedTask;
});
Console.WriteLine($"All {passed} M6 checks passed. Fake APIs only; fixtures are in {root}");

sealed class FakeHotkeys : IHotkeyApi
{
    internal bool FailSecond;
    internal readonly List<(int, uint, uint)> Calls = [];
    internal readonly List<int> Released = [];
    public bool Register(nint window, int id, uint modifiers, uint key, out int error)
    {
        Calls.Add((id, modifiers, key));
        error = FailSecond && id == 2 ? 1409 : 0;
        return error == 0;
    }
    public bool Unregister(nint window, int id, out int error) { Released.Add(id); error = 0; return true; }
}
sealed class FakeApi : ISdrDisplayApi
{
    internal static readonly SdrDisplay Display = new(new(1, 2, 3), new(1, 2, 4), "fake-private-device-path",
        "DISPLAY4", "Q27G42XE", 5, true, true);
    internal SdrDisplay Current = Display;
    internal uint Value = 4650;
    internal int Reads;
    internal readonly List<uint> Writes = [];
    public IReadOnlyList<SdrDisplay> GetActiveDisplays() => [Current];
    public uint ReadWhiteLevel(DisplayTargetId target) { Reads++; return Value; }
    public void WriteWhiteLevel(DisplayTargetId target, uint value) { Writes.Add(value); Value = value; }
}
sealed class DelayGate
{
    private readonly Channel<TaskCompletionSource> waits = Channel.CreateUnbounded<TaskCompletionSource>();
    internal Task Delay(CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        waits.Writer.TryWrite(completion);
        return completion.Task.WaitAsync(token);
    }
    internal async Task ReleaseNext()
    {
        var completion = await waits.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        completion.TrySetResult();
    }
}
