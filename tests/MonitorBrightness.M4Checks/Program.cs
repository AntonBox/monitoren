using System.Threading.Channels;
using MonitorBrightness.Services;

var passed = 0;
async Task Check(string name, Func<Task> action)
{
    await action().WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine($"PASS: {name}");
    passed++;
}
static void Require(bool condition, string message = "M4 assertion failed")
{
    if (!condition) throw new Exception(message);
}
static async Task Refuses(Func<Task> action)
{
    try { await action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Unsafe operation accepted.");
}
static void RefusesRequest(BrightnessSliderController controller, int value)
{
    try { controller.Request(value); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Disconnected slider accepted a request.");
}

await Check("construction, connection and refresh never write", async () =>
{
    var fake = new FakeMonitor();
    var opens = 0;
    var controller = new BrightnessSliderController(() => { opens++; return fake; });
    Require(opens == 0 && fake.Reads == 0);
    await Refuses(() => controller.ConnectAsync(false));
    RefusesRequest(controller, 50);
    var connected = await controller.ConnectAsync(true);
    Require(connected.Reading.Current == 4650 && connected.Name == "Fake AOC");
    fake.Value = 4400;
    Require((await controller.RefreshAsync()).Current == 4400);
    await controller.StopAsync();
    Require(fake.Writes.Count == 0 && opens == 1 && fake.Disposals == 1);
});
await Check("burst is coalesced to the final position", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(20);
    controller.Request(30);
    controller.Request(68);
    Require(fake.Writes.Count == 0);
    await delay.ReleaseNext(); // Superseded first window.
    await delay.ReleaseNext(); // Final stable value.
    await controller.WaitForIdleAsync();
    Require(fake.Writes.SequenceEqual(new uint[] { 4400 }));
    await controller.StopAsync();
});
await Check("latest value wins during a slow in-flight write, with no overlap", async () =>
{
    var fake = new FakeMonitor();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var release = new ManualResetEventSlim();
    fake.BeforeWrite = () => { entered.TrySetResult(); if (!release.Wait(5000)) throw new Exception("Test timed out."); };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    var updates = new List<uint>();
    controller.Updated += value => { if (value.Reading is BrightnessReading reading) updates.Add(reading.Current); };
    await controller.ConnectAsync(true);
    controller.Request(20);
    await delay.ReleaseNext();
    await entered.Task;
    controller.Request(30);
    controller.Request(68);
    release.Set();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.SequenceEqual(new uint[] { 2000, 4400 }));
    Require(updates.SequenceEqual(new uint[] { 4400 }) && fake.MaxConcurrentWrites == 1);
    await controller.StopAsync();
});
await Check("superseded value is skipped after a slow pre-write read", async () =>
{
    var fake = new FakeMonitor();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var release = new ManualResetEventSlim();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    fake.BeforeRead = () => { entered.TrySetResult(); if (!release.Wait(5000)) throw new Exception("Test timed out."); };
    controller.Request(20);
    await delay.ReleaseNext();
    await entered.Task;
    controller.Request(68);
    release.Set();
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.SequenceEqual(new uint[] { 4400 }));
    await controller.StopAsync();
});
await Check("normalization clamps requests, handles offsets, and cannot overflow", () =>
{
    Require(BrightnessSliderController.ToRaw(-100, 1000, 6000) == 1500);
    Require(BrightnessSliderController.ToRaw(101, 1000, 6000) == 6000);
    Require(BrightnessSliderController.ToRaw(73, 1000, 6000) == 4650);
    Require(BrightnessSliderController.ToRaw(50, 20, 121) == 71);
    Require(BrightnessSliderController.ToRaw(10, 20, 121) == 31); // Rounding must not cross below the floor.
    Require(BrightnessSliderController.ToRaw(100, 100, uint.MaxValue) == uint.MaxValue);
    return Refuses(() => Task.FromResult(BrightnessSliderController.ToRaw(50, 10, 10)));
});
await Check("reading below the floor does not change it on connection", async () =>
{
    var fake = new FakeMonitor { Value = 1100 };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    Require((await controller.ConnectAsync(true)).Reading.Percent == 2);
    Require(fake.Writes.Count == 0);
    controller.Request(0);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.SequenceEqual(new uint[] { 1500 }));
    await controller.StopAsync();
});
await Check("unchanged value skips a redundant write", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(73);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.Count == 0);
    await controller.StopAsync();
});
await Check("write failure disables requests and drops pending values without retry", async () =>
{
    var fake = new FakeMonitor { FailWrite = true };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    string? error = null;
    fake.BeforeWrite = () => controller.Request(60); // Newer value must also be dropped when this write fails.
    controller.Updated += update => error = update.Error;
    await controller.ConnectAsync(true);
    controller.Request(68);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(!controller.Ready && error?.Contains("write failed") == true && fake.Writes.Count == 1);
    RefusesRequest(controller, 60);
    await controller.StopAsync();
    Require(fake.Writes.Count == 1);
});
await Check("mismatched read-back disables control and never reports the request as actual", async () =>
{
    var fake = new FakeMonitor { IgnoreWrite = true };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    SliderUpdate? last = null;
    controller.Updated += update => last = update;
    await controller.ConnectAsync(true);
    controller.Request(68);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(!controller.Ready && last?.Reading is null && last?.Error?.Contains("did not match") == true);
    await controller.StopAsync();
});
await Check("failed initial read disposes the target and never enables control", async () =>
{
    var fake = new FakeMonitor { FailRead = true };
    var controller = new BrightnessSliderController(() => fake);
    await Refuses(() => controller.ConnectAsync(true));
    Require(!controller.Ready && fake.Disposals == 1 && fake.Writes.Count == 0);
    await controller.StopAsync();
});
await Check("failed refresh disables control and cannot automatically reconnect", async () =>
{
    var fake = new FakeMonitor();
    var opens = 0;
    var controller = new BrightnessSliderController(() => { opens++; return fake; });
    await controller.ConnectAsync(true);
    fake.FailRead = true;
    await Refuses(() => controller.RefreshAsync());
    Require(!controller.Ready && opens == 1 && fake.Writes.Count == 0);
    await controller.StopAsync();
});
await Check("identity or HDR failure before write drops the queued request", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(68);
    fake.IdentityValid = false;
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(!controller.Ready && fake.Writes.Count == 0);
    await controller.StopAsync();
});
await Check("topology notification cancels pending writes without retargeting", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var opens = 0;
    var controller = new BrightnessSliderController(() => { opens++; return fake; }, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(68);
    controller.Invalidate("Display changed");
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(fake.Writes.Count == 0 && opens == 1 && !controller.Ready);
    await controller.StopAsync();
});
await Check("range change blocks writing", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    fake.Maximum = 7000;
    controller.Request(68);
    await delay.ReleaseNext();
    await controller.WaitForIdleAsync();
    Require(!controller.Ready && fake.Writes.Count == 0);
    await controller.StopAsync();
});
await Check("Exit cancels a debounced value and never restores automatically", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(68);
    await controller.StopAsync();
    Require(fake.Writes.Count == 0 && fake.Disposals == 1);
    RefusesRequest(controller, 60);
});
await Check("Exit waits for an in-flight write but cancels a newer pending position", async () =>
{
    var fake = new FakeMonitor();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var release = new ManualResetEventSlim();
    fake.BeforeWrite = () => { entered.TrySetResult(); if (!release.Wait(5000)) throw new Exception("Test timed out."); };
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(68);
    await delay.ReleaseNext();
    await entered.Task;
    controller.Request(60);
    var stop = controller.StopAsync();
    Require(!stop.IsCompleted);
    release.Set();
    await stop;
    Require(fake.Writes.SequenceEqual(new uint[] { 4400 }) && fake.Disposals == 1);
});
await Check("reopening waits for the requested value then reads it without another write", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    controller.Request(68);
    var refresh = controller.RefreshAsync();
    await delay.ReleaseNext();
    Require((await refresh).Current == 4400 && fake.Writes.Count == 1);
    await controller.StopAsync();
});
await Check("repeated bursts after idle keep working", async () =>
{
    var fake = new FakeMonitor();
    var delay = new DelayGate();
    var controller = new BrightnessSliderController(() => fake, delay.Delay);
    await controller.ConnectAsync(true);
    for (var percent = 20; percent < 40; percent++)
    {
        controller.Request(percent);
        await delay.ReleaseNext();
        await controller.WaitForIdleAsync();
        Require(fake.Value == 1000 + percent * 50);
    }
    Require(fake.Writes.Count == 20);
    await controller.StopAsync();
});

