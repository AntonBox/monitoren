namespace MonitorBrightness.Services;

internal sealed record SliderUpdate(long Revision, BrightnessReading? Reading, string? Error);

// One confirmed target. Only Request schedules writes; connect/refresh are read-only.
// All native operations use the shared worker queue, including cleanup.
internal sealed class BrightnessSliderController
{
    private readonly object sync = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly Func<ITestMonitor> openMonitor;
    private readonly Func<CancellationToken, Task> debounce;
    private readonly CancellationTokenSource shutdown = new();
    private ITestMonitor? monitor;
    private BrightnessReading range;
    private int? pending;
    private long revision;
    private bool ready;
    private bool stopping;
    private bool running;
    private Task pump = Task.CompletedTask;

    internal event Action<SliderUpdate>? Updated;
    internal bool Ready { get { lock (sync) return ready && !stopping; } }
    internal long Revision { get { lock (sync) return revision; } }

    internal BrightnessSliderController(Func<ITestMonitor> openMonitor,
        Func<CancellationToken, Task>? debounce = null)
    {
        this.openMonitor = openMonitor;
        this.debounce = debounce ?? (token => Task.Delay(200, token));
    }

    internal async Task<(string Name, BrightnessReading Reading)> ConnectAsync(bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("Confirm the external AOC before enabling brightness control.");
        long version;
        lock (sync)
        {
            if (stopping) throw new InvalidOperationException("Application is stopping.");
            ready = false;
            pending = null;
            version = ++revision;
        }
        await operations.WaitAsync();
        try
        {
            return await MonitorAccess.RunAsync(() =>
            {
                monitor?.Dispose();
                monitor = null;
                lock (sync)
                    if (stopping || version != revision) throw new InvalidOperationException("Connection cancelled after a display change.");
                var opened = openMonitor();
                try
                {
                    var actual = opened.Read();
                    actual.Validate();
                    var name = opened.DisplayName;
                    lock (sync)
                    {
                        if (stopping || version != revision)
                            throw new InvalidOperationException("Connection changed while reading. Confirm the display again.");
                        range = actual;
                        monitor = opened;
                        ready = true;
                    }
                    return (name, actual);
                }
                catch { opened.Dispose(); throw; }
            });
        }
        finally { operations.Release(); }
    }

    internal async Task WaitForIdleAsync()
    {
        while (true)
        {
            Task observed;
            lock (sync) observed = pump;
            await observed;
            lock (sync) if (!running) return;
        }
    }

    internal async Task<BrightnessReading> RefreshAsync()
    {
        // On reopen, finish the latest user request before displaying a fresh read.
        await WaitForIdleAsync();
        await operations.WaitAsync();
        try
        {
            return await MonitorAccess.RunAsync(() =>
            {
                ITestMonitor selected;
                lock (sync)
                {
                    if (!ready || stopping || monitor is null) throw new InvalidOperationException("Confirm and reconnect the external display.");
                    selected = monitor;
                }
                var actual = selected.Read();
                actual.Validate();
                if (actual.Minimum != range.Minimum || actual.Maximum != range.Maximum)
                    throw new InvalidOperationException("Brightness range changed. Confirm and reconnect the display.");
                return actual;
            });
        }
        catch (Exception exception) { Invalidate(exception.Message); throw; }
        finally { operations.Release(); }
    }

    internal void Request(int percent)
    {
        lock (sync)
        {
            if (!ready || stopping) throw new InvalidOperationException("Brightness control is not connected.");
            pending = Math.Clamp(percent, 10, 100);
            revision++;
            if (!running)
            {
                running = true;
                pump = PumpAsync();
            }
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                long version;
                lock (sync)
                {
                    if (!ready || stopping || pending is null) { running = false; return; }
                    version = revision;
                }
                await debounce(shutdown.Token);
                lock (sync)
                    if (version != revision) continue;

                await operations.WaitAsync(shutdown.Token);
                BrightnessReading? result;
                try
                {
                    result = await MonitorAccess.RunAsync<BrightnessReading?>(() =>
                    {
                        ITestMonitor selected;
                        int requested;
                        lock (sync)
                        {
                            // Recheck after waiting for diagnostics or other native work.
                            if (!ready || stopping || pending is null || version != revision || monitor is null) return null;
                            requested = pending.Value;
                            pending = null;
                            selected = monitor;
                        }
                        var current = selected.Read();
                        current.Validate();
                        if (current.Minimum != range.Minimum || current.Maximum != range.Maximum)
                            throw new InvalidOperationException("Brightness range changed. No write sent.");
                        var raw = ToRaw(requested, current.Minimum, current.Maximum);
                        lock (sync)
                            if (!ready || stopping || version != revision) return null;
                        if (raw == current.Current) return current;
                        selected.Write(raw);
                        var actual = selected.Read();
                        actual.Validate();
                        if (actual.Minimum != range.Minimum || actual.Maximum != range.Maximum || actual.Current != raw)
                            throw new InvalidOperationException($"Brightness read-back did not match {requested}%. No retry made; refresh to read the current value.");
                        return actual;
                    });
                }
                finally { operations.Release(); }
                lock (sync)
                {
                    if (result is not null && version == revision && ready && !stopping)
                        Updated?.Invoke(new(revision, result, null));
                }
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            lock (sync) running = false;
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                running = false;
                Invalidate(exception.Message);
            }
        }
    }

    internal void Invalidate(string reason)
    {
        lock (sync)
        {
            ready = false;
            pending = null;
            revision++;
            if (!stopping) Updated?.Invoke(new(revision, null, reason));
        }
    }

    internal async Task DisconnectAsync()
    {
        Invalidate("Control disconnected. Confirm the AOC and read its brightness to reconnect.");
        await operations.WaitAsync();
        try
        {
            await MonitorAccess.RunAsync(() => { monitor?.Dispose(); monitor = null; return true; });
        }
        finally { operations.Release(); }
    }

    internal async Task StopAsync()
    {
        lock (sync)
        {
            stopping = true;
            ready = false;
            pending = null;
            revision++;
            shutdown.Cancel();
        }
        await WaitForIdleAsync();
        await operations.WaitAsync();
        try
        {
            await MonitorAccess.RunAsync(() => { monitor?.Dispose(); monitor = null; return true; });
        }
        finally { operations.Release(); }
    }

    internal static uint ToRaw(int percent, uint minimum, uint maximum)
    {
        if (maximum <= minimum) throw new InvalidOperationException("Invalid brightness range.");
        var clamped = Math.Clamp(percent, 10, 100);
        var range = maximum - minimum;
        var raw = minimum + (uint)decimal.Round(range * (decimal)clamped / 100m, 0, MidpointRounding.AwayFromZero);
        var floor = minimum + (uint)(((ulong)range + 9) / 10);
        return Math.Max(raw, floor);
    }
}
