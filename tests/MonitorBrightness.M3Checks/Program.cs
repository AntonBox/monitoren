using MonitorBrightness.Services;
using NativeStub = MonitorBrightness.Native.PhysicalMonitorNative;

var passed = 0;
void Check(string name, Action action)
{
    action();
    Console.WriteLine($"PASS: {name}");
    passed++;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void MustFail(Action action)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected refusal/failure, but the operation succeeded.");
}

Check("45 -> 40 -> 45, original preserved, no write on preparation/disposal", () =>
{
    var fake = new FakeMonitor();
    using (var session = BrightnessTestSession.Prepare(fake, true))
    {
        Require(fake.Writes.Count == 0, "Preparation wrote brightness.");
        Require(session.Target == 40, "Wrong target.");
        Require(session.Test(true).Contains("Requested value confirmed"), "Read-back was not confirmed.");
        Require(session.NeedsRestore, "Restore unavailable after test.");
        session.Restore(true);
        Require(session.Restored, "Restore not confirmed.");
    }
    Require(fake.Writes.SequenceEqual(new uint[] { 40, 45 }), "Unexpected writes.");
    Require(fake.Disposals == 1, "Monitor not released exactly once.");
});

Check("external confirmation required; failed preparation releases monitor", () =>
{
    var fake = new FakeMonitor();
    MustFail(() => BrightnessTestSession.Prepare(fake, false));
    Require(fake.Writes.Count == 0 && fake.Disposals == 1, "Unconfirmed target was used/leaked.");
});

Check("declined approval sends no write", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    MustFail(() => session.Test(false));
    Require(fake.Writes.Count == 0 && !session.TestAttempted, "Declined write attempted.");
});

Check("unexpected starting brightness is refused", () =>
{
    var fake = new FakeMonitor { Reading = new(0, 46, 100) };
    MustFail(() => BrightnessTestSession.Prepare(fake, true));
    Require(fake.Writes.Count == 0 && fake.Disposals == 1, "Unexpected brightness was changed/leaked.");
});

Check("degenerate, inverted, and out-of-range reads are refused", () =>
{
    foreach (var reading in new[] { new BrightnessReading(45, 45, 45), new(10, 9, 100), new(0, 101, 100), new(100, 45, 0) })
    {
        var fake = new FakeMonitor { Reading = reading };
        MustFail(() => BrightnessTestSession.Prepare(fake, true));
        Require(fake.Writes.Count == 0 && fake.Disposals == 1, "Invalid range was used/leaked.");
    }
});

Check("nonzero minimum and large ranges preserve the five-point limit", () =>
{
    foreach (var reading in new[] { new BrightnessReading(20, 65, 120), new(100, 109, 120), new(0, 1800000000, 4000000000) })
    {
        using var session = BrightnessTestSession.Prepare(new FakeMonitor { Reading = reading }, true);
        Require(session.Target >= reading.Minimum && session.Target <= reading.Maximum, "Target outside hardware range.");
        Require(session.Original.Percent - session.TargetPercent is > 0m and <= 5m, "Change exceeded five points.");
        Require(session.TargetPercent >= 10m, "Below safety floor.");
    }
});

Check("preparation read failure releases monitor", () =>
{
    var fake = new FakeMonitor { FailReads = true };
    MustFail(() => BrightnessTestSession.Prepare(fake, true));
    Require(fake.Disposals == 1 && fake.Writes.Count == 0, "Read failure leaked/wrote.");
});

Check("identity change during approval prevents test", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    fake.IdentityValid = false;
    MustFail(() => session.Test(true));
    Require(fake.Writes.Count == 0, "Changed identity received a write.");
});

Check("brightness change during approval prevents test", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    fake.Reading = new(0, 44, 100);
    MustFail(() => session.Test(true));
    Require(fake.Writes.Count == 0, "Stale brightness was overwritten.");
});

Check("duplicate test and restore attempts do not repeat writes", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    session.Test(true);
    MustFail(() => session.Test(true));
    session.Restore(true);
    MustFail(() => session.Restore(true));
    Require(fake.Writes.SequenceEqual(new uint[] { 40, 45 }), "Duplicate write.");
});

Check("write failure retains original; no automatic retry or restore", () =>
{
    var fake = new FakeMonitor { FailWrites = true };
    using var session = BrightnessTestSession.Prepare(fake, true);
    MustFail(() => session.Test(true));
    Require(session.NeedsRestore && session.Original.Current == 45, "Original lost after failure.");
    MustFail(() => session.Test(true));
    Require(fake.Writes.Count == 1, "Failed test retried.");
});

