# Codex on Windows: Step-by-Step Setup for MonitorBrightness

This guide is **for you, the developer**. `AGENTS.md` contains the standing instructions that **Codex reads when starting a local session** in this repository. `SPEC.md` describes the product, and `ROADMAP.md` breaks the work into milestones. You do not need to paste every file into every prompt.

## 1. Choose your development environment

**Recommended for your first project:** the ChatGPT desktop app on Windows -> **Codex** -> **Local** -> native Windows / PowerShell. Cursor, Visual Studio, and VS Code are not required.

Why Local instead of Cloud:
- A WinForms application targeting `net10.0-windows` is best built and tested on Windows.
- Real DDC/CI monitor control and system tray behavior need testing on your actual Windows laptop.
- Codex changes the files on your computer, so you can build and run the application immediately.

An alternative is the **Codex CLI** in PowerShell. It works with the same local project, but you interact with the agent in a terminal. For a first project, the desktop app may make it easier to review diffs, conversations, and project files.

Official starting points:
- https://chatgpt.com/codex
- https://learn.chatgpt.com/docs/windows/windows-app
- https://learn.chatgpt.com/docs/windows/windows-sandbox
- https://learn.chatgpt.com/docs/agent-configuration/agents-md

Interface labels and available modes can change. Use the equivalent local-workspace and approval options if the current interface differs from this guide.

## 2. Install the prerequisites

Open Windows PowerShell. You normally do not need to run it as administrator to develop this project.

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e
winget install --id Git.Git -e
```

After the installation finishes, close PowerShell and open it again:

```powershell
dotnet --info
dotnet new list
git --version
```

`dotnet --info` should report an installed SDK version 10.x; `dotnet new list` should include the `winforms` template. `git --version` should print a Git version. An installer may request confirmation or elevation: read the dialog before approving it, and do not grant Codex broad administrator access just for convenience.

**Codex:** install or open the official ChatGPT desktop experience with Codex support from the link above and sign in to your account. Codex CLI and Node.js are **not** needed if you use the desktop interface.

## 3. Create the project folder

```powershell
New-Item -ItemType Directory -Force C:\Projects\MonitorBrightness
```

Extract the entire ZIP **directly into** `C:\Projects\MonitorBrightness`, not into another nested folder. The project root should initially look like this:

```text
C:\Projects\MonitorBrightness\
  AGENTS.md
  SPEC.md
  ROADMAP.md
  PROMPTS.md
  GETTING_STARTED.md
  CODEX_SETUP.md
  README.md
  .editorconfig
  .gitignore
```

In the ChatGPT desktop app, open **Codex** and add this directory as a local project (for example, Add project / Open folder; labels may differ). Choose **Local** and native **Windows**, if those options are offered. Avoid Cloud or WSL for the initial hardware-testing workflow, since they cannot perform the same direct Windows tray and HDMI tests.

## 4. Most important: permissions

Choose a permission/approval mode that **asks before actions needing elevated scope**. In the CLI, the equivalent starting configuration is:

```powershell
codex --sandbox workspace-write --ask-for-approval on-request
```

What this means:
- Codex can read/change workspace files and run normal build commands within the allowed workspace.
- Access outside the workspace sandbox or to restricted resources should request approval, according to the active environment's policy.
- It does **not** mean every change inside your project is approved individually.
- The sandbox is **not a hardware-safety barrier** for DDC/CI commands. Our milestone rules therefore prohibit monitor brightness writes until a specific manual test is authorized.

**Do not select unrestricted/Full access to fix an ordinary build error.** Do not grant administrator permissions, access to unrelated personal files, SSH keys, credentials, or GitHub unless a later, clearly explained task requires it.

In Codex CLI, `/status` can help inspect your environment and `/permissions` can expose permission settings where supported. In a graphical interface, use its current permission controls.

### Optional: Codex CLI configuration

CLI users can inspect and, if appropriate, edit `%USERPROFILE%\.codex\config.toml`:

```toml
approval_policy = "on-request"
sandbox_mode = "workspace-write"

