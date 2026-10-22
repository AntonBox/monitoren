# M4: tray brightness slider

The user verified the HDR-preserving SDR control in M3 and authorized M4.
This version uses one slider for SDR desktop brightness while HDR remains on.
It supports the same sole active external AOC Q27G42XE over HDMI, with the laptop
panel disabled. The program never automatically chooses a replacement target.

## Run

Exit older Monitor Brightness instances and Monitorian before testing, to avoid
competing controllers. Leave HDR enabled. From PowerShell in
`D:\projects\monitoren`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --no-build --project .\MonitorBrightness\MonitorBrightness.csproj
```

## Six manual checks

1. **Startup and confirmation:** startup shows one tray icon and does not change
   brightness. Left-click opens the popup. The slider is disabled until you
   confirm that only the AOC HDMI display is active and click **Read / Refresh**.
   Confirm the reported monitor name, HDMI and HDR-on status, and current
   percentage. Reading must not visibly change brightness. Note the starting value.
2. **Slow adjustment:** move the slider a few points and pause. The picture should
   change; the label first shows the request and then the actual read-back.
   Move it back to the starting value. HDR remains enabled. Each slider movement
   authorizes a brightness request; there is no per-movement approval dialog.
3. **Rapid adjustment:** drag quickly back and forth through a comfortable range,
   then release at a known value. The window remains responsive, and after a brief
   pause the final position matches the actual read-back. Intermediate positions
   may be skipped. Restore the starting position using the slider.
4. **Limits and reopening:** the slider permits 10-100% only. Existing readings
   below 10% are shown truthfully without an automatic increase; moving the slider
   applies its minimum. Close/reopen the popup and verify a fresh read, no reset,
   and no extra tray icon. The popup should fit the screen's working area and be
   readable at your Windows scaling. If other software changes brightness while
   closed, reopening should reflect that value rather than restoring an old one.
5. **Disconnect:** unplug/reconnect the HDMI cable. After the display returns,
   brightness control should be disabled, with an error/status message. It must
   not write to another display or silently reconnect. Confirm the AOC again and
   click **Read / Refresh**; only then try another small adjustment. A failed
   read/write should likewise show the error and disable further queued writes.
6. **Close versus Exit:** closing the popup keeps the tray app running. Tray
   **Exit** removes the icon and stops the app. Pending slider positions are
   cancelled; an already executing native call may finish before Exit completes.
   Relaunching starts unconfirmed and does not restore a remembered brightness.

Send which checks passed, any failed status text, and whether rapid dragging felt
responsive. The status box supports selecting/copying text. Do not infer hardware
success from build/test results alone.

## Limits

- This is SDR-content brightness, not uniform dimming of HDR video/games. The
  80-480 nit reference-white scale is an application convention, not a measured
  panel luminance range. Percentages may differ from Windows Settings.
- Windows 11 build 26100 or newer is required. The SDR setter is undocumented,
  explicitly approved in M3, and could behave differently after an OS/driver update.
- Only one active HDMI display is supported in this milestone. Unknown, embedded,
  disconnected, multiple, or HDR-disabled targets fail clearly without fallback.
- Confirmation is held only in memory. No hotkeys or saved settings are included.
- M3's manual Test/Restore menu is retired. M4 uses explicit slider changes,
  including moving back to the original percentage when desired.

Writes run off the UI thread, share one queue with diagnostics, and wait for a
stable latest request using 200 ms debounce windows. Failed writes are never
automatically retried. No display commands were used by the automated checks.
