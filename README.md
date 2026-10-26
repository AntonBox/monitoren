# Monitoren — Monitor Brightness

A local Windows tray utility with a Winamp-inspired slider, configurable global
hotkeys, saved monitor selection, and optional startup after sign-in.

**This version adjusts Windows SDR-content brightness while HDR stays enabled.**
It does not change the monitor's hardware backlight, and HDR video/games may not
dim with desktop content. This behavior was explicitly approved and visually
verified on the user's AOC Q27G42XE over HDMI after DDC/CI writes produced no
visible change. The tray has one brightness control, with no automatic fallback.

## Supported setup

- Windows x64; brightness control requires Windows 11 24H2, build 26100 or newer.
- Exactly one active external HDMI display, with HDR enabled and the laptop's
  built-in display disabled. DisplayPort and multiple active displays are not
  supported by the current HDR backend.
- The selected Windows display identity must match the saved selection. A changed
  device path requires explicit reselection in Settings.
- The published executable includes .NET 10; no SDK or separate .NET runtime is
  needed to run it. Building from source requires the .NET 10 SDK.

No administrator privileges, service, installer, network server, telemetry or
account are used. Publishing is local; the binary is not code-signed.

## Run the published application

1. Right-click any older MonitorBrightness tray icon and choose **Exit**.
2. Open `MonitorBrightness\bin\publish\win-x64\MonitorBrightness.exe`, or copy
   that single EXE to a permanent folder and launch it there. Keep its name.
3. Look for the monitor icon in the notification area, including hidden icons.
   Startup opens no main window and changes no brightness.
4. If no monitor is selected, right-click > **Settings...**, choose **Scan displays
   (read-only)**, select your external HDMI monitor, confirm the choice, and Save.
   Existing preferences are reused when their saved display identity matches.
5. Left-click the tray icon for the slider. Opening reads the current value;
   moving the slider explicitly requests a change. X hides the popup; **Exit**
   in the tray menu stops the process and releases its shortcuts.

The percentage is an application scale: 0-100% maps to 80-480 reference-white
nits. It can differ from Windows Settings. The default minimum is 10%; Settings
allows 0-95%. Changing the minimum alone does not change current brightness.

## Shortcuts and settings

Default shortcuts are **Ctrl+Alt+Shift+PageUp** and **Ctrl+Alt+Shift+PageDown**,
with five-point changes. Settings supports a 1-20 point step and different
shortcut combinations. They work with the popup hidden; holding a key does not
repeat. Conflicts are reported in **Hotkeys...** and can be resolved in Settings.

Preferences are stored in `%LOCALAPPDATA%\MonitorBrightness\settings.json`.
Saving preserves the previous file as `settings.json.bak`. Invalid JSON uses safe
defaults and shows a warning. No brightness level is restored automatically.
Settings stores a device-path hash, not raw device paths or monitor serials.

**Start with Windows (when I sign in)** is optional. Check it and Save to register
the currently running EXE for this user; uncheck and Save to remove it. Cancel
does not change startup. After switching from a development build to the published
EXE, open Settings in the published copy and save the checked option again to
update its path. The application never changes the startup path automatically.
If Windows Startup apps has separately disabled it, enable it there too.
See the [startup checks](docs/startup-test.md).

## Troubleshooting and limitations

- **Unavailable display:** verify one active HDMI monitor, HDR on, and the saved
  selection. Reopen the popup or press a shortcut for one new attempt. There are
  no automatic retry loops or silent replacement targets.
- **Internal panel or HDR state changed:** control stops until the supported
  configuration returns. A new monitor identity requires selection in Settings.
- **DDC/CI diagnostics:** the read-only report remains available in the tray menu.
  Copy Diagnostics can be pasted into chat. Capability reads and brightness reads
  can disagree; the AOC returned capability error `0xC0262587` but valid brightness
  reads. DDC writes/read-back did not visibly change brightness with HDR enabled,
  so DDC is not the active slider backend.
- **Windows compatibility:** SDR writes use an undocumented Windows setter.
  Driver/Windows updates can affect it. There is no gamma adjustment, HDR toggle,
  GPU SDK, or automatic DDC fallback.
- **Already running:** one instance is allowed per user/session. Exit the old
  copy before starting an updated executable. Processes are never forcibly killed.
- **Startup or moved files:** startup points to the saved EXE path. Keep that EXE
  there or save startup again from its new location. No startup setting is changed
  by merely launching the application.
- **Single-file runtime:** native runtime components extract under `%TEMP%\.net`
  on first launch. The EXE is large because it includes the Windows desktop runtime.
  See [Microsoft's single-file documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

## Build, test and publish

Run these commands in PowerShell from the repository root:

```powershell
dotnet build -c Release
dotnet run -c Release --project .\tests\MonitorBrightness.M3Checks\MonitorBrightness.M3Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.M4Checks\MonitorBrightness.M4Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.M5Checks\MonitorBrightness.M5Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.M6Checks\MonitorBrightness.M6Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.UIChecks\MonitorBrightness.UIChecks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.StartupChecks\MonitorBrightness.StartupChecks.csproj
.\scripts\Test-NativeDeclarations.ps1
dotnet publish .\MonitorBrightness\MonitorBrightness.csproj -c Release -p:PublishProfile=WinX64
```

The checked-in [WinX64 profile](MonitorBrightness/Properties/PublishProfiles/WinX64.pubxml)
sets `win-x64`, self-contained, single-file, native-library extraction, embedded
symbols and **no trimming**. Output is the single EXE under
`MonitorBrightness\bin\publish\win-x64`. The first publish may download Microsoft
runtime packs from NuGet. Build outputs remain ignored by Git. No files are
uploaded by these commands.

To run source during development:

```powershell
dotnet run --project .\MonitorBrightness\MonitorBrightness.csproj
```

## Release acceptance

The M7 local artifact is built and automated checks pass. **The published EXE's
Windows smoke test is awaiting user confirmation.** Follow the six checks in
[M7 release verification](docs/M7-release-test.md), including launch and Exit.
No Git commit, external release, or upload has been made by the agent.

The [roadmap](ROADMAP.md) records milestone history; [SPEC.md](SPEC.md) and
[AGENTS.md](AGENTS.md) record requirements and approved scope changes.
