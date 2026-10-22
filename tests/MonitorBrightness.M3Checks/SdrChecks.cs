using System.ComponentModel;
using System.Runtime.InteropServices;
using MonitorBrightness.Services;
using N = MonitorBrightness.Native.DisplayConfigNative;

internal static class SdrChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(string name, Action action)
        {
            action();
            Console.WriteLine($"PASS: SDR {name}");
            passed++;
        }
        static void Require(bool condition)
        {
            if (!condition) throw new Exception("SDR safety check failed.");
        }
        static void Refuses(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new Exception("Unsafe SDR operation was accepted.");
        }
        static BrightnessTestSession Prepare(FakeSdrApi api, Func<bool>? changed = null) =>
            BrightnessTestSession.Prepare(SdrTestMonitor.Open(api, changed ?? (() => false)), true, null, 5);

        Check("fresh value used instead of DDC 45%; five-point change and exact Restore", () =>
        {
            var api = new FakeSdrApi { Value = 3377 };
            using var session = Prepare(api);
            Require(api.Writes.Count == 0 && session.Original.Current == 3377 && session.Target == 3127);
            session.Test(true);
            session.Restore(true);
            Require(session.Restored && api.Writes.Select(x => x.Value).SequenceEqual(new uint[] { 3127, 3377 }));
            Require(api.Writes.All(x => x.Target == api.Display.Target));
            Require(!session.Description.Contains(api.Display.DevicePath));
        });
        Check("no write when confirmation or either approval is declined", () =>
        {
            var api = new FakeSdrApi();
            Refuses(() => BrightnessTestSession.Prepare(SdrTestMonitor.Open(api, () => false), false, null));
            using var session = Prepare(api);
            Refuses(() => session.Test(false));
            Require(api.Writes.Count == 0);
            session.Test(true);
            Refuses(() => session.Restore(false));
            Require(api.Writes.Count == 1);
        });
        Check("no targets, multiple targets, and duplicate/clone paths are refused", () =>
        {
            foreach (var count in new[] { 0, 2, 3 })
            {
                var api = new FakeSdrApi { Count = count };
                Refuses(() => Prepare(api));
                Require(api.Reads == 0 && api.Writes.Count == 0);
            }
        });
        Check("internal, embedded, DP, unknown and unavailable paths are refused", () =>
        {
            foreach (var connector in new uint[] { 0x80000000, 6, 11, 10, 0xFFFFFFFF })
            {
                var api = new FakeSdrApi();
                api.Display = api.Display with { OutputTechnology = connector };
                Refuses(() => Prepare(api));
                Require(api.Reads == 0 && api.Writes.Count == 0);
            }
            var unavailable = new FakeSdrApi();
            unavailable.Display = unavailable.Display with { Available = false };
            Refuses(() => Prepare(unavailable));
        });
        Check("missing identity or inactive HDR is refused", () =>
        {
            var baseline = new FakeSdrApi().Display;
            foreach (var display in new[] { baseline with { DevicePath = "" }, baseline with { DisplayName = "" }, baseline with { HdrActive = false } })
            {
                var api = new FakeSdrApi { Display = display };
                Refuses(() => Prepare(api));
                Require(api.Writes.Count == 0);
            }
        });
        Check("any target/source/path replacement blocks Test without retargeting", () =>
        {
            var baseline = new FakeSdrApi().Display;
            foreach (var display in new[]
            {
                baseline with { Target = baseline.Target with { AdapterLow = 99 } },
                baseline with { Target = baseline.Target with { AdapterHigh = 9 } },
                baseline with { Target = baseline.Target with { Id = 8 } },
                baseline with { Source = baseline.Source with { Id = 44 } },
                baseline with { DevicePath = "different monitor" }
            })
            {
                var api = new FakeSdrApi();
                using var session = Prepare(api);
                api.Display = display;
                Refuses(() => session.Test(true));
                Require(api.Writes.Count == 0);
            }
        });
        Check("HDR off during approval permanently invalidates the retained session", () =>
        {
            var api = new FakeSdrApi();
            using var session = Prepare(api);
            api.Display = api.Display with { HdrActive = false };
            Refuses(() => session.Test(true));
            api.Display = api.Display with { HdrActive = true };
            Refuses(() => session.Test(true));
            Require(api.Writes.Count == 0);
        });
        Check("topology notification blocks Restore and disposal never writes", () =>
        {
            var api = new FakeSdrApi();
            var changed = false;
            using (var session = Prepare(api, () => changed))
            {
                session.Test(true);
                changed = true;
                Refuses(() => session.Restore(true));
                Require(session.NeedsRestore);
            }
            Require(api.Writes.Count == 1);
        });
        Check("HDR off after Test blocks Restore", () =>
        {
            var api = new FakeSdrApi();
            using var session = Prepare(api);
            session.Test(true);
            api.Display = api.Display with { HdrActive = false };
            Refuses(() => session.Restore(true));
            Require(api.Writes.Count == 1);
        });
        Check("changed SDR value during approval is not overwritten", () =>
        {
            var api = new FakeSdrApi();
            using var session = Prepare(api);
            api.Value++;
            Refuses(() => session.Test(true));
            Require(api.Writes.Count == 0);
        });
        Check("out-of-scale readings and values below safety floor are refused", () =>
        {
            foreach (var value in new uint[] { 0, 999, 1000, 1499, 6001, uint.MaxValue })
            {
                var api = new FakeSdrApi { Value = value };
                Refuses(() => Prepare(api));
                Require(api.Writes.Count == 0);
            }
        });
        Check("five-point proposal respects both limits and restores every boundary exactly", () =>
        {
            var cases = new (uint Original, uint Target)[]
            {
                (1500, 1750), (1501, 1751), (1749, 1999), (1750, 1500),
                (1751, 1501), (4650, 4400), (5999, 5749), (6000, 5750)
            };
            foreach (var (value, target) in cases)
            {
                var api = new FakeSdrApi { Value = value };
                using var session = Prepare(api);
                Require(Math.Abs(session.TargetPercent - session.Original.Percent) == 5m);
                Require(session.Target == target);
                Require(session.TargetPercent is >= 10m and <= 100m);
                session.Test(true);
                session.Restore(true);
                Require(api.Value == value);
            }
        });
        Check("steps above five points are refused before reads or writes for SDR and DDC", () =>
        {
            foreach (var step in new uint[] { 0, 6, 20, 50, uint.MaxValue })
            {
                var api = new FakeSdrApi();
                Refuses(() => BrightnessTestSession.Prepare(SdrTestMonitor.Open(api, () => false), true, null, step));
                Require(api.Reads == 0 && api.Writes.Count == 0);
            }
            var ddc = new FakeMonitor();
            Refuses(() => BrightnessTestSession.Prepare(ddc, true, 45m, 20));
            Require(ddc.Writes.Count == 0 && ddc.Disposals == 1);
        });
        Check("read failure prevents a write and reports the actual Win32 error", () =>
        {
            var api = new FakeSdrApi { ReadError = 50 };
            try { Prepare(api); throw new Exception("Failed read accepted."); }
            catch (Win32Exception ex) { Require(ex.NativeErrorCode == 50 && ex.Message.Contains("0x00000032")); }
            Require(api.Writes.Count == 0);
        });
        Check("write failure retains Restore and never retries", () =>
        {
            var api = new FakeSdrApi { WriteError = 87 };
            using var session = Prepare(api);
            try { session.Test(true); throw new Exception("Failed write accepted."); }
            catch (Win32Exception ex) { Require(ex.NativeErrorCode == 87); }
            Refuses(() => session.Test(true));
            Require(session.NeedsRestore && api.Writes.Count == 1);
            api.WriteError = 0;
            session.Restore(true);
            Require(session.Restored && api.Writes.Count == 1); // Already original; no second write.
        });
        Check("mismatched read-back never claims success and Restore retains exact original", () =>
        {
            var api = new FakeSdrApi { IgnoreWrites = true };
            using var session = Prepare(api);
            Require(session.Test(true).Contains("does NOT match"));
            session.Restore(true);
            Require(session.Restored);
        });
        Check("read-back failure permits one explicitly approved Restore", () =>
        {
            var api = new FakeSdrApi { FailReadAfterWrite = true };
            using var session = Prepare(api);
            try { session.Test(true); throw new Exception("Read-back failure hidden."); }
            catch (Win32Exception) { }
            Require(session.NeedsRestore);
            api.ReadError = 0;
            api.FailReadAfterWrite = false;
            session.Restore(true);
            Require(session.Restored && api.Writes.Count == 2);
        });
        Check("disposed identity cannot be reused", () =>
        {
            var api = new FakeSdrApi();
            var monitor = SdrTestMonitor.Open(api, () => false);
            monitor.Dispose();
            monitor.Dispose();
            try { monitor.Write(2000); throw new Exception("Disposed monitor accepted write."); }
            catch (ObjectDisposedException) { }
            Require(api.Writes.Count == 0);
        });
        Check("setter packet preserves adapter/target, raw units, type, size and flag", () =>
        {
            var target = new DisplayTargetId(123, -45, 67);
            var packet = WindowsSdrDisplayApi.CreateWritePacket(target, 3377);
            Require(packet.Header.Type == 0xFFFFFFEE && packet.Header.Size == 28 && packet.Flag == 1);
            Require(packet.Header.Adapter.Low == 123 && packet.Header.Adapter.High == -45 && packet.Header.Id == 67);
            Require(packet.Value == 3377 && Marshal.OffsetOf<N.SetWhiteLevel>("Value").ToInt32() == 20
                && Marshal.OffsetOf<N.SetWhiteLevel>("Flag").ToInt32() == 24);
            Refuses(() => WindowsSdrDisplayApi.CreateWritePacket(target, 999));
            Refuses(() => WindowsSdrDisplayApi.CreateWritePacket(target, 6001));
        });
        return passed;
    }
}

