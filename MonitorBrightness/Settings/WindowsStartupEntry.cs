using Microsoft.Win32;

namespace MonitorBrightness.Settings;

internal sealed class WindowsStartupEntry : IStartupEntry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MonitorBrightness";

    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return null;
        if (value is not string command || key!.GetValueKind(ValueName) != RegistryValueKind.String)
            throw new InvalidOperationException("The MonitorBrightness startup entry has an unexpected format. It has not been changed.");
        return command;
    }

    public void Write(string? command)
    {
        if (command is null)
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
    }
}
