using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using MonitorBrightness.Native;
using MonitorBrightness.Services;

var passed = 0;
async Task Check(string name, Func<Task> action)
{
    await action().WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine($"PASS: {name}");
    passed++;
}
static void Require(bool value)
{
    if (!value) throw new Exception("M5 assertion failed.");
}
static void RefusesStep(BrightnessSliderController controller)
{
    try { controller.RequestStep(5); }
    catch (InvalidOperationException) { return; }
    throw new Exception("An unconfirmed or invalid target accepted a hotkey.");
}
static nint KeyData(uint key) => (nint)((key << 16) | GlobalHotkeys.Modifiers);

await Check("native BOOL, HWND, UINT and last-error declarations", () =>
{
    var methods = typeof(HotkeyNative).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
    foreach (var name in new[] { "RegisterHotKey", "UnregisterHotKey" })
    {
        var method = methods.Single(m => m.Name == name);
        var import = method.GetCustomAttribute<DllImportAttribute>()!;
        Require(import.Value == "user32.dll" && import.ExactSpelling && import.SetLastError);
        Require(method.ReturnType == typeof(bool));
        Require(method.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.Bool);
        var expected = name == "RegisterHotKey"
            ? new[] { typeof(nint), typeof(int), typeof(uint), typeof(uint) }
            : new[] { typeof(nint), typeof(int) };
        Require(method.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected));
    }
    return Task.CompletedTask;
});
await Check("default registrations use one window and suppress held-key repeat", () =>
{
    var api = new FakeHotkeys();
    var keys = new GlobalHotkeys(api, 123);
    keys.Start();
    keys.Start();
    Require(keys.Enabled && api.Registrations.SequenceEqual(new[]
    {
        ((nint)123, 1, 0x4007u, 0x21u), ((nint)123, 2, 0x4007u, 0x22u)
    }));
    keys.Stop();
    keys.Stop();
    Require(api.Releases.SequenceEqual(new[] { ((nint)123, 1), ((nint)123, 2) }));
    return Task.CompletedTask;
});
await Check("messages route only registered IDs and exact keys; stopped messages do nothing", () =>
{
    var keys = new GlobalHotkeys(new FakeHotkeys(), 123);
    Require(keys.StepForMessage(1, KeyData(0x21)) == 0);
    keys.Start();
    Require(keys.StepForMessage(1, KeyData(0x21)) == 5);
    Require(keys.StepForMessage(2, KeyData(0x22)) == -5);
    Require(keys.StepForMessage(3, KeyData(0x21)) == 0);
    Require(keys.StepForMessage(1, KeyData(0x22)) == 0);
    Require(keys.StepForMessage(1, (nint)(0x21 << 16)) == 0);
    keys.Stop();
    Require(keys.StepForMessage(1, KeyData(0x21)) == 0);
    return Task.CompletedTask;
});
foreach (var failedId in new[] { 1, 2 })
{
    await Check($"conflict on hotkey {failedId} disables both and rolls back acquired registrations", () =>
    {
        var api = new FakeHotkeys { FailedId = failedId };
        var keys = new GlobalHotkeys(api, 123);
        keys.Start();
        Require(!keys.Enabled && keys.Status.Contains("1409") && keys.Status.Contains("0x00000581"));
        Require(keys.Status.Contains(failedId == 1 ? "PageUp" : "PageDown"));
        Require(keys.Status.Contains("slider still works") && api.Releases.Count == failedId - 1);
        Require(keys.StepForMessage(1, KeyData(0x21)) == 0);
        keys.Stop();
        Require(api.Releases.Count == failedId - 1);
        return Task.CompletedTask;
    });
}
await Check("cleanup reports errors and still attempts both registrations", () =>
{
    var api = new FakeHotkeys { FailRelease = true };
    var keys = new GlobalHotkeys(api, 123);
    keys.Start();
    var error = keys.Stop();
    Require(!keys.Enabled && api.Releases.Count == 2 && error.Contains("UnregisterHotKey(1)")
        && error.Contains("UnregisterHotKey(2)") && error.Contains("Win32=5"));
    return Task.CompletedTask;
});
await Check("startup, connection and registration do not write; unconfirmed hotkeys are refused", async () =>
{
    var fake = new FakeMonitor();
    var controller = new BrightnessSliderController(() => fake);
    var keys = new GlobalHotkeys(new FakeHotkeys(), 123);
    keys.Start();
    RefusesStep(controller);
    Require(fake.Reads == 0 && fake.Writes.Count == 0);
    await controller.ConnectAsync(true);
    Require(fake.Writes.Count == 0);
    await controller.StopAsync();
    RefusesStep(controller);
    keys.Stop();
});
await Check("fractional current brightness moves exactly five points in both directions", async () =>
{
    var fake = new FakeMonitor { Value = 4670 }; // 73.4%, not rounded to 73%.
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    Require(controller.RequestStep(-5) == 68.4m);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 4420);
    Require(controller.RequestStep(5) == 73.4m);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 4670);
    await controller.StopAsync();
});
await Check("rapid hotkeys accumulate from latest slider intent and coalesce", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(50);
    Require(controller.RequestStep(5) == 55);
    Require(controller.RequestStep(5) == 60);
    Require(controller.RequestStep(-5) == 55);
    await delay.ReleaseNext();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.SequenceEqual(new uint[] { 3750 }));
    await controller.StopAsync();
});
await Check("hotkeys during an in-flight write retain all steps; later slider wins", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var release = new ManualResetEventSlim();
    fake.BeforeWrite = () => { entered.TrySetResult(); if (!release.Wait(5000)) throw new Exception("Test timed out."); };
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.RequestStep(-5); // 68
    await delay.ReleaseNext();
    await entered.Task;
    Require(controller.RequestStep(-5) == 63);
    Require(controller.RequestStep(-5) == 58);
    release.Set();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.SequenceEqual(new uint[] { 4400, 3900 }));
    controller.RequestStep(5);
    controller.Request(42);
    await delay.ReleaseNext();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 3100);
    await controller.StopAsync();
});
await Check("hotkey bounds clamp without overshoot and reverse immediately", async () =>
{
    var fake = new FakeMonitor { Value = 6000 };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    Require(controller.RequestStep(5) == 100);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.Count == 0);
    Require(controller.RequestStep(-5) == 95);
    controller.Request(10);
    Require(controller.RequestStep(-5) == 10);
    Require(controller.RequestStep(5) == 15);
    await delay.ReleaseNext();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 1750);
    await controller.StopAsync();
});
await Check("read-only refresh resets the hotkey base to actual brightness", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    fake.Value = 3000; // An outside change, observed on popup reopen.
    await controller.RefreshAsync();
    Require(fake.Writes.Count == 0 && controller.RequestStep(5) == 45);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Value == 3250);
    await controller.StopAsync();
});
await Check("display invalidation drops queued hotkeys and never reconnects", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var opens = 0;
    var controller = new BrightnessSliderController(() => { opens++; return fake; }, delay.Delay);
    await controller.ConnectAsync(true);
    controller.RequestStep(-5);
    controller.Invalidate("HDMI disconnected");
    RefusesStep(controller);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.Count == 0 && opens == 1);
    await controller.StopAsync();
});
await Check("failed hotkey write does not retry or retain newer steps", async () =>
{
    var fake = new FakeMonitor { FailWrite = true };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    fake.BeforeWrite = () => controller.RequestStep(-5);
    controller.RequestStep(-5);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(!controller.Ready && fake.Writes.Count == 1);
    RefusesStep(controller);
    await controller.StopAsync();
});
await Check("Exit unregisters keys and cancels queued brightness work", async () =>
{
    var fake = new FakeMonitor();
    var api = new FakeHotkeys();
    var keys = new GlobalHotkeys(api, 123);
    var controller = new BrightnessSliderController(() => fake, new DelayGate().Delay);
    keys.Start();
    await controller.ConnectAsync(true);
    controller.RequestStep(-5);
    keys.Stop();
    await controller.StopAsync();
    Require(fake.Writes.Count == 0 && api.Releases.Count == 2);
    Require(keys.StepForMessage(2, KeyData(0x22)) == 0);
});
Console.WriteLine($"All {passed} M5 checks passed. No Windows hotkeys or native display APIs were invoked.");

sealed class FakeHotkeys : IHotkeyApi
{
    internal int FailedId;
    internal bool FailRelease;
    internal readonly List<(nint, int, uint, uint)> Registrations = [];
    internal readonly List<(nint, int)> Releases = [];
    public bool Register(nint window, int id, uint modifiers, uint key, out int error)
    {
        Registrations.Add((window, id, modifiers, key));
        error = id == FailedId ? 1409 : 0;
        return error == 0;
    }
    public bool Unregister(nint window, int id, out int error)
    {
        Releases.Add((window, id));
        error = FailRelease ? 5 : 0;
        return error == 0;
    }
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
sealed class FakeMonitor : ITestMonitor
{
    public string Description => "Fake confirmed external monitor";
    internal uint Value = 4650;
    internal int Reads;
    internal bool FailWrite;
    internal Action? BeforeWrite;
    internal readonly List<uint> Writes = [];
    public void ValidateIdentity() { }
    public BrightnessReading Read() { Reads++; return new(1000, Value, 6000); }
    public void Write(uint value)
    {
        BeforeWrite?.Invoke();
        Writes.Add(value);
        if (FailWrite) throw new InvalidOperationException("Fake write failed");
        Value = value;
    }
    public void Dispose() { }
}
