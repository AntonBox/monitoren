namespace MonitorBrightness;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Per-user, per-Windows-session guard. A second launch exits politely;
        // it never terminates or modifies the existing process.
        var user = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Environment.UserName)));
        try
        {
            using var instance = new Mutex(true, @"Local\MonitorBrightness." + user, out var firstInstance);
            if (!firstInstance)
            {
                UI.RetroMessage.Show("Monitor Brightness is already running. Open its icon in the notification area (including hidden icons).",
                    "Monitoren / Already running");
                return;
            }
            try
            {
                using var context = new TrayApplicationContext();
                Application.Run(context);
            }
            finally { instance.ReleaseMutex(); }
        }
        catch (UnauthorizedAccessException exception)
        {
            UI.RetroMessage.Show("Unable to start safely: " + exception.Message, "Monitoren / Startup");
        }
    }
}