Check("read-back failure retains Restore", () =>
{
    var fake = new FakeMonitor { FailReadAfterWrite = true };
    using var session = BrightnessTestSession.Prepare(fake, true);
    MustFail(() => session.Test(true));
    Require(session.NeedsRestore, "Read-back failure lost Restore.");
    fake.FailReads = false;
    fake.FailReadAfterWrite = false;
    session.Restore(true);
    Require(session.Restored, "Cannot restore after a failed read-back.");
});

Check("mismatched read-back is reported truthfully", () =>
{
    var fake = new FakeMonitor { IgnoreWrites = true };
    using var session = BrightnessTestSession.Prepare(fake, true);
    Require(session.Test(true).Contains("does NOT match"), "Mismatch reported as success.");
    session.Restore(true);
    Require(session.Restored && fake.Writes.Count == 1, "Already-restored monitor received another write.");
});

Check("Restore requires separate approval", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    session.Test(true);
    MustFail(() => session.Restore(false));
    Require(fake.Writes.Count == 1 && session.NeedsRestore, "Unapproved Restore wrote brightness.");
});

Check("Restore refuses changed identity and changed hardware range", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    session.Test(true);
    fake.IdentityValid = false;
    MustFail(() => session.Restore(true));
    fake.IdentityValid = true;
    fake.Reading = new(0, 40, 200);
    MustFail(() => session.Restore(true));
    Require(fake.Writes.Count == 1, "Unsafe Restore was sent.");
});

Check("failed Restore is not silently retried", () =>
{
    var fake = new FakeMonitor();
    using var session = BrightnessTestSession.Prepare(fake, true);
    session.Test(true);
    fake.FailWrites = true;
    MustFail(() => session.Restore(true));
    MustFail(() => session.Restore(true));
    Require(fake.Writes.Count == 2 && session.NeedsRestore, "Restore repeated or failure hidden.");
});

Check("Dispose releases the monitor once and never restores automatically", () =>
{
    var fake = new FakeMonitor();
    var session = BrightnessTestSession.Prepare(fake, true);
    session.Test(true);
    session.Dispose();
    session.Dispose();
    Require(fake.Disposals == 1 && fake.Writes.SequenceEqual(new uint[] { 40 }), "Dispose leaked or wrote brightness.");
});

Check("shared native-work queue serializes requests and recovers from failure", () =>
{
    var active = 0;
    var overlap = false;
    var tasks = Enumerable.Range(0, 8).Select(_ => MonitorAccess.RunAsync(() =>
    {
        if (Interlocked.Increment(ref active) != 1) overlap = true;
        Thread.Sleep(10);
        Interlocked.Decrement(ref active);
        return true;
    })).ToArray();
    Task.WhenAll(tasks).GetAwaiter().GetResult();
    Require(!overlap, "Hardware operations overlapped.");
    MustFail(() => MonitorAccess.RunAsync<bool>(() => throw new InvalidOperationException("simulated failure")).GetAwaiter().GetResult());
    Require(MonitorAccess.RunAsync(() => true).GetAwaiter().GetResult(), "Queue stayed locked after failure.");
});

Check("acquired zero-valued handle survives Test/Restore and is released exactly once", () =>
{
    NativeStub.Reset();
    using (var session = BrightnessTestSession.Prepare(PhysicalTestMonitor.Open(() => false), true))
    {
        session.Test(true);
        session.Restore(true);
        Require(session.Restored, "Zero-valued handle could not be restored.");
    }
    Require(NativeStub.Writes.SequenceEqual(new[] { ((nint)0, 40u), ((nint)0, 45u) }), "Write did not preserve the acquired zero handle.");
    Require(NativeStub.ReadHandles.All(handle => handle == 0), "Read used a different handle.");
    Require(NativeStub.ReleasedHandles.SequenceEqual(new nint[] { 0 }), "Zero-valued handle not released exactly once.");
});

Check("explicit disposal state distinguishes a live zero-valued handle from a closed one", () =>
{
    NativeStub.Reset();
    var monitor = PhysicalTestMonitor.Open(() => false);
    monitor.Read();
    monitor.Dispose();
    monitor.Dispose();
    try
    {
        monitor.Read();
        throw new Exception("Read succeeded after disposal.");
    }
    catch (ObjectDisposedException) { }
    Require(NativeStub.ReleasedHandles.SequenceEqual(new nint[] { 0 }), "Zero-valued handle release was skipped/repeated.");
});

