# M7 local release verification

Prepared 2026-10-26. The local Windows x64 package is ready; manual acceptance
of the published EXE is pending. No installer or external publication is included.

## Artifact

`D:\projects\monitoren\MonitorBrightness\bin\publish\win-x64\MonitorBrightness.exe`

- Exactly one output file, 116,271,294 bytes (110.88 MiB).
- Windows x64 PE header checked (`MZ`, `PE`, machine `0x8664`).
- Self-contained .NET 10 WinForms; single-file with native runtime extraction.
- No trimming; application symbols embedded. No separate DLL/PDB is required.
- SHA-256: `D937A45D003F7A76A39A2F4B5E66586DFAC113C762698A2D75F1A315A0522005`.
- Unsigned local executable. No upload, signing, installer, or Git commit performed.

The hash identifies this build, not future rebuilds. Publish output is ignored by Git.

## Actual commands and results

Run from `D:\projects\monitoren` in PowerShell. The agent used the full executable
path `C:\Program Files\dotnet\dotnet.exe` for every `dotnet` command below.

```powershell
dotnet build -c Release
dotnet publish .\MonitorBrightness\MonitorBrightness.csproj -c Release -p:PublishProfile=WinX64
dotnet run -c Release --project .\tests\MonitorBrightness.M3Checks\MonitorBrightness.M3Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.M4Checks\MonitorBrightness.M4Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.M5Checks\MonitorBrightness.M5Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.M6Checks\MonitorBrightness.M6Checks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.StartupChecks\MonitorBrightness.StartupChecks.csproj
dotnet run -c Release --project .\tests\MonitorBrightness.UIChecks\MonitorBrightness.UIChecks.csproj
.\scripts\Test-NativeDeclarations.ps1
Get-ChildItem .\MonitorBrightness\bin\publish\win-x64 -File
Get-FileHash .\MonitorBrightness\bin\publish\win-x64\MonitorBrightness.exe -Algorithm SHA256
git diff --check
```

| Check | Actual result |
|---|---|
| Release build | Exit 0, 0 warnings, 0 errors; 2.04 seconds |
| First publish, sandbox | Failed NU1301 / NU1900: NuGet DNS unavailable |
| Same publish with approved network access | Exit 0; runtime packs restored; no warnings/errors reported |
| M3 / M4 / M5 / M6 | 45 / 19 / 15 / 24 passed |
| Startup / UI | 12 / 10 passed |
| Total automated checks | 125 passed |
| Native layout, offsets, import allowlists | Passed; no display APIs invoked |
| Artifact directory and binary header | One EXE, Windows x64 |
| Published EXE launch / tray / Exit | Awaiting user |

The profile additionally sets `IncludeNativeLibrariesForSelfExtract=true` to
avoid loose native DLLs, and `DebugType=embedded` to avoid a separate PDB.
See [Microsoft single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
Runtime extraction uses the user's temporary directory; it does not install .NET.

## Six manual checks on the published EXE

1. **Launch:** right-click any old tray instance and choose Exit. Double-click the
   published EXE above, not a Debug/Release build-folder EXE and not `dotnet run`.
   Verify one tray icon appears, no popup opens automatically, and brightness
   stays unchanged. Launching it again should not create a second tray instance.
2. **Popup and slider:** left-click the icon. Confirm the saved AOC HDMI target
   and current SDR percentage. Drag by about five points and back; verify visible
   change, read-back and keyboard control. Close with X, reopen, and confirm the
   current value is read without another brightness change. Check your DPI layout.
3. **Hotkeys:** focus another application and test both configured shortcuts.
   Verify the configured step/minimum, and that rapid presses settle correctly.
   Settings should retain your monitor selection, shortcuts and minimum on restart.
4. **Reconnect/errors:** disconnect/reconnect HDMI if convenient. Expect a visible
   error and no writes to the internal display. Return to the sole-HDMI/HDR-on
   setup and reopen. It must validate the saved identity; a changed path requires
   reselection. Read-only diagnostics should still produce copyable output.
5. **Optional startup:** if wanted, first place the single EXE in its permanent
   folder and launch that copy. In Settings check Start with Windows and Save.
   Even if already checked from development, Save from the published copy to
   update the registered path. Sign out/in and verify one tray instance, no popup
   and no brightness change. Uncheck/Save to verify disabling. The agent has not
   modified your existing startup registration.
6. **Exit and relaunch:** choose Exit from the tray menu. Verify the icon disappears
   and shortcuts stop acting. Relaunch this published EXE and confirm preferences
   persist and the slider/shortcuts work again.

For the current workspace, the direct manual launch command is:

```powershell
& 'D:\projects\monitoren\MonitorBrightness\bin\publish\win-x64\MonitorBrightness.exe'
```

Report each check as passed/failed (or startup skipped), plus any visible error.
No hardware or real startup checks were performed by the agent. M7 remains
pending this confirmation; a local Git checkpoint can follow separate approval.
