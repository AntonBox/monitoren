# Agent instructions — Monitoren (Windows)

> Source of truth for any AI coding agent working in this repository. Read `SPEC.md` and `ROADMAP.md` before changing code. If your tool does not automatically load `AGENTS.md`, the user will explicitly ask you to read it.

## Mission

Build a small **local-only Windows tray utility** to control the **hardware brightness of an explicitly selected external HDMI/DisplayPort monitor** using DDC/CI. Provide a slider and configurable system-wide hotkeys. This is a beginner-friendly project: favor incremental, inspectable changes over clever architecture.

## Technology decisions (do not change without asking)

- C# + **.NET 10**, Windows Forms; target `net10.0-windows` and `UseWindowsForms=true`.
- Native Windows monitor APIs via P/Invoke (`Dxva2.dll`, `User32.dll`), not a third-party brightness SDK in MVP.
- `NotifyIcon`, `ContextMenuStrip`, and a small borderless or ordinary `Form` for tray UX.
- `RegisterHotKey` / `WM_HOTKEY` / `UnregisterHotKey` for global hotkeys.
- User settings in JSON at `%LOCALAPPDATA%\MonitorBrightness\settings.json`; store no secrets.
- No background service, network server, telemetry, login, installer, elevated permissions, Electron, or GPU-specific SDK in V1.
- Avoid adding NuGet packages unless necessary; explain and obtain user approval first.

## Workflow you MUST follow

1. Start each task by stating what you will change and how you will verify it (briefly).
2. Work on **only the named milestone** in `ROADMAP.md`. Do not implement later milestones prematurely.
3. Before editing, inspect existing source and `git status`. Do not overwrite unrelated user changes.
4. Keep patches small, straightforward, and easy to review; no broad refactors unless requested.
5. Run `dotnet build` after each milestone (on Windows). Run existing automated tests if available. Show the actual result; never claim tests passed without running them.
6. Do not state that DDC/CI or hotkeys were verified merely because compilation passed. Hardware and Windows tray behavior require **human testing on the user's Windows laptop**.
7. End your response with: files changed, commands run + results, manual Windows test instructions, known issues, and what the next milestone is. Then **stop and wait for approval**.
8. Update `ROADMAP.md` checkboxes only when the milestone's acceptance criteria have actually been met. Record human-only checks as awaiting user confirmation.
9. Do not create a git commit, push, release, or publish externally without the user's approval. Suggest a commit after each validated milestone.
10. In case of uncertainty about Windows API declarations, confirm them in official Microsoft documentation rather than inventing signatures.

## Hardware safety and DDC/CI constraints

### Approved M3 HDR exception (2026-10-22)

The user requires HDR to stay enabled and explicitly approved adapting M3 to
Monitorian-style Windows SDR-content brightness, including its undocumented
DisplayConfig setter. This is the only approved exception to the DDC-only scope.
Use one manual Test/Restore flow on the confirmed sole external HDMI display;
do not add a mode selector or advance to M4. Require fresh per-write approval,
retain the exact original SDR value, and block writes on identity/HDR changes.
Do not toggle HDR, use gamma ramps, or silently fall back to DDC. Windows SDR
content brightness is not hardware backlight control and must be described honestly.
M3 SDR detection currently requires Windows 11 build 26100 or newer; older
systems must fail clearly without changing anything.
The user verified the temporary 50-point SDR test and Restore visually, then
requested M3 again with a maximum five-point change. Both SDR and DDC tests are
now limited to five percentage points, with the same 10% floor and exact Restore.
The earlier permission for a larger test is superseded by this latest request.

### Approved M4 continuation

After confirming the five-point change and Restore, the user authorized M4
("go step 4"). Use the same verified HDR-preserving SDR path for one tray slider.
For M4, confirm the sole external HDMI target once per connection; subsequent
explicit slider movements authorize writes within 10-100% without repeated
approval dialogs. M3's per-write dialogs and five-point test limit remain specific
to its archived manual test. Do not add hotkeys or persisted settings yet. Re-read
on popup reopen, debounce/coalesce slider writes, and require reconfirmation after
an error or display/HDR change. No automatic target replacement or startup writes.

### Approved M5 continuation

After confirming all six M4 checks, the user authorized "Complete only M5".
Add Ctrl+Alt+Shift+PageUp/PageDown for five-point changes through the same SDR
controller and confirmed connection. Explicit key presses authorize these writes,
including while the popup is hidden. Keep HDR on, the 10-100% floor/range,
identity checks, and error invalidation. Report conflicts and unregister on Exit.
Settings, persistence, and configurable shortcut UI remain M6; no automatic writes.

### Approved M6 continuation

After confirming all six M5 checks, the user authorized "Complete only M6".
Provide local JSON preferences for an explicitly chosen external HDMI display,
hotkeys, step size and an optional safety minimum. Keep the single-active-HDMI
restriction and the same verified SDR backend. Save a hash of the Windows device
path, not raw paths/serial numbers, and reject mismatches; a changed path requires
explicit reselection. Saved preferences never authorize startup writes or restore
brightness. Handle invalid JSON and conflicting shortcuts visibly. A per-user/session
single-instance guard may reject a duplicate politely; do not terminate processes.
Do not implement M7 publishing or startup integration.

M6 usability amendment: the user reported M6 working but explicitly requested
removal of repeated confirmation before brightness control. The saved Settings
selection is ongoing authorization for that monitor. Remove the popup checkbox;
opening it reads/connects automatically, and a hotkey can connect/read then apply
that explicit request without first opening the popup. After Settings/errors or
topology changes, validate the same saved identity on the next user action; never
silently select another display. No startup writes or retry loops. Only a new or
changed monitor choice in Settings requires its selection confirmation.

