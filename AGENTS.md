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
