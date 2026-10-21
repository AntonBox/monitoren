# Product spec — Monitor Brightness v0.1

## Problem

On a Windows laptop with an external HDMI monitor, changing brightness through a graphics settings application is too slow. User needs a single-click tray slider and optional global hotkeys.

## Target

- Windows 10/11, x64, .NET 10 WinForms local desktop app (initial deployment target; can later support arm64).
- One or more detected monitors, but **one explicitly selected external physical monitor** controlled at a time in V1.
- Real hardware DDC/CI brightness, not simulated color/gamma correction.

## V1 features

1. Runs in notification area / tray, with no permanently visible taskbar window.
2. Left-click tray icon -> popup with selected monitor's name, slider 0–100%, percentage, and status.
3. Slider sets actual monitor brightness when compatible; read current brightness when opening if possible.
4. Right-click -> Exit (Settings can arrive in later phase).
5. Two global hotkeys: brightness up/down by 5 percentage points, changeable in settings. Suggested defaults: `Ctrl+Alt+Shift+PageUp` and `Ctrl+Alt+Shift+PageDown`. If a combination is unavailable, warn rather than silently failing.
6. Monitor choice and hotkeys persisted in JSON (later milestone).
7. Clean shutdown, no orphan tray icons, no handle leaks, no admin privilege.
8. Graceful unsupported-monitor state. No silent fallback to software dimming.

## Out of scope until requested

- NVIDIA API / color adjustment / gamma ramps, GPU driver manipulation.
- Brightness on internal laptop display, DDC alternatives and macOS/Linux.
- Scheduling, ambient light sensors, profile synchronization, cloud sync, update check, installer, automatic startup, service.
- Rewriting UI in WPF/WinUI, dependencies just for styling.

## Expected UX

- Tray icon: sun or monitor. Left click opens compact popup by notification area.
- One slider and percent readout. `+`/`-` hotkeys work with popup closed.
- Right-click context menu includes Exit; settings added only after core works.
- On error, a short status line and a way to refresh/retry **manually**.
- Prevent rapid bursts of calls while slider is dragged. UI remains responsive.

## Technical behavior to test

### Discovery
- Enumerate Windows logical monitors and related physical monitor handles.
- Display diagnostics with physical monitor descriptions, capability read results, brightness min/current/max, and individual API error codes.
- Avoid guessing that every `HMONITOR` maps one-to-one to a physical device. No writes during discovery.

### Brightness conversion
- Convert UI percent to monitor device range `[min, max]` with rounding and clamp.
- Convert device readings back to UI percent, guard `max <= min`.
- Keep user-visible value truthful on errors; show 'unable to read current brightness' instead of inventing 50%.

### Writes
- Operate only on selected confirmed monitor.
- Handle command failure, disconnects, and stale handles.
- Debounce slider updates and serialize all writes to avoid conflicting commands.
- Hotkeys use the same brightness service and clamping logic as slider.

### Acceptance checklist for finished V1
- [ ] App starts without a main window and appears in tray.
- [ ] Popup opens/closes without exiting.
- [ ] Internal laptop monitor is not accidentally controlled.
- [ ] Selected external monitor name/status is visible.
- [ ] A manual 5% change on HDMI monitor has a visible effect.
- [ ] Close/reopen popup reflects actual brightness or reports reading issue.
- [ ] Both global hotkeys work outside app; conflicts are reported.
- [ ] Unplug/replug monitor does not crash app.
- [ ] Closing popup keeps process running; Exit stops it and clears tray icon.
- [ ] `dotnet build -c Release` completes on Windows.
- [ ] Published `win-x64` application launches on target PC.

## Architecture suggestion (not mandatory file names)

```text
MonitorBrightness/
  Program.cs
  TrayApplicationContext.cs
  UI/BrightnessPopup.cs
  Services/IMonitorBrightnessService.cs
  Services/DdcCiMonitorService.cs
  Services/HotkeyService.cs
  Native/PhysicalMonitorNative.cs
  Native/HotkeyNative.cs
  Settings/AppSettings.cs
```

Keep business logic separable from native I/O so normalization/clamping/settings can be tested without hardware. Avoid forcing complex DI frameworks.