[sandbox_workspace_write]
network_access = false
```

This is **not a repository file**. It is user-level configuration, possibly shared by other Codex sessions. Check for existing settings before editing it. If using the desktop interface, it is easier to choose the available permission mode in the UI instead.

## 5. Your first Codex session: read-only planning

Start a **Local** Codex conversation attached to `C:\Projects\MonitorBrightness`. Send this message:

> Read AGENTS.md, SPEC.md, ROADMAP.md, and CODEX_SETUP.md. For now, only analyze; do not edit any files or install anything. Tell me which instructions you have loaded, our current milestone, the exact commands to check .NET and Git, and what I will need to test manually on Windows. Do not send any DDC/CI brightness-write commands to my monitor. Then stop.

Check that Codex understands the project path and that the current milestone is M0. If it starts editing files without being asked, stop the operation and inspect the diff.

## 6. Next step: milestone M0

Send **Prompt 1** from `PROMPTS.md`. It should create only a minimal WinForms project. When Codex finishes:

1. Review the file diff; make sure no unnecessary NuGet packages, drivers, or services were introduced.
2. Read the actual output of `dotnet build`.
3. Run `dotnet run` and confirm the starter window appears on Windows.
4. Report your manual-test result to Codex. After your approval, create a local Git commit.
5. Only then proceed to M1, which adds the tray icon.

If `dotnet restore` needs network access to contact NuGet, grant **narrow, explained approval** rather than unrestricted internet access. A fresh WinForms project should not need arbitrary third-party packages. GitHub integration and publishing can wait until later.

## 7. Review changes and use Git

Use the **Changes / Diff** view if your Codex interface has one. In PowerShell:

```powershell
git status
git diff
git diff --stat
```

After you have verified a milestone and want to save a checkpoint:

```powershell
git add .
git status
git commit -m "M0: initial WinForms build"
```

Before `git add .`, examine `.gitignore` and `git status` so you do not accidentally commit secrets, build artifacts, or personal files. If the sandbox blocks writes under `.git`, perform the commit yourself in a normal PowerShell session instead of disabling all protection.

**Do not have two Codex sessions modify the same working directory simultaneously.** For the first project, use one agent, one milestone, and one manual verification at a time.

## 8. The limit of automated verification

Codex can verify that the .NET compiler built the code, but **that alone cannot prove**:
- The tray icon and popup behave correctly on your desktop.
- The app never controls the internal laptop display by mistake.
- The external HDMI monitor actually changes its hardware backlight brightness.
- The hotkeys work when a different application has focus.

For milestones M1–M6, perform the **Manual gate** steps in `ROADMAP.md`. The first authorized DDC/CI **write** test is **M3**, only after you confirm the selected external monitor.

## 9. If you prefer Codex CLI

CLI is **optional** if you are using the desktop app. Follow the official installation instructions, since installation methods and versions may change: https://learn.chatgpt.com/docs/codex/cli

After installing the CLI, run:

```powershell
Set-Location C:\Projects\MonitorBrightness
codex --sandbox workspace-write --ask-for-approval on-request
```

Sign in with your ChatGPT account when prompted. There is no reason to paste API keys into the agent conversation.

Potentially useful commands **inside** Codex CLI: `/status`, `/permissions`, `/review`, and `/model`, depending on the installed version. Codex reads project-level `AGENTS.md` at session startup; after changing agent instructions, starting a fresh session helps ensure they are reloaded.

## 10. Three common beginner mistakes

1. **“The agent said Done” does not mean “I verified it.”** Ask for real build output and test behavior on your laptop.
2. **“It needs access” does not automatically mean “grant Full access.”** The actual issue might be blocked NuGet traffic, an incorrect path, protected `.git` operations, or a missing SDK. Diagnose that specific issue.
3. **“Build every feature at once” makes bugs hard to locate.** Follow M0 through M7, ending each milestone with a manual test and a Git checkpoint.
