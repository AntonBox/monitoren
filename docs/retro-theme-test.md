# Winamp-inspired visual pass

User-requested styling before M7. This changes presentation, not the HDR-preserving
SDR controller, saved monitor authorization, brightness limits or hotkey behavior.

Changed presentation files: `UI/RetroTheme.cs`, `UI/RetroTrackBar.cs`,
`UI/RetroMessage.cs`, popup designer/readout text, Settings, Diagnostics,
the archived M3 test form, tray context and startup informational dialogs.
The shared theme uses original code-drawn assets without packages.

## Commands and verification

Run from `D:\projects\monitoren` in PowerShell:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M3Checks\MonitorBrightness.M3Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M4Checks\MonitorBrightness.M4Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M5Checks\MonitorBrightness.M5Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M6Checks\MonitorBrightness.M6Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.UIChecks\MonitorBrightness.UIChecks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\scripts\ThemePreview\ThemePreview.csproj
git diff --check
```

Final Debug build succeeded with 0 warnings and 0 errors (3.62 seconds).
Automated checks passed: M3 45, M4 19, M5 15, M6 24 (103 total).
Preview rendering (including tray icon creation) and `git diff --check` exited 0.
The preview command renders hidden controls with sample data to the ignored
`scripts/ThemePreview/bin/previews` folder. It never shows the tray application,
reads a monitor, registers global shortcuts or saves real preferences. It checks
custom-draw native layout as well. Combo selected text is replayed through the
real owner-draw handler because native WM_PRINT omits that region.

## Manual Windows checks (awaiting user)

Exit the previous tray app first, then run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run -c Release --project .\MonitorBrightness\MonitorBrightness.csproj
```

1. Open the tray popup. Check the green readout and gold slider, with no Refresh
   button, permanent status box or desktop/HDR/range description.
   Opening should read the saved display without changing brightness.
2. Drag the slider slowly, then quickly. Check visible SDR changes, final read-back,
   and the configured minimum. Focus the slider and check arrow-key control.
3. Drag the striped header. Close with X; the app must remain in the tray.
   Check both configured hotkeys with another application focused.
4. Open Settings. Check dropdowns, numbers, checkbox focus, Save and Cancel.
   Previously saved preferences should remain intact unless you explicitly Save.
5. Open diagnostics and Hotkeys information. Check readable text and Copy output;
   diagnostics remain read-only. Resize diagnostics using its bottom-right grip.
6. Check layout at your normal Windows scale (and 150% if convenient), including
   reopening near screen edges. Exit should remove the tray icon and shortcuts.

Windows notifications, scrollbar and numeric-arrow rendering retain system styling.
High contrast at launch uses native controls/window chrome. Actual DPI transitions,
tray interaction and hardware effects require the manual checks above.
No M7 publish/startup work or Git commit is part of this visual pass.

## Slider input regression fix

The user reported that hotkeys worked but slider movements did not change
brightness. A hidden-control regression reproduced the issue before the fix:
`Scroll 5: expected one change to 65, got []` (exit 1).

The custom channel painting read `TrackBar.Value`. That getter updates the
framework's cached value; when native repaint precedes the scroll notification,
WinForms sees no remaining change and omits `ValueChanged`. See the official
[WinForms TrackBar source](https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms/System/Windows/Forms/Controls/TrackBar/TrackBar.cs).
The skin now caches its paint value in `OnValueChanged` and invalidates the
control for redraw. Painting no longer reads `Value` or modifies input state.

After the fix, all 10 UI checks passed (exit 0): native baseline, repaint before
drag/end-track/arrow/page/home/end input, and programmatic updates without extra
events. The checks create hidden TrackBars only and dispatch messages to those
test controls; they do not show windows, move the real cursor, send global input,
load preferences or contact a monitor. All 103 M3-M6 checks also passed.
Debug build succeeded with 0 warnings/errors. Repeat manual checks 1-3 above
after exiting the old tray process and launching the updated application.

## Compact popup follow-up

Removed Refresh, the routine success/status section and the desktop/HDR/range
description at the user's request. The normal popup is shorter. Opening still
reads the saved display; errors and below-minimum warnings alone expand a notice.
Reopen the popup or use a shortcut for a manual retry after an error.

Commands actually run for this follow-up:

- `dotnet build`: failed with MSB3027/MSB3021, 10 warnings and 2 errors, because
  the running app locked the Debug EXE. Debug preview build hit the same lock.
- `dotnet build -c Release`: passed, 0 warnings/errors (23.45 seconds).
- The M3-M6 `dotnet run --project` commands above: all 103 checks passed.
- `dotnet run -c Release --project .\tests\MonitorBrightness.UIChecks\MonitorBrightness.UIChecks.csproj`:
  all 10 UI checks passed.
- `dotnet run -c Release --project .\scripts\ThemePreview\ThemePreview.csproj`:
  five previews rendered, including the normal and error popup. Both inspected.

Use the Release launch command above after exiting the running app. Check that
opening reads the current percentage, slider/hotkeys still work, and closing and
reopening refreshes the value. Live behavior still awaits user confirmation.
