namespace MonitorBrightness.Settings;

internal interface IStartupEntry
{
    string? Read();
    void Write(string? command);
}

// Windows owns this preference; JSON cannot silently enable it at app launch.
// Mutations happen only as part of the user's Settings Save action.
internal sealed class StartupSettings(IStartupEntry entry, Func<string?> executablePath, Func<string, bool> fileExists)
{
    internal bool Enabled => entry.Read() is not null;

    internal void Save(bool enabled, Action savePreferences)
    {
        var previous = entry.Read();
        var desired = enabled ? BuildCommand(executablePath(), fileExists) : null;
        if (previous == desired)
        {
            savePreferences();
            return;
        }
        try
        {
            entry.Write(desired);
            if (entry.Read() != desired) throw new IOException("Windows did not retain the startup preference.");
            savePreferences();
        }
        catch (Exception failure)
        {
            try
            {
                if (entry.Read() != previous) entry.Write(previous);
                if (entry.Read() != previous) throw new IOException("Startup preference could not be restored.");
            }
            catch (Exception rollback)
            {
                throw new InvalidOperationException("Saving failed and the previous startup preference could not be restored. "
                    + "Reopen Settings and check Start with Windows. " + failure.Message + " " + rollback.Message, failure);
            }
            throw;
        }
    }

    internal static string BuildCommand(string? path, Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
            || path.Contains('"') || path.Any(char.IsControl)
            || !string.Equals(Path.GetFileName(path), "MonitorBrightness.exe", StringComparison.OrdinalIgnoreCase)
            || !exists(path))
            throw new InvalidOperationException("Start with Windows needs the MonitorBrightness.exe application. Launch that EXE and save again.");
        var command = "\"" + path + "\"";
        if (command.Length > 260)
            throw new InvalidOperationException("The application path is too long for Windows startup. Move the app to a shorter permanent path first.");
        return command;
    }
}
