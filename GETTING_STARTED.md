# Your First AI-Coded Project: Monitoren

This is a guide **for the human developer**, not a system prompt for the agent. Give the agent one assignment at a time from `PROMPTS.md`; its standing rules are in `AGENTS.md`.

## 1. Goal and limitations

We want a small Windows utility: a system tray icon, a popup slider to change the brightness of an external HDMI monitor, and keyboard shortcuts. Version 1 controls **hardware brightness using DDC/CI**. That is not necessarily the same adjustment as the Brightness slider in NVIDIA software, which may use digital color correction. If your monitor does not support DDC/CI, do not ask an agent to promise it will work; discuss a different implementation separately.

**Important:** to test the actual system tray, global hotkeys, and DDC/CI, you need a Windows computer with the external monitor connected. An agent running in the cloud, macOS, or Linux cannot honestly certify hardware compatibility. If necessary, run commands locally on Windows and send the agent the real outputs and errors.

## 2. Install on Windows

1. **Git:** https://git-scm.com/downloads/win
2. **.NET 10 SDK** (the SDK, not just the Runtime): https://dotnet.microsoft.com/en-us/download/dotnet/10.0
3. **Codex in the ChatGPT desktop experience on Windows:** https://chatgpt.com/codex. You do not need Cursor or Visual Studio. See `CODEX_SETUP.md` for details.
4. **An external monitor** attached by HDMI. Find its DDC/CI option in the monitor's on-screen menu and turn it on if present.
5. Optional: try Monitorian (https://github.com/emoacht/Monitorian) first to check whether hardware brightness control is supported.

Open **PowerShell** and check:

```powershell
dotnet --info
dotnet new list
git --version
```

`dotnet --info` should show an installed 10.x SDK. In the template list, look for `winforms`. If you do not have a working Windows environment, set that up before attempting DDC milestones.

## 3. Create the repository folder

Choose a path such as `C:\Projects\MonitorBrightness` (not `Program Files`). Extract **all the files** in this starter ZIP so `AGENTS.md` sits at the root. Open this exact folder as a **Local Codex project** using native Windows / PowerShell.

Start a new Codex session rooted in this folder and choose an appropriate **ask-for-approval** permission mode. Send Prompt 0 from `PROMPTS.md`, then Prompt 1, and continue in order. Codex normally loads `AGENTS.md` at session startup, but explicit references to `SPEC.md` and `ROADMAP.md` help keep the task scoped.

## 4. Permissions: what to allow

| Agent action | What you should do |
|---|---|
| Read and edit files **inside this project** | Allow |
| Run `dotnet restore/build/run/test` and `git status/diff` | Allow after reading the command |
| Install an SDK, Git, or a NuGet package | First check exactly what is being installed and why |
| Access the internet | Allow specific Microsoft documentation/NuGet operations if necessary; unrestricted access is not needed |
| Run as administrator or change the Windows registry | **Not for MVP** |
| Execute an unfamiliar PowerShell script from the internet | **Do not allow without reviewing it** |
| Change actual monitor brightness | Only approve **one explicitly selected manual test** at the correct milestone |
| Delete the project, `git reset --hard`, `git clean`, push to GitHub | **Do not allow without separate understanding and approval** |
| Add Windows startup tasks, a service, or scheduled tasks | **Not until explicitly requested** |

If your environment has Ask/Approve, Auto, or Full access modes, begin with a **restricted workspace and approvals for higher-risk operations**, not unrestricted access. Avoid “YOLO,” “dangerously skip permissions,” or similar modes. See `CODEX_SETUP.md` for details.

## 5. How to work with the agent

Repeat this cycle for **every** milestone:

1. Send **exactly one milestone prompt** from `PROMPTS.md`.
2. Let the agent change the necessary files and run `dotnet build`.
3. Review the changed-file summary, diff, and exact build result.
4. Run the app **yourself** and complete the relevant `Manual gate` in `ROADMAP.md`.
5. If something fails, do not just say “it doesn't work.” Provide the exact symptom, steps to reproduce, complete terminal output, milestone number, and a screenshot if useful.
6. When it passes, create a local Git checkpoint and only then proceed to the next milestone.

Avoid switching models or editing tools in the middle of a milestone unless necessary. If you do switch, start a fresh session by asking the agent to read the project documents and `git diff` first.

## 6. Git commands worth learning

From the project root in PowerShell:

```powershell
git status                    # See changed files
git diff                      # Inspect edits to tracked files
git add .                     # Stage files for the next commit
git commit -m "M1: tray shell works"
git log --oneline -5          # Show the last five checkpoints
```

The agent may run `git init` during M0. If Git asks for a user name/email, configure Git following its own instructions; do not give the agent passwords. Never paste passwords or GitHub tokens into a prompt. A Git commit is a **local checkpoint**; a GitHub account is not required yet.

**Undoing changes:** ask the agent to explain a safe, targeted reversal. Do not blindly run `git reset --hard`, as it can destroy uncommitted work.

## 7. Tests that actually demonstrate success

`dotnet build` verifies compilation; **it does not prove HDMI brightness control works**.

- **M1:** tray icon appears; slider is labeled Demo; Exit stops the program.
- **M2:** diagnostics identify the external monitor and show current brightness or a concrete DDC error. No brightness changes at this stage.
- **M3:** after **your approval**, a small hardware brightness change works and can be restored.
- **M4:** rapid slider movement does not freeze the UI; the monitor eventually reaches the last selected value.
- **M5:** shortcuts work while a browser has focus and stop working after Exit.
- **M6:** settings survive a restart; unplugging HDMI does not crash the app or change the internal display.
- **M7:** the published `.exe` starts directly, without `dotnet run`.

## 8. Common problems

**Monitor not detected:** check the cable, target selection, USB-C/HDMI docks, and whether Windows is extending displays.

**DDC/CI does not respond:** enable DDC/CI in the monitor menu, try direct HDMI without a dock, and compare with Monitorian. Do not call `SetVCPFeature` blindly as a workaround.

**NVIDIA adjusts brightness but DDC/CI cannot:** those might be **different brightness mechanisms**. Do not assume a DDC-only implementation can reproduce NVIDIA settings; decide separately whether a GPU/gamma fallback is acceptable (it may change colors rather than backlight output).

**Agent says “done” without running anything:** ask for the actual commands, output, and list of **unverified** Windows behaviors.

**Agent edits 20 files in one go:** stop and ask it to split the work into small, testable patches.

**Build fails:** give the agent the **complete** `dotnet build` output and the first error, not just “build failed.”

## 9. Definition of a finished V1

A stable tray icon, popup slider, two global hotkeys, saved settings, control of **the chosen external monitor only**, clean shutdown, and a Windows x64 executable that you have actually run and tested. Auto-start, custom styling, and NVIDIA integration can come **later**.

## Official references

- Microsoft WinForms NotifyIcon: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-windows-forms
- Microsoft Windows monitor brightness API: https://learn.microsoft.com/en-us/windows/win32/api/highlevelmonitorconfigurationapi/nf-highlevelmonitorconfigurationapi-getmonitorbrightness
- Microsoft RegisterHotKey: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
- Microsoft single-file deployment: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/
