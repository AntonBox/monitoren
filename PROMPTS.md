# Copy-and-Paste Prompts for Your Coding Agent

**Rule:** send the next prompt only after you have tested the previous milestone on Windows. Codex reads `AGENTS.md` when starting a local project session; also direct it to `SPEC.md` and `ROADMAP.md`. See `CODEX_SETUP.md` for Codex configuration.

## Prompt 0 — Understand the project (no code changes)

> Read AGENTS.md, SPEC.md, ROADMAP.md, GETTING_STARTED.md, and CODEX_SETUP.md. This is my first Windows application built with an AI coding agent. Do not modify anything yet. Briefly explain the development plan, give me commands to check Windows/.NET 10 SDK/Git, identify our current milestone, and list actions that will require my explicit approval. Do not request administrator privileges, install anything, or send commands to my monitor.

## Prompt 1 — M0: Initial project and build

> Complete **only M0** in ROADMAP.md. Create a minimal C# WinForms application targeting .NET 10 (`net10.0-windows`), a solution file, and a local Git repository if one does not exist. Do not implement a tray icon, DDC/CI, or hotkeys. Respect the existing .gitignore and .editorconfig. Run `dotnet build` and report the exact commands and actual results. Give me clear steps to run the application manually on Windows, then stop. Do not make a Git commit without my approval.

## Prompt 2 — M1: Tray shell with a mock slider

> Complete **only M1**. Build a tray application using WinForms NotifyIcon and ApplicationContext. Left-clicking the icon must open a small popup with a **mock brightness slider**, clearly labeled DEMO, without changing the monitor. Right-clicking opens a menu with Exit. Closing the popup must not terminate the application. Build the project and give me 4–6 manual checks. Do not add DDC/CI, hotkeys, or settings yet.

## Prompt 3 — M2: Read-only monitor discovery

> Complete **only M2**. Add read-only Win32 DDC/CI diagnostics using P/Invoke: enumerate physical monitors, inspect capabilities, read minimum/current/maximum brightness values, report Win32 errors, and properly release native handles. Show a clear mapping to the external monitor; if identification is ambiguous, say so explicitly. **Do not call SetMonitorBrightness or SetVCPFeature anywhere in this milestone.** Report the build result and explain how I can copy the diagnostic output from my Windows laptop back into our chat. Then stop.

## Prompt 4 — M3: One controlled DDC/CI brightness test

> I have now confirmed which physical monitor is external and that DDC/CI reads work: **[REPLACE THIS with my actual M2 diagnostic results]**. Complete **only M3**. Add one manually invoked test button that reads the current brightness, changes it by no more than 5 percentage points within the supported range **on the confirmed monitor only**, and offers a Restore button. Never change brightness automatically on startup, never retry in an endless loop, and do not touch the internal laptop display. Ask me to approve the first hardware-brightness write before it is executed. Show the build results and exact manual test steps.

## Prompt 5 — M4: Working brightness slider

> Complete **only M4**. Connect the UI slider to the previously verified DDC/CI backend. Show unknown brightness values and errors truthfully; normalize the hardware min/max range, use an initial 10% UI safety floor, coalesce rapid slider changes approximately every 150–250 ms, and serialize DDC calls off the UI thread. After building, provide a test plan for both slow and rapid slider movement. Do not add hotkeys or settings yet.

## Prompt 6 — M5: Global keyboard shortcuts

> Complete **only M5**. Add global hotkeys using RegisterHotKey / WM_HOTKEY / UnregisterHotKey. Suggest Ctrl+Alt+Shift+PageUp/PageDown as initial key combinations, if available. Use 5% increments, the same selected external monitor, and the same brightness service as the slider. Registration conflicts must be visible to the user. The registrations must be removed on Exit. Build the application and explain how to test the hotkeys while other applications have focus.

## Prompt 7 — M6: Settings and monitor reconnects

> Complete **only M6**. Add minimal settings: selected external monitor, brightness step, two hotkeys, and UI safety floor. Persist JSON in `%LOCALAPPDATA%\MonitorBrightness\settings.json`, safely handling corrupt settings. Handle display reordering and unplug/replug: never silently apply brightness changes to a different physical monitor. Do not add auto-start behavior. Show the build result, changed files, and manual test plan.

## Prompt 8 — M7: Release EXE

> Complete **only M7**. Review the project, run `dotnet build -c Release` and available tests, then publish a self-contained single-file Windows x64 build using PublishSingleFile=true and PublishTrimmed=false. Do not install drivers, Windows services, or certificates and do not change the registry. Give me the exact path to the finished EXE **only if it really exists**, its file size, and steps to test the published EXE manually on my laptop. Update README.md with known DDC/CI limitations. Do not publish anything online.

## Bug-fix prompt

> We are on milestone M**[NUMBER]**. Bug: **[EXACT SYMPTOM]**. Reproduction: 1) **[...]** 2) **[...]** 3) **[...]**. Expected: **[...]**. Actual: **[...]**. Full error/output: **[...]**. First suggest the most likely cause and a minimal way to confirm it. Modify only code relevant to this bug; do not refactor the whole project. Run the build and explain what you fixed and what still requires testing on my real Windows monitor.

## Prompt for a new session or a different agent

> This is an existing project. Do not create a new one. Read AGENTS.md, SPEC.md, ROADMAP.md, `git status`, `git log -5`, and the current source files. Do not modify anything. Summarize what was implemented, what was **actually verified**, what requires my approval, and the next small step. Do not accept prior claims of “done” as proof without test results.

## Read-only code-review prompt

> Perform a code review of the current milestone **without changing files**. Check native handle lifetime and disposal, DDC error handling, cross-thread UI updates, repeated brightness writes during slider movement, hotkey registration cleanup, settings persistence, dangerous operations, and unnecessary dependencies. Separate confirmed bugs from possible risks. Do not execute any command that changes monitor brightness.