Check("identity change after acquisition releases the zero-valued handle without writing", () =>
{
    NativeStub.Reset();
    MustFail(() => PhysicalTestMonitor.Open(() => NativeStub.Acquired));
    Require(NativeStub.ReleasedHandles.SequenceEqual(new nint[] { 0 }), "Handle leaked after failed identity validation.");
    Require(NativeStub.Writes.Count == 0, "Unexpected write.");
});

Check("failed acquisition does not close an unowned default-zero output slot", () =>
{
    NativeStub.Reset();
    NativeStub.AcquireSucceeds = false;
    try
    {
        PhysicalTestMonitor.Open(() => false);
        throw new Exception("Failed acquisition was accepted.");
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
        Require(exception.NativeErrorCode == 31, "Acquisition error was not preserved.");
    }
    Require(NativeStub.ReleasedHandles.Count == 0 && NativeStub.Writes.Count == 0, "Unowned zero slot was used/released.");
});

Check("nonzero partially returned handle is released after acquisition failure", () =>
{
    NativeStub.Reset();
    NativeStub.ReturnedHandle = 9;
    NativeStub.AcquireSucceeds = false;
    try
    {
        PhysicalTestMonitor.Open(() => false);
        throw new Exception("Failed acquisition was accepted.");
    }
    catch (System.ComponentModel.Win32Exception) { }
    Require(NativeStub.ReleasedHandles.SequenceEqual(new nint[] { 9 }), "Partial output was not released.");
});

Check("diagnostics reads/releases zero-valued handle despite capability failure", () =>
{
    NativeStub.Reset();
    NativeStub.FailCapabilities = true;
    var result = MonitorDiagnostics.Collect(CancellationToken.None);
    Require(result.Report.Contains("GetMonitorBrightness: OK; min=0, current=45, max=100"), "Diagnostic brightness read was skipped.");
    Require(result.Report.Contains("0xC0262587"), "Capability failure was hidden.");
    Require(NativeStub.ReleasedHandles.SequenceEqual(new nint[] { 0 }), "Diagnostic zero handle was not released.");
    Require(NativeStub.Writes.Count == 0, "Read-only diagnostics wrote brightness.");
});

Check("diagnostics releases zero-valued handle after a failed brightness read", () =>
{
    NativeStub.Reset();
    NativeStub.FailBrightnessRead = true;
    var result = MonitorDiagnostics.Collect(CancellationToken.None);
    Require(result.Report.Contains("GetMonitorBrightness: FAILED; Win32=31"), "Read failure not reported.");
    Require(NativeStub.ReleasedHandles.SequenceEqual(new nint[] { 0 }), "Handle leaked after read failure.");
});

Check("diagnostics does not read/release a default-zero slot after acquisition failure", () =>
{
    NativeStub.Reset();
    NativeStub.AcquireSucceeds = false;
    var result = MonitorDiagnostics.Collect(CancellationToken.None);
    Require(result.Report.Contains("GetPhysicalMonitorsFromHMONITOR: FAILED"), "Acquisition failure not reported.");
    Require(NativeStub.ReadHandles.Count == 0 && NativeStub.ReleasedHandles.Count == 0, "Unowned default slot was used/released.");
});

passed += SdrChecks.Run();
Console.WriteLine($"{passed} checks passed. Fake monitors only; no native APIs or display brightness writes executed.");

sealed class FakeMonitor : ITestMonitor
{
    public string Description => "Fake confirmed external monitor";
    internal BrightnessReading Reading = new(0, 45, 100);
    internal bool IdentityValid = true;
    internal bool FailReads;
    internal bool FailWrites;
    internal bool FailReadAfterWrite;
    internal bool IgnoreWrites;
    internal int Disposals;
    internal readonly List<uint> Writes = new();

    public void ValidateIdentity()
    {
        if (!IdentityValid) throw new InvalidOperationException("Simulated identity change.");
    }

    public BrightnessReading Read()
    {
        if (FailReads) throw new InvalidOperationException("Simulated read failure.");
        return Reading;
    }

    public void Write(uint value)
    {
        Writes.Add(value);
        if (FailWrites) throw new InvalidOperationException("Simulated write failure.");
        if (!IgnoreWrites) Reading = Reading with { Current = value };
        if (FailReadAfterWrite) FailReads = true;
    }

    public void Dispose() => Disposals++;
}
