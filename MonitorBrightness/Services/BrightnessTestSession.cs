namespace MonitorBrightness.Services;

internal readonly record struct BrightnessReading(uint Minimum, uint Current, uint Maximum)
{
    internal void Validate()
    {
        if (Maximum <= Minimum || Current < Minimum || Current > Maximum)
        {
            throw new InvalidOperationException("Invalid brightness range; no write is allowed.");
        }
    }

    internal decimal Percent => 100m * (Current - Minimum) / (Maximum - Minimum);
}

// A small boundary lets the safeguards be tested using a fake monitor, without native I/O.
internal interface ITestMonitor : IDisposable
{
    string Description { get; }
    string DisplayName => Description;
    void ValidateIdentity();
    BrightnessReading Read();
    void Write(uint value);
}

internal sealed class BrightnessTestSession : IDisposable
{
    private readonly ITestMonitor monitor;
    private bool disposed;
    internal BrightnessReading Original { get; }
    internal uint Target { get; }
    internal decimal TargetPercent => 100m * (Target - Original.Minimum) / (Original.Maximum - Original.Minimum);
    internal string Description => monitor.Description;
    internal bool TestAttempted { get; private set; }
    internal bool RestoreAttempted { get; private set; }
    internal bool Restored { get; private set; }
    internal bool NeedsRestore => TestAttempted && !Restored;

    // Takes ownership even if preparation fails. This method only reads.
    internal static BrightnessTestSession Prepare(ITestMonitor monitor, bool externalConfirmed,
        decimal? expectedStartingPercent = 45m, uint changePercentagePoints = 5)
    {
        try
        {
            if (!externalConfirmed)
            {
                throw new InvalidOperationException("Explicit confirmation of the external monitor is required.");
            }

            if (changePercentagePoints is 0 or > 5)
                throw new InvalidOperationException("Invalid test step. M3 permits at most five percentage points.");

            monitor.ValidateIdentity();
            var original = monitor.Read();
            original.Validate();
            if (expectedStartingPercent is decimal expected && original.Percent != expected)
            {
                throw new InvalidOperationException($"Expected the approved starting brightness of {expected}%, but read {original.Percent:F1}%. No write made; share fresh diagnostics before a different test.");
            }

            // Round DOWN and multiply in 64 bits so the requested limit is never exceeded.
            var step = (uint)((ulong)(original.Maximum - original.Minimum) * changePercentagePoints / 100);
            if (step == 0)
            {
                throw new InvalidOperationException($"The brightness range is too coarse for a change of at most {changePercentagePoints} percentage points.");
            }

            // SDR starts from its fresh read, not the unrelated DDC value of 45%.
            // Choose the direction with more room when the requested reduction cannot fit.
            if (original.Percent < 10m)
                throw new InvalidOperationException("Current brightness is below the 10% test safety floor. No write made.");
            uint target;
            if (expectedStartingPercent is null)
            {
                var floor = original.Minimum + (uint)(((ulong)(original.Maximum - original.Minimum) + 9) / 10);
                var downward = Math.Min(step, original.Current - floor);
                var upward = Math.Min(step, original.Maximum - original.Current);
                target = downward >= upward ? original.Current - downward : original.Current + upward;
            }
            else
            {
                target = original.Current - step;
            }
            if (target < original.Minimum || target > original.Maximum
                || 100m * (target - original.Minimum) / (original.Maximum - original.Minimum) < 10m)
            {
                throw new InvalidOperationException("The proposed change is outside the safe brightness range.");
            }

            return new BrightnessTestSession(monitor, original, target);
        }
        catch
        {
            monitor.Dispose();
            throw;
        }
    }

    private BrightnessTestSession(ITestMonitor monitor, BrightnessReading original, uint target)
    {
        this.monitor = monitor;
        Original = original;
        Target = target;
    }

    internal string Test(bool approved)
    {
        CheckApproval(approved);
        if (TestAttempted)
        {
            throw new InvalidOperationException("The single test was already attempted. No retry was made.");
        }

        monitor.ValidateIdentity();
        var current = monitor.Read();
        if (current != Original)
        {
            throw new InvalidOperationException("Brightness or its range changed after approval was prepared. No write made.");
        }

        monitor.ValidateIdentity();
        TestAttempted = true; // Retain the original even if the driver reports a write failure.
        monitor.Write(Target);
        return Verify(Target, "Test");
    }

    internal string Restore(bool approved)
    {
        CheckApproval(approved);
        if (!TestAttempted || RestoreAttempted || Restored)
        {
            throw new InvalidOperationException("Restore is unavailable or was already attempted. No retry was made.");
        }

        monitor.ValidateIdentity();
        var current = monitor.Read();
        current.Validate();
        if (current.Minimum != Original.Minimum || current.Maximum != Original.Maximum)
        {
            throw new InvalidOperationException("The brightness range changed. Restore blocked; use the original control's settings.");
        }

        if (current.Current == Original.Current)
        {
            Restored = true;
            return $"Restore: already at the original value {Original.Current} ({Original.Percent:F1}%); no write needed.";
        }

        monitor.ValidateIdentity();
        RestoreAttempted = true;
        monitor.Write(Original.Current);
        var result = Verify(Original.Current, "Restore");
        return result;
    }

    private string Verify(uint requested, string operation)
    {
        monitor.ValidateIdentity();
        var actual = monitor.Read();
        actual.Validate();
        var matches = actual.Minimum == Original.Minimum && actual.Maximum == Original.Maximum && actual.Current == requested;
        if (operation == "Restore")
        {
            Restored = matches;
        }

        return $"{operation}: write returned success. Read-back min={actual.Minimum}, current={actual.Current}, max={actual.Maximum} ({actual.Percent:F1}%). "
            + (matches ? "Requested value confirmed by read-back. Confirm the visible effect yourself."
                : "Read-back does NOT match the request. No retry made; restore manually if needed.");
    }

    private void CheckApproval(bool approved)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!approved)
        {
            throw new InvalidOperationException("Write not approved. No brightness command sent.");
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            monitor.Dispose();
        }
    }
}