- Phase 1 hardware access is **read-only diagnostics**: enumerate monitors and attempt capability/brightness reads. **Never write brightness before the user authorizes a single test on a selected external monitor.**
- **Never write to all monitors** by default. Do not assume monitor index ordering is stable after hotplug/reboot.
- **Do not change the internal laptop display** in MVP. If uncertain whether a display is external/physical, require explicit selection/confirmation.
- For the first hardware-write test, propose a small reversible change to a user-approved absolute brightness, e.g. 60% -> 55%, and provide a way to restore the previous value. Do not loop until success.
- Respect reported hardware minimum/maximum; normalize values to UI 0–100 with proper math, handle min == max and failed reads. Consider a configurable UI safety floor (default 10%) to avoid an unusably dark screen.
- Do not retry failed DDC commands indefinitely; report the actual Win32 error and a helpful message. Capability detection may be imperfect, so report each API result separately.
- Use the supported physical monitor handles correctly; **always dispose/close them** using `DestroyPhysicalMonitors` or `DestroyPhysicalMonitor` on every success/error path.
- DDC/CI calls can be slow (~40–50 ms or more). Avoid calling them directly in the UI event handler. Serialize hardware requests, debounce/coalesce frequent slider updates (~150–250 ms), and ensure the latest requested value wins.
- Do not restore brightness automatically on application startup unless the user explicitly enables that future feature.
- Keep V1 free of GPU gamma/color adjustments and NVIDIA API integration. If DDC/CI is unsupported, show an explicit unsupported state and stop; propose alternatives only after discussing them with the user.
- If hotkeys conflict, report registration failure and offer different key combinations. Unregister them reliably on shutdown.

## UI and reliability

### Approved M7 continuation

The user authorized proceeding to M7. Build/test Release, publish locally as a
self-contained single-file win-x64 executable without trimming, and document
usage and limitations. Preserve the existing opt-in startup behavior; do not
enable, disable or retarget startup as part of publishing. Human checks of the
published EXE, including tray behavior and Exit, remain required for acceptance.
No Git commit, push, external release or upload is authorized by this milestone.

### Approved optional Windows startup

The user explicitly requested a working Settings option to start the app with
Windows. This supersedes the earlier startup exclusion for this feature only.
Provide opt-in startup after sign-in for the current user through the app's own
HKCU Run value. Only explicit Settings Save may add/update/remove that entry;
opening Settings, loading JSON and launching the app never repair or create it.
Keep startup off when no entry exists. No admin, services, scheduled tasks,
brightness restore or automatic display writes. Do not alter Windows'
StartupApproved overrides. Test with a fake entry; leave real enabling to the
user. The rest of M7 publishing remains separately authorized work.

### Approved pre-M7 visual pass

The user requested a Winamp-inspired slider and matching application styling
before M7. Use code-drawn WinForms styling without new packages: dark panels,
green readouts, gold slider thumb, beveled buttons and striped draggable headers.
Preserve native slider input/accessibility and all existing monitor authorization,
settings and brightness behavior. Fall back to native styling in high contrast.
This authorizes presentation changes only; M7 remains unstarted.

- Use one tray icon; left click toggles popup, right click shows menu with Open, Settings (later milestone), Exit.
- Closing popup does **not** terminate process. Explicit Exit fully disposes `NotifyIcon`, removes hotkeys, and stops the application.
- Handle Explorer/taskbar restart gracefully if practical, but this is not an MVP blocker.
- Display and handle 'no compatible external monitor', disconnected monitor, DDC read failure, and write failure.
- Start with a functional simple UI, not pixel-perfect design.
- Read/save JSON defensively. Do not crash on corrupt settings; back up or ignore invalid settings and use defaults.
- Single-instance behavior is a later milestone; if implemented, do not kill other processes or require admin rights.
- Never perform slow native I/O on the UI thread; marshal UI updates to the WinForms synchronization context.
- Log errors to a local file only when needed, with no personal device identifiers unless necessary; no telemetry.

## Permissions and destructive actions

- Allowed (with ordinary workspace permission): inspect files, modify files under repository, run `dotnet restore/build/test/run`, `git status/diff/log`, and locally inspect diagnostic output.
- **Ask first** before: installing packages/tools, deleting files, running scripts fetched from internet, changing OS/registry/startup/scheduled tasks, using administrator privileges, pushing to GitHub, releasing binaries, or changing monitor hardware brightness.
- Never run `git reset --hard`, `git clean -fdx`, `git push --force`, arbitrary `curl | sh`, `Invoke-Expression` of downloaded code, disable security features, install drivers, or modify unrelated folders.
- External documentation lookup is fine when the user has enabled network access; **no need for unrestricted internet, credentials, browser access, or SSH** to implement V1.

## Acceptance definition

A milestone is only 'done' after: code compiles on Windows, its specific manual checklist passes on the user's laptop (when applicable), no unintended settings have changed, and the user says to continue. If the agent cannot run Windows builds in its environment, state this as **not verified** and give exact commands for the user to run.

## Official references

- WinForms NotifyIcon: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-windows-forms
- Windows DDC/CI brightness: https://learn.microsoft.com/en-us/windows/win32/api/highlevelmonitorconfigurationapi/nf-highlevelmonitorconfigurationapi-getmonitorbrightness
- Windows monitor handles: https://learn.microsoft.com/en-us/windows/win32/api/physicalmonitorenumerationapi/nf-physicalmonitorenumerationapi-getphysicalmonitorsfromhmonitor
- Win32 hotkeys: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