await Check("a display change during connection discards the read and disposes that target", async () =>
{
    var fake = new FakeMonitor();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var release = new ManualResetEventSlim();
    fake.BeforeRead = () => { entered.TrySetResult(); if (!release.Wait(5000)) throw new Exception("Test timed out."); };
    var controller = new BrightnessSliderController(() => fake);
    var connect = controller.ConnectAsync(true);
    await entered.Task;
    controller.Invalidate("Display changed during connection");
    release.Set();
    await Refuses(() => connect);
    Require(!controller.Ready && fake.Writes.Count == 0 && fake.Disposals == 1);
    await controller.StopAsync();
});

Console.WriteLine($"{passed} M4 checks passed. Fake monitor only; no native display APIs or brightness commands executed.");

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
    public string Description => "Fake AOC";
    internal uint Value = 4650;
    internal uint Maximum = 6000;
    internal bool FailRead;
    internal bool FailWrite;
    internal bool IgnoreWrite;
    internal bool IdentityValid = true;
    internal int Reads;
    internal int Disposals;
    internal int MaxConcurrentWrites;
    private int activeWrites;
    internal Action? BeforeRead;
    internal Action? BeforeWrite;
    internal readonly List<uint> Writes = new();
    public void ValidateIdentity()
    {
        if (!IdentityValid) throw new InvalidOperationException("Fake identity/HDR changed.");
        if (Disposals != 0) throw new InvalidOperationException("Fake target disposed.");
    }
    public BrightnessReading Read()
    {
        ValidateIdentity();
        Reads++;
        BeforeRead?.Invoke();
        if (FailRead) throw new InvalidOperationException("Fake read failed.");
        return new(1000, Value, Maximum);
    }
    public void Write(uint value)
    {
        ValidateIdentity();
        MaxConcurrentWrites = Math.Max(MaxConcurrentWrites, Interlocked.Increment(ref activeWrites));
        try
        {
            BeforeWrite?.Invoke();
            Writes.Add(value);
            if (FailWrite) throw new InvalidOperationException("Fake write failed.");
            if (!IgnoreWrite) Value = value;
        }
        finally { Interlocked.Decrement(ref activeWrites); }
    }
    public void Dispose() => Disposals++;
}
