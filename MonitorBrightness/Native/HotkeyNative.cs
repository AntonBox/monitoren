using System.Runtime.InteropServices;
using MonitorBrightness.Services;

namespace MonitorBrightness.Native;

internal sealed class HotkeyNative : IHotkeyApi
{
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);

    public bool Register(nint window, int id, uint modifiers, uint key, out int error)
    {
        var success = RegisterHotKey(window, id, modifiers, key);
        error = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }

    public bool Unregister(nint window, int id, out int error)
    {
        var success = UnregisterHotKey(window, id);
        error = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }
}
