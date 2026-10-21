# Monitoren — Codex Starter Kit

**This repository initially contains documentation and configuration, not a working application.** The goal is to build a small Windows tray utility that controls an external HDMI monitor's hardware brightness, one verified milestone at a time, using Codex.

## Start here

1. On Windows, install **Git**, the **.NET 10 SDK**, and a **Codex-capable ChatGPT desktop experience** (or use Codex CLI with a local Windows workspace).
2. Extract **all files** from this starter kit into a new directory, for example `C:\Projects\MonitorBrightness`. Open that folder as a local project in Codex.
3. Read [`CODEX_SETUP.md`](CODEX_SETUP.md) and [`GETTING_STARTED.md`](GETTING_STARTED.md), then verify `dotnet --info` and `git --version`.
4. Send **Prompt 0** from [`PROMPTS.md`](PROMPTS.md) to the agent. Continue with Prompts 1–8, **one milestone at a time**.
5. After every milestone, verify the result on your actual Windows laptop and save a local Git checkpoint.

## Files in this kit

| File | Intended reader | Purpose |
|---|---|---|
| `AGENTS.md` | Codex / other AI coding agents | Standing rules, workflow, permissions, and hardware safety |
| `SPEC.md` | Agent and developer | Product requirements and V1 scope |
| `ROADMAP.md` | Agent and developer | Milestones M0–M7 and acceptance tests |
| `PROMPTS.md` | Developer | Ready-to-use English prompts for each milestone |
| `GETTING_STARTED.md` | Developer | Beginner's step-by-step workflow |
| `CODEX_SETUP.md` | Developer | Windows Codex setup, permissions, first session, and Git |
| `.editorconfig` | Editor / tooling | Basic formatting conventions |
| `.gitignore` | Git | Ignore build outputs and temporary files |

## Key safety rule

Do not grant the agent unrestricted permissions. **Before M3, it must never send brightness-write commands to the monitor**; at M3, authorize only a small, reversible manual test on your confirmed external display. If DDC/CI is unsupported, stop and revisit the implementation plan: NVIDIA's brightness control may use a different mechanism.

## Build and run (after M0 creates the project)

The source project does **not yet exist** in this starter kit. Once Codex has created it, run the commands reported for your actual generated directory, generally:

```powershell
dotnet build
dotnet run --project .\MonitorBrightness\MonitorBrightness.csproj
```

See `ROADMAP.md` for release publishing instructions at M7.
