# M3: manual desktop brightness test with HDR enabled

M3 is validated and archived. Its menu entry is removed in the M4 build to avoid
competing brightness controls. Use [the M4 slider guide](M4-slider-test.md) for
the current application; the steps below document the earlier M3 build.

The user approved this scope change on 2026-10-22 after DDC/CI read-back changed
45 -> 40 -> 45 without a visible effect. Keep HDR enabled. One manual test and
Restore adjust Windows SDR content brightness on the sole confirmed AOC Q27G42XE
over HDMI. The laptop panel must remain disabled. The tray slider is still DEMO.

## Run and test

1. Exit any older Monitor Brightness instance using its tray menu. Exit Monitorian
   during this test so it does not change the same setting concurrently.
2. In Windows Settings > System > Display, select the AOC, open HDR, and note the
   current **SDR content brightness** slider number for manual recovery. Keep
   **Use HDR** enabled. Do not change brightness yet.
3. From PowerShell in `D:\projects\monitoren`, run:

   ```powershell
   & 'C:\Program Files\dotnet\dotnet.exe' run --no-build --project .\MonitorBrightness\MonitorBrightness.csproj
   ```

4. Right-click the tray icon > **Manual brightness test (M3)**. Verify the title
   says **M3 HDR desktop brightness test** and the instructions mention **5 points**.
   Confirm that the ONLY active display
   is the AOC over HDMI. Click **Test brightness...**. It reads only until the
   approval dialog appears. Review the reported display name, HDR status and
   exact original/proposed values. Choose **No** first; verify no visible change.
5. Click Test again and choose **Yes** only if the target and proposed change are
   correct. This approves one SDR-content brightness write. Observe an ordinary
   SDR window, such as Notepad with a white background, while the test happens.
   Read-back success alone is not visible-effect verification. Record what you see.
6. Click **Restore original...**, review the exact saved value, and approve.
   Confirm the original appearance returns and read-back matches. Confirm HDR is
   still enabled. Select the log, press Ctrl+A then Ctrl+C, and paste it into chat
   along with whether both changes were visible. Close the window to verify the
   tray app remains running; use tray **Exit** to stop it.

One actual Test and one actual Restore attempt are permitted per app session.
Do not repeat a failed test automatically. Closing the test window hides it;
Exit warns if restoration is pending and never writes on shutdown.

## Scale and recovery

- Read-only Windows SDR white level is raw fixed-point: 1000 = 80 reference-white
  nits. These are rendering reference values, not measured panel brightness.
- This test follows Monitorian's conventional 80-480 nit scale (raw 1000-6000).
  It is an application range, not discovered hardware limits. Its percentage may
  differ from the number in Windows Settings. No calibration/max-brightness probe
  or automatic clamping of an unknown reading is performed.
- The latest M3 request restores the maximum change to five percentage points
  (raw 250 / 20 reference-white nits). The temporary 50-point test is no longer
  permitted. It normally dims, or brightens near the 10% floor. For the last
  reported original value, the proposal would be 73% -> 68% (raw 4650 -> 4400),
  followed by Restore to raw 4650. A fresh read always determines the proposal.
  Below the
  floor or outside the scale it stops. The proposal is always shown before approval.
- Restore uses the original raw value, with no percentage rounding. If identity
  or HDR changes, Restore is blocked rather than targeting another display.
  Use Windows Settings > Display > AOC > HDR > SDR content brightness to return
  to the **Windows value you noted**, not the percentage printed by this test.
- Keep the same display connected and do not change HDR during Test/Restore.
  Failed calls report the returned Win32 error and do not silently retry.

## Compatibility and scope

This adaptation requires Windows 11 24H2 (build 26100) or newer, allowing explicit
active-HDR detection rather than confusing wide-color SDR with HDR. It is intended
for the user's one-active-HDMI-display setup. Other configurations fail clearly.
The undocumented SDR setter may fail or change behavior on a Windows/driver update.
It does not toggle HDR or use GPU gamma ramps. HDR videos/games are not uniformly
dimmed by SDR white-level adjustment. There is no separate mode selector.

DisplayConfig identifiers are held in memory, not owned handles, and require no
DestroyPhysicalMonitor call. No device paths or serial numbers are logged. M2
DDC diagnostics still release their own acquired physical monitor handles.

## Interface references

- [Microsoft: QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)
- [Microsoft: SDR white level and units](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_sdr_white_level)
- [Microsoft: DisplayConfigSetDeviceInfo function](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfigsetdeviceinfo)
- [Microsoft SDK wingdi.h: INFO_2 and active HDR enum](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/wingdi.h)
- [Monitorian: undocumented setter packet and flag](https://github.com/emoacht/Monitorian/blob/master/Source/Monitorian.Core/Models/Monitor/DisplayConfig.cs)
- [Monitorian: conventional SDR range](https://github.com/emoacht/Monitorian/issues/705)

Documented Microsoft signatures and SDK structures were checked for reads and
enumeration. The explicitly approved exception is setter request type
`0xFFFFFFEE`, 28-byte packet, raw white level at offset 20, byte flag `1` at offset
24. No other DisplayConfig setter packet is exposed by the application.