internal sealed class FakeSdrApi : ISdrDisplayApi
{
    internal SdrDisplay Display = new(new(1, 2, 3), new(1, 2, 4), "private-device-path",
        @"\\.\DISPLAY4", "AOC Q27G42XE", 5, true, true);
    internal uint Value = 3000;
    internal int Count = 1;
    internal int Reads;
    internal int ReadError;
    internal int WriteError;
    internal bool IgnoreWrites;
    internal bool FailReadAfterWrite;
    internal readonly List<(DisplayTargetId Target, uint Value)> Writes = new();

    public IReadOnlyList<SdrDisplay> GetActiveDisplays() => Enumerable.Repeat(Display, Count).ToArray();
    public uint ReadWhiteLevel(DisplayTargetId target)
    {
        if (target != Display.Target) throw new Exception("Wrong SDR read target.");
        Reads++;
        WindowsSdrDisplayApi.Check(ReadError, "Simulated SDR read");
        return Value;
    }
    public void WriteWhiteLevel(DisplayTargetId target, uint value)
    {
        Writes.Add((target, value));
        WindowsSdrDisplayApi.Check(WriteError, "Simulated SDR write");
        if (!IgnoreWrites) Value = value;
        if (FailReadAfterWrite) ReadError = 31;
    }
}
