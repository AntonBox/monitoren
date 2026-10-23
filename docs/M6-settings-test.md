# M6 — Settings, persistence and recovery

Close older copies through their tray Exit menu, then run from PowerShell:

```powershell
Set-Location D:\projects\monitoren
& 'C:\Program Files\dotnet\dotnet.exe' run --no-build --project .\MonitorBrightness\MonitorBrightness.csproj
```

Right-click the tray icon and choose **Settings...**. Keep HDR enabled and the
laptop panel disabled. The first M6 launch has no saved monitor selection, so
configure it before using the popup. No brightness value is saved or restored.

At your request, the popup confirmation checkbox has been removed. Your saved
monitor selection is sufficient: opening the popup reads/connects automatically,
and a hotkey can connect/read and apply its step without opening the popup first.
The same identity, HDMI and HDR checks remain in force. Startup alone does not
read or change brightness. A new/different choice in Settings still requires
confirmation; editing preferences for the same saved monitor does not.

## Six manual checks

1. **Select and save:** click **Scan displays (read-only)**, choose the detected
   Q27G42XE from the monitor list, and check the external-display confirmation.
   Keep step 5, minimum 10%, and the default Ctrl+Alt+Shift+PageUp/PageDown pair;
   click Save. Scanning/saving must not alter brightness. Open the tray popup;
   it should read automatically, without a checkbox or extra click. Verify slider and
   hotkeys work as in M5. Settings should create
   `%LOCALAPPDATA%\MonitorBrightness\settings.json` only when you Save.
2. **Customize, validate and cancel:** open Settings and set step 3, minimum 20%,
   and Ctrl+Alt+Up / Ctrl+Alt+Down (clear Shift), then Save.
   Shortcuts should immediately work with the popup closed, step by three points, and
   the slider should stop at 20%. Reading a lower current value must not raise
   it automatically. Old shortcuts must stop controlling brightness. Open
   Settings again and temporarily make both shortcuts identical: Save must show
   a validation error. Cancel must retain your previous settings/shortcuts.
   After Cancel, shortcuts should reconnect to the same saved monitor on demand.
3. **Restart persistence:** note your preferences, then Exit and relaunch. The
   selected monitor, step, minimum and shortcuts must still appear in Settings.
   Brightness must remain unchanged at startup. Without opening Settings or the
   popup first, press a shortcut: it should verify/read the saved monitor and
   apply the step. Three quick taps should accumulate correctly even on first use.
4. **Disconnect/reconnect safety:** unplug HDMI and try a shortcut. There must
   be no change to the internal display. Reconnect, disable the laptop panel if
   Windows enabled it. The next shortcut should recheck the saved monitor and work
   without reconfirmation if the identity still matches and HDR remains active.
   If the saved path no longer matches, use Settings -> Scan displays and select
   the intended monitor again. If a different monitor is available for testing,
   connecting it must not silently replace the saved target. That replacement
   case is also covered by an automated fake-display test.
5. **Corrupt-file recovery:** Exit first. Open
   `%LOCALAPPDATA%\MonitorBrightness` in File Explorer and make a separate copy
   of `settings.json` as your test backup. Edit `settings.json` in Notepad to
   contain only `{broken` and save. Relaunch: the app must stay usable, show a
   settings warning, use defaults with no selected monitor, and make no brightness
   changes. Open Settings to read the warning. Exit, then restore your backup's
   contents to `settings.json` and relaunch. Alternatively, explicitly scan/select
   and Save new settings; the invalid original will become `settings.json.bak`.
6. **One instance and clean Exit:** while the app runs, launch the same command
   from another PowerShell window. It must report that Monitor Brightness is
   already running; dismiss that message. There must be only one tray icon and
   one active controller. Exit the original app: the icon disappears and its
   shortcuts no longer change brightness. A fresh launch must work normally.

If a proposed shortcut belongs to another application, Save should report the
exact failed combination and Win32 error. Choose a different pair or Cancel;
previous saved preferences remain intact. Real conflicts depend on other running
apps; automatic tests cover both partial registration cleanup and custom-key
dispatch. A second copy of this app now tests the single-instance guard, not
shortcut conflicts as it did in M5.

Report each check as passed/failed, with any error text. Actual Windows UI,
key registration and visible brightness behavior still need human confirmation.

## Storage and limitations

- Settings are local JSON with schema version, monitor name/path hash, step,
  minimum and shortcuts. No raw device paths, serial numbers, secrets or last
  brightness value are stored. The hash is a local identity token and can change
  after Windows/port/device changes; reselection is then required.
- Saving uses a temporary file in the same directory, then replacement; the
  previous complete file is kept as `settings.json.bak`. A failed save leaves
  the old settings active; errors are visible in the Settings status box.
- Opening Settings pauses control and cancels queued changes. An already-running
  native operation may finish before the dialog opens. Closing Settings does not
  write brightness; the next action reconnects to the saved target. Requests
  waiting on a read are discarded if Settings, Exit or topology changes intervene.
- Shortcuts support Ctrl and/or Alt, optional Shift, and letters, digits,
  F1-F11, arrows, Home/End/PageUp/PageDown. Reserved Win-key and F12 combinations
  are excluded. A held key still produces only one step.
- The extra minimum defaults to 10%; 0 removes that floor. The SDR application
  range stays 80-480 reference-white nits and differs from Windows Settings.
  HDR video/games may not dim. HDR remains on; no DDC fallback is introduced.
- Exactly one active HDMI display is still required. This milestone does not
  introduce multi-display control, auto-start, publishing or an installer.

## Automated verification

Commands executed from `D:\projects\monitoren`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M6Checks\MonitorBrightness.M6Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M5Checks\MonitorBrightness.M5Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M4Checks\MonitorBrightness.M4Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M3Checks\MonitorBrightness.M3Checks.csproj
.\scripts\Test-NativeDeclarations.ps1
git diff --check
```

All commands exited 0. Latest confirmation-removal Debug build: 0 warnings,
0 errors (24.88 seconds). All 103 existing checks were rerun and passed.
Native layout/offset/import checks and `git diff --check` passed. M6: 24 checks passed;
M5: 15 passed; M4: 19 passed; M3: 45 passed (103 total). Tests cover JSON
validation/backup/locked-file failure, custom shortcuts and conflicts, persisted
target matching and invalidation, configurable floor math and queued-work
cancellation. No real display API calls or global key registrations were made.
Tests created only ignored fixtures under `tests/MonitorBrightness.M6Checks/bin`;
the agent did not access the user's actual settings file or launch the tray app.
