# M5 — Global brightness hotkeys

Close any older copy using its tray menu's Exit. From PowerShell:

```powershell
Set-Location D:\projects\monitoren
& 'C:\Program Files\dotnet\dotnet.exe' run --no-build --project .\MonitorBrightness\MonitorBrightness.csproj
```

This runs the freshly built Debug application. Keep Windows HDR enabled and the
laptop panel disabled, as in the verified M4 configuration. Shortcuts are fixed
for M5; no preferences are saved.

| Shortcut | Action |
| --- | --- |
| Ctrl+Alt+Shift+PageUp | Increase by 5 percentage points |
| Ctrl+Alt+Shift+PageDown | Decrease by 5 percentage points |

Tap and release PageUp/PageDown for each step. Holding a key does not repeat.
The app uses the same 10-100% SDR content scale as the slider, not the monitor's
backlight or Windows Settings percentage. HDR video/games may not dim.

## Six manual checks

1. **Startup and confirmation:** before confirming anything, press both shortcuts.
   Brightness must stay unchanged. Open the tray popup; check the AOC confirmation
   and click Read / Refresh. Reading must not change brightness. The popup should
   show the shortcuts, and the tray's Hotkeys... item should show registration
   details. A shortcut conflict must be reported, never silently ignored.
2. **Other app focused:** note the current percentage (use a value between 20 and
   90 for this test). Close the popup with X, focus Notepad or a browser, and tap
   PageDown with Ctrl+Alt+Shift held. Wait about a second, then reopen the popup:
   expect five points lower and a visible SDR brightness change, with HDR on.
   Hide it again and use PageUp once to return to the starting value. The popup
   must not steal focus when a shortcut succeeds.
3. **Rapid input:** with the popup hidden and brightness away from the limits,
   tap the decrease shortcut three times quickly. Wait and reopen: expect 15
   points lower, not just five. Three increase taps should restore that value.
   With the popup visible, also mix a slider movement and a shortcut: the last
   intent should win and the UI must stay responsive.
4. **Hold and limits:** hold a shortcut for two seconds: expect one five-point
   step, not continuous changes. Use the slider to set 10%, then press decrease:
   it must stay at 10%. At 100%, increase must stay at 100%. Return to your chosen
   comfortable value using the slider. If an externally set current value is
   below 10%, reading preserves it; the first explicit request clamps to 10%.
5. **Disconnect:** unplug HDMI while the app runs and press a shortcut. It must
   not change the internal display or select a different target. Reconnect HDMI;
   shortcuts must remain blocked until you confirm the AOC and Read / Refresh
   again. If Windows enabled the laptop screen, disable it manually before
   reconfirming; multiple active displays are deliberately rejected.
6. **Conflict and Exit:** while the first copy runs, launch the same command in a
   second PowerShell window. Do not confirm a target in this second copy. Its
   popup should say Hotkeys unavailable; Hotkeys... should identify the failed
   shortcut and Win32 error (usually 1409). Exit the second copy, then the first.
   With both closed, shortcuts must no longer change brightness. Relaunch a single
   copy: shortcuts should register again, but must not write before confirmation.
   Exit it when finished. No automatic restore occurs on Exit.

## Error handling and scope

A registration conflict disables both hotkeys but leaves the slider available.
Release the combination in the competing app or exit the other copy, then
restart Monitor Brightness. If those assignments must be kept, an alternative
pair such as Ctrl+Alt+Shift+Up/Down can be requested; configurable shortcuts are
M6. There is no automatic registration retry or monitor reconnection.

Hotkeys are ignored during a read/reconnect; wait for it to finish and tap again.
Outside brightness changes are picked up when the popup reads again; otherwise
hotkeys step from the last confirmed value or the latest queued request.
Display/HDR changes and write/read-back errors disable both slider and hotkey
control until reconfirmation. Registration notifications can be hidden by Windows
notification preferences; the popup label and Hotkeys... menu remain available.

The agent's automated checks use fake APIs and cannot prove global key delivery
or the visible brightness effect. The user subsequently confirmed all six manual
checks passed. M5 is validated; this checklist remains available for regression
testing. M6 requires separate authorization.

## API references

Declarations and message fields follow Microsoft's documentation:
[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
[WM_HOTKEY](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey), and
[UnregisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey).

## Commands and actual automated results

Run from `D:\projects\monitoren` on Windows:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M5Checks\MonitorBrightness.M5Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M4Checks\MonitorBrightness.M4Checks.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\tests\MonitorBrightness.M3Checks\MonitorBrightness.M3Checks.csproj
.\scripts\Test-NativeDeclarations.ps1
git diff --check
```

Actual results on 2026-10-22: all commands exited 0. Final Debug build succeeded
with 0 warnings and 0 errors (3.38 seconds). M5: 15 checks passed; M4: 19 passed;
M3: 45 passed. The native-declaration script passed layout, field-offset, and
import allowlist checks. `git diff --check` produced no errors. No display commands
were sent, no application was launched, and no Git commit was created by the agent.
