# Start with Windows

The Settings checkbox **Start with Windows (when I sign in)** is functional.
It registers this copy of MonitorBrightness for the current user only. It does
not require administrator privileges and does not change brightness at launch.
The default is off when no registration exists. The agent has not enabled it.

## Try it

1. Exit any older MonitorBrightness tray instance. From `D:\projects\monitoren`:

   ```powershell
   & 'C:\Program Files\dotnet\dotnet.exe' run --project .\MonitorBrightness\MonitorBrightness.csproj
   ```

2. Right-click the tray icon, open Settings, check **Start with Windows (when I
   sign in)** and click **Save**. Reopen Settings: the check should remain set.
3. Sign out and sign back in (or restart Windows). One tray icon should appear;
   no popup should open and brightness should remain unchanged. Check a hotkey.
4. Uncheck the option and Save. Sign out/in again: the application should no
   longer launch automatically.
5. Change the checkbox but click Cancel: reopening should show the previous
   saved startup choice.

Keep the EXE and its companion files at their current path. The command above
registers `MonitorBrightness\bin\Debug\net10.0-windows\MonitorBrightness.exe`.
Deleting build output or moving the application breaks that registration. After
moving it, launch the new copy and save the checked option again. No installer
or single-file publication is included in this change.

If you previously disabled MonitorBrightness in Windows Settings > Apps >
Startup (or Task Manager > Startup apps), re-enable it there too. The application
does not override that Windows choice. Windows can delay startup execution.

## Implementation and limits

Windows owns the startup preference; it is not duplicated in JSON. Opening
Settings reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value
`MonitorBrightness`. Save adds a REG_SZ containing the quoted absolute EXE path,
or removes that value when unchecked. No other startup entry is changed. Only
the real `MonitorBrightness.exe` is accepted, not the `dotnet` host, and the
command is checked against Windows' documented 260-character limit.
See [Microsoft's Run key documentation](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys).

Shortcut validation happens before startup/preferences changes. If startup
registration fails, JSON is not saved. If JSON saving fails after registration,
the previous startup entry is restored; any rollback failure is shown explicitly.
Registry read failures prevent Settings from opening and show the error rather
than guessing a checkbox state. Launching or reading JSON never applies startup.

Changed files: `Settings/StartupSettings.cs`, `Settings/WindowsStartupEntry.cs`,
`UI/SettingsForm.cs`, `TrayApplicationContext.cs`, the theme preview helper,
`tests/MonitorBrightness.StartupChecks`, and project documentation.

## Verification

Commands run with `C:\Program Files\dotnet\dotnet.exe` from the repository:

```powershell
dotnet build
dotnet run --project .\tests\MonitorBrightness.StartupChecks\MonitorBrightness.StartupChecks.csproj
dotnet run --project .\tests\MonitorBrightness.M3Checks\MonitorBrightness.M3Checks.csproj
dotnet run --project .\tests\MonitorBrightness.M4Checks\MonitorBrightness.M4Checks.csproj
dotnet run --project .\tests\MonitorBrightness.M5Checks\MonitorBrightness.M5Checks.csproj
dotnet run --project .\tests\MonitorBrightness.M6Checks\MonitorBrightness.M6Checks.csproj
dotnet run --project .\tests\MonitorBrightness.UIChecks\MonitorBrightness.UIChecks.csproj
dotnet run --project .\scripts\ThemePreview\ThemePreview.csproj
git diff --check
```

Build: 0 warnings, 0 errors. Startup checks: 12 passed; existing checks: 113
passed (125 total). Initial startup-test compilation had two CS1009 path-escaping
errors; the fixture was corrected and rerun successfully. Preview rendered five
images; Settings was inspected for layout/clipping. Startup tests link only the
coordinator and a fake entry; they cannot access the registry. No actual startup
setting, global hotkey or monitor brightness was changed by the agent.
Real sign-in behavior remains awaiting the manual checks above.
