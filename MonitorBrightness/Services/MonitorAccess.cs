namespace MonitorBrightness.Services;

// Diagnostics, the slider and the manual test share one queue, including cleanup.
internal static class MonitorAccess
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<T> RunAsync<T>(Func<T> action)
    {
        await Gate.WaitAsync();
        try
        {
            return await Task.Run(action);
        }
        finally
        {
            Gate.Release();
        }
    }
}
