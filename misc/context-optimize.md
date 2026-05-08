# Context Optimization Setup

Use this guide to give AI coding agents longer, cleaner working sessions with less token waste.

This setup combines:

- [RTK](https://github.com/rtk-ai/rtk): command-output compression for shells and dev CLIs.
- [Headroom](https://github.com/chopratejas/headroom): local proxy/context optimization for model requests, tool outputs, logs, files, and agent traffic.
- Shared global AI instruction files: one source of truth that feeds Claude, Codex, Copilot, and editor/CLI agents.

## Why Use This

AI coding agents burn context on noisy terminal output, long logs, repetitive file reads, verbose package managers, raw diffs, and repeated session instructions. Context optimization keeps the facts the agent needs and removes boilerplate that pushes useful state out of the model window.

RTK handles the shell layer. It runs commands like `git status`, `dotnet test`, `npm test`, `rg`, `docker ps`, and `kubectl logs`, then returns compact output. RTK preserves command exit codes and can keep raw failed output for recovery.

Headroom handles the model traffic layer. It runs locally as a proxy or wrapper, intercepts requests before they reach the provider, compresses context, improves cache stability, and can expose retrieval/memory tools. In proxy mode, OpenAI-compatible clients point to `http://127.0.0.1:8787/v1`; Anthropic-compatible clients point to `http://127.0.0.1:8787`.

## Quick Start

After publishing this file, replace `[this page url]` with the public URL.

### Install And Configure

```text
Follow the instructions at [this page url].

Install RTK and Headroom for my OS, configure RTK for the AI agents I use, set up a Headroom proxy on 127.0.0.1:8787, configure startup service/task if supported, create a desktop/manual shortcut if useful, and add global AI instruction files for Claude, Codex, and GitHub Copilot.

Before changing files, inspect my current config and preserve existing custom instructions. Do not run git commit or git push.
```

### Check For Updates And Install

```text
Check RTK and Headroom installed versions against their official release/package sources. Update them if newer stable versions are available. After updating, verify `rtk --version`, `rtk gain`, `headroom --help`, `headroom install status`, and Headroom health at http://127.0.0.1:8787/health if the proxy is running. Preserve existing agent instructions and service config.
```

### Turn Telemetry On Or Off

```text
Configure telemetry for RTK and Headroom.

Ask me whether telemetry should be on or off. For RTK, use the current RTK-supported telemetry command if present, or update RTK config only if the installed version documents that setting. For Headroom, use `--no-telemetry` for proxy/service installs or `HEADROOM_TELEMETRY=off` when disabling. Restart the Headroom persistent service if needed, then report effective status and any command that was unavailable in my installed versions.
```

### Retrieve Performance Stats

```text
Retrieve context-optimization stats for RTK and Headroom.

Run `rtk gain`, `rtk gain --history`, and `rtk gain --all --format json` if supported. For Headroom, check `headroom install status`, `headroom perf --hours 24`, `headroom memory stats` if memory is enabled, and proxy endpoints `/stats`, `/stats-history`, and `/metrics` at http://127.0.0.1:8787. Summarize token savings, failures/fallbacks, proxy health, and any missing telemetry/log data.
```

### Bypass Headroom For Exact Work

```text
For this session, ignore Headroom optimization. Use raw provider routing or Headroom passthrough with `--no-optimize`. Use raw command output when exact formatting, order, quoting, logs, JSON, diffs, or legal/security review matters. Keep using RTK only when compact shell output is acceptable.
```

## Important Tradeoff

Headroom is intentionally lossy in normal optimization mode: it tries to preserve task intent, errors, anomalies, structure, and recent relevant facts, not every byte of the original context. Some Headroom modes and features support retrieval of originals, but the default session experience still optimizes aggressively.

Tell the agent to ignore Headroom or use passthrough/raw reads when exactness matters:

- Legal, security, compliance, or forensic review.
- Exact log, stack trace, command output, JSON, XML, CSV, or protocol inspection.
- Quoting text, reproducing docs, license review, copyright-sensitive work.
- Debugging a suspected compression/proxy issue.
- Benchmarking model cost, latency, or raw provider behavior.
- Applying patches where omitted lines, whitespace, or ordering may matter.
- Any task where the agent says it needs the original full output.

Useful bypasses:

```powershell
# Start Headroom but disable optimization.
headroom proxy --port 8787 --no-optimize

# Run through original provider for one session by removing Headroom base URLs.
Remove-Item Env:\OPENAI_BASE_URL -ErrorAction SilentlyContinue
Remove-Item Env:\ANTHROPIC_BASE_URL -ErrorAction SilentlyContinue

# Use RTK explicitly for compact output, or omit RTK for raw output.
rtk git status
git status
```

## Install RTK

Official docs: [rtk-ai/rtk](https://github.com/rtk-ai/rtk)

macOS:

```bash
brew install rtk
rtk --version
rtk gain
```

Linux/macOS quick install:

```bash
curl -fsSL https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh | sh
export PATH="$HOME/.local/bin:$PATH"
rtk --version
```

Cargo:

```bash
cargo install --git https://github.com/rtk-ai/rtk
rtk --version
```

Windows native:

1. Download the Windows release zip from <https://github.com/rtk-ai/rtk/releases>.
2. Extract `rtk.exe` to a folder on `PATH`, for example `%USERPROFILE%\.local\bin`.
3. Restart terminal.
4. Verify:

```powershell
rtk --version
rtk gain
```

Windows note: RTK filters work on native Windows, but shell auto-rewrite hooks are limited. Treat manual `rtk <cmd>` prefixes as the reliable Windows path. WSL gives fuller hook support.

## Configure RTK For Agents

Run the target-specific init commands you use:

```bash
# Claude Code
rtk init -g

# Claude Code, non-interactive or migration-safe
rtk init -g --auto-patch

# Codex
rtk init -g --codex

# GitHub Copilot CLI / Copilot agent integration where supported
rtk init -g --copilot

# Cursor
rtk init -g --agent cursor

# Gemini CLI
rtk init -g --gemini

# Show installed hook/config state
rtk init --show
```

`rtk init --show` may report `Local (./CLAUDE.md): exists but rtk not configured` in a repository that has its own neutral Claude instruction file. That is fine when global instructions already tell agents to use manual `rtk <cmd>` prefixes. Do not inject machine-specific RTK blocks into repo-local instruction files unless the repo intentionally owns that policy.

Native PowerShell habit:

```powershell
rtk git status
rtk dotnet test
rtk npm test
rtk rg "pattern"
rtk docker ps
rtk kubectl logs my-pod
```

Command chains should prefix each external segment:

```powershell
rtk git status && rtk dotnet test
```

## Install Headroom

Official docs: [chopratejas/headroom](https://github.com/chopratejas/headroom)

Python install:

```bash
python -m pip install "headroom-ai[all]"
headroom --help
```

Windows note: if Python lives under a corporate, OneDrive-managed, or sandboxed profile, prefer a dedicated venv at `%USERPROFILE%\.headroom\venv` instead of `pip install --user`. See [Windows Troubleshooting](#windows-troubleshooting).

Node install, for TypeScript/Node apps:

```bash
npm install headroom-ai
```

Docker image:

```bash
docker pull ghcr.io/chopratejas/headroom:latest
```

Headroom Desktop:

- Desktop app repo: <https://github.com/gglucass/headroom-desktop>
- Latest releases: <https://github.com/gglucass/headroom-desktop/releases/latest>
- Current public desktop support is strongest on macOS Apple Silicon; Linux builds are preview. Check release notes before relying on it for other environments.

## Run Headroom Proxy

Manual foreground run:

```bash
headroom proxy --port 8787
```

OpenAI-compatible clients:

```bash
export OPENAI_BASE_URL=http://127.0.0.1:8787/v1
```

PowerShell:

```powershell
$env:OPENAI_BASE_URL = "http://127.0.0.1:8787/v1"
```

Anthropic-compatible clients:

```bash
export ANTHROPIC_BASE_URL=http://127.0.0.1:8787
```

PowerShell:

```powershell
$env:ANTHROPIC_BASE_URL = "http://127.0.0.1:8787"
```

Health checks:

```powershell
Invoke-RestMethod http://127.0.0.1:8787/livez
Invoke-RestMethod http://127.0.0.1:8787/readyz
Invoke-RestMethod http://127.0.0.1:8787/health
Invoke-RestMethod http://127.0.0.1:8787/stats
```

## Install Headroom As Startup Proxy

Preferred persistent install:

```bash
headroom install apply --preset persistent-service --runtime python --scope user --port 8787 --mode token --no-telemetry
headroom install status
```

Windows note: `persistent-service` and `persistent-task` commonly require Administrator rights. For no-admin Windows installs, use a Startup folder shortcut to a visible launcher script instead. See [Windows Troubleshooting](#windows-troubleshooting).

Useful service commands:

```bash
headroom install start
headroom install stop
headroom install restart
headroom install status
headroom install remove
```

Configure durable agent routing:

```bash
# Claude Code
headroom init -g claude --port 8787

# Codex
headroom init -g codex --port 8787

# GitHub Copilot CLI
headroom init -g copilot --port 8787
```

Enable memory only if wanted:

```bash
headroom install apply --preset persistent-service --runtime python --scope user --port 8787 --mode token --memory --no-telemetry
```

## Windows Desktop Link For Manual Proxy Run

Create a desktop shortcut that starts the proxy in a PowerShell window:

```powershell
$desktop = [Environment]::GetFolderPath("Desktop")
$target = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$args = "-NoExit -Command headroom proxy --port 8787 --no-telemetry"
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut("$desktop\Headroom Proxy.lnk")
$shortcut.TargetPath = $target
$shortcut.Arguments = $args
$shortcut.WorkingDirectory = $env:USERPROFILE
$shortcut.Save()
```

Use `--no-optimize` in the shortcut arguments when you want pass-through mode.

## Windows Troubleshooting

These notes come from a real Windows 11 install with Python 3.14 and a OneDrive-managed profile.

### Recommended Windows Bring-Up

Use a venv outside AppData, install proxy runtime dependencies explicitly, and route clients through the proxy with durable user environment variables:

```powershell
# 1. Build a venv outside AppData.
& "$env:LOCALAPPDATA\Programs\Python\Python314\python.exe" -m venv "$env:USERPROFILE\.headroom\venv"

# 2. Install binary wheels only. Avoid source builds and [all] ML extras.
& "$env:USERPROFILE\.headroom\venv\Scripts\python.exe" -m pip install --only-binary=:all: `
    headroom-ai fastapi "uvicorn[standard]" "httpx[http2]" h2

# 3. Route AI clients through Headroom. Restart agents after setx.
setx ANTHROPIC_BASE_URL "http://127.0.0.1:8787"
setx OPENAI_BASE_URL    "http://127.0.0.1:8787/v1"
```

Create `%USERPROFILE%\.headroom\run-proxy.cmd`:

```batch
@echo off
title Headroom Proxy (port 8787)
set HEADROOM_TELEMETRY=off
set HEADROOM_REQUIRE_RUST_CORE=false
echo === Headroom Proxy launching at %date% %time% ===
echo.
"%USERPROFILE%\.headroom\venv\Scripts\headroom.exe" proxy --port 8787 --host 127.0.0.1 --no-telemetry
set EXITCODE=%errorlevel%
echo.
echo === Headroom Proxy exited (exit code %EXITCODE%) ===
echo Press any key to close this window...
pause >nul
```

Create Desktop and Startup shortcuts with Windows shell APIs so OneDrive redirection is handled correctly:

```powershell
$shell = New-Object -ComObject WScript.Shell
foreach ($dir in @([Environment]::GetFolderPath("Desktop"),
                   [Environment]::GetFolderPath("Startup"))) {
    $sc = $shell.CreateShortcut("$dir\Headroom Proxy.lnk")
    $sc.TargetPath       = "$env:USERPROFILE\.headroom\run-proxy.cmd"
    $sc.WorkingDirectory = $env:USERPROFILE
    $sc.WindowStyle      = 1
    $sc.Save()
}
```

A simpler shortcut such as `powershell -NoExit -Command headroom proxy --port 8787 --no-telemetry` can work when `headroom.exe` resolves reliably from `PATH`. Prefer the `run-proxy.cmd` + venv `headroom.exe` form when diagnosing Explorer/PATH issues or Python install ambiguity.

### Windows Failure Modes

- `pip install --user headroom-ai` succeeds, but `ModuleNotFoundError: No module named 'headroom'` appears from another shell. Cause: AppData/user-site redirection or sandboxing. Fix: use `%USERPROFILE%\.headroom\venv`; verify with `python -c "import sys, site; print(site.getusersitepackages()); [print(p) for p in sys.path]"`.
- Python 3.14 may lack a prebuilt `headroom._core` Rust wheel. Fix: set `HEADROOM_REQUIRE_RUST_CORE=false` for degraded pure-Python mode, or build the venv with Python 3.13 for full Rust-core support.
- `pip install "headroom-ai[all]"` fails with MSVC errors. Cause: ML extras such as `hnswlib` and `py-rust-stemmers` may build from source. Fix: skip `[all]` and use `--only-binary=:all:` with `headroom-ai fastapi "uvicorn[standard]" "httpx[http2]" h2`.
- Proxy exits with missing `fastapi`, `h2`, or HTTP/2 dependency errors. Fix: install the explicit runtime dependencies above; base `headroom-ai` does not always pull proxy extras.
- `headroom install apply` fails with `[SC] OpenSCManager FAILED 5` or `schtasks ... Access is denied`. Cause: Windows service/task presets need admin rights. Fix: use a Startup folder `.lnk` pointing at `run-proxy.cmd`.
- `python -m headroom` fails with `No module named headroom.__main__`. Fix: call the venv console script: `%USERPROFILE%\.headroom\venv\Scripts\headroom.exe`.
- Global or AppData `headroom.exe` works in one shell but fails from Explorer with `The system cannot find the path specified`. Fix: call the venv `headroom.exe`; venv shims hardcode the venv Python.
- A hidden launcher window flashes and disappears. Fix: keep `cmd` visible and use `pause >nul` on exit so crash output stays readable.
- Port `8787` is already in use. Diagnose and stop the stale listener:

```powershell
Get-NetTCPConnection -LocalPort 8787 -State Listen |
    ForEach-Object { Get-Process -Id $_.OwningProcess } |
    Format-Table Id, Name, StartTime, Path

Get-NetTCPConnection -LocalPort 8787 -State Listen |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

- `rtk proxy <cmd>` is not the normal Windows wrapper. Use `rtk <cmd>` for filtered output. Reserve `rtk proxy <cmd>` for RTK passthrough/debug.
- `rtk init --show` reports `Hook: not found` on native Windows. This can be cosmetic when `CLAUDE.md` or global agent instructions already mandate manual `rtk <cmd>` prefixes. Run `rtk init -g --auto-patch` once to migrate old blocks, then use manual prefixes. Do not keep chasing Unix-style auto-rewrite hooks on native Windows.
- Proxy running does not mean clients use it. Clients must start with `ANTHROPIC_BASE_URL=http://127.0.0.1:8787` or `OPENAI_BASE_URL=http://127.0.0.1:8787/v1`. Use `setx`, fully restart agents, then confirm `/stats` counters climb while the agent works.
- `headroom install status` reports `No deployment profile named 'default' is installed`. That only means Headroom is not installed as a managed service/task. It is acceptable when using manual launch or the no-admin Startup shortcut.
- `headroom init -g <agent>` reports success, but config files do not update. Cause: sandboxed or redirected writes. Fix: prefer `setx` routing, or verify config files from a separate regular terminal.

### Windows Verification Checklist

```powershell
Get-NetTCPConnection -LocalPort 8787 -State Listen
Invoke-RestMethod http://127.0.0.1:8787/livez
Invoke-RestMethod http://127.0.0.1:8787/readyz
Invoke-RestMethod http://127.0.0.1:8787/health
Invoke-RestMethod http://127.0.0.1:8787/stats
```

Expected results:

- Exactly one listener on port `8787`, owned by the venv Python.
- `/livez` reports alive.
- `/readyz` reports ready.
- `/health` may show `rust_core` disabled on Python 3.14; that is acceptable when `HEADROOM_REQUIRE_RUST_CORE=false`.
- `/stats` request counters increase after restarting an AI agent and sending work through it.

## Validate Setup Against This Guide

Use this when an agent reports setup drift.

```powershell
rtk init --show
[Environment]::GetEnvironmentVariable('ANTHROPIC_BASE_URL','User')
[Environment]::GetEnvironmentVariable('OPENAI_BASE_URL','User')
Invoke-RestMethod http://127.0.0.1:8787/health
Invoke-RestMethod http://127.0.0.1:8787/stats
headroom install status
```

Expected:

- Claude Code RTK on Unix/WSL: hook, global `RTK.md`, global `CLAUDE.md` reference, and `settings.json` hook should be `ok`.
- Claude Code RTK on native Windows: `Hook: not found` can be acceptable because auto-rewrite hooks are limited; verify global instructions require manual `rtk <cmd>` prefixes.
- Repo-local `CLAUDE.md` can remain unconfigured when it is only a neutral project pointer.
- Codex can route through `~/.codex/config.toml` with `base_url = "http://127.0.0.1:8787/v1"`; it does not require user env vars for Codex itself.
- User env vars should be set with `setx` for other clients and future shells.
- Headroom health should be ready/healthy. `rust_core: disabled` is acceptable on Python 3.14 degraded mode.
- `headroom install status` may show no installed profile when using manual launch or the Startup shortcut.

## Global AI Instruction Strategy

Keep one editable source folder for global instructions, then generate agent-specific files from it.

Recommended source:

```text
~/.ai/
  GLOBAL_INSTRUCTIONS.md
  CLAUDE.adapter.md
  CODEX.adapter.md
  COPILOT.adapter.md
  sync-instructions.ps1
```

Recommended generated targets:

```text
Global/user level:
  ~/.claude/CLAUDE.md
  ~/.codex/AGENTS.md or repo AGENTS.md
  VS Code / Copilot custom instructions target

Repo level:
  AGENTS.md
  CLAUDE.md
  .github/copilot-instructions.md
  .vscode/settings.json, only if needed for VS Code/Copilot behavior
```

Generated files should include a header like:

```markdown
<!-- GENERATED BY ~/.ai/sync-instructions.ps1; edit source files in ~/.ai instead. -->
```

Core instructions used in this environment:

```markdown
# Global AI Coding Instructions

## Critical Rules

- Accuracy, pragmatism, and honesty are critical. State facts; avoid unsupported opinions.
- Be concise and focused. Short, direct responses. No filler, no preamble, no trailing summaries.
- Use caveman compression principles. Strip articles, connectives, filler, and passive voice. Keep facts, numbers, names, technical terms, commands, paths, and constraints.
- Compress working notes harder than final answers. Plans, scratch text, and progress updates may use fragments. Final user-facing answers must stay readable.
- Never trade brevity for ambiguity. If compression would hide risk, uncertainty, or a blocker, state it plainly.
- Never run `git commit` or `git push` unless explicitly told to do so.

## RTK - Token-Optimized Commands

- Prefer `rtk` for external CLI commands when available. Unknown commands pass through unchanged, so RTK is safe for normal command use.
- Prefix command chains segment-by-segment. Example: `rtk git status && rtk dotnet test`.
- Common commands: `rtk git`, `rtk gh`, `rtk dotnet`, `rtk npm`, `rtk pnpm`, `rtk npx`, `rtk cargo`, `rtk docker`, `rtk kubectl`, `rtk curl`, `rtk grep`, `rtk ls`, `rtk find`.
- Useful wrappers: `rtk summary <cmd>`, `rtk err <cmd>`, `rtk log <file>`, `rtk json <file>`, `rtk diff`, `rtk proxy <cmd>`.

## Headroom

- Headroom proxy normally listens on `http://127.0.0.1:8787`.
- OpenAI-compatible traffic routes through `http://127.0.0.1:8787/v1`.
- Health endpoints include `/livez`, `/readyz`, `/health`, `/stats`, `/stats-history`, and `/metrics`.
- Use `rtk headroom ...` when RTK has a wrapper available; otherwise use `headroom ...`.
- If model calls fail with proxy/connectivity errors, check Headroom health/listening state before changing model/provider configuration.
```

Codex adapter:

```markdown
## Codex-Specific Rules

- In PowerShell, keep native PowerShell cmdlets native. Use `rtk` for external CLI tools when available, especially build, test, search, git, package manager, Docker, Kubernetes, and network commands.
- Prefix external shell commands that invoke wrapped tools with `rtk`, including every segment in chained commands.
- Headroom provider routing can use `model_provider = "headroom"` and `base_url = "http://127.0.0.1:8787/v1"` when Codex is configured for an OpenAI-compatible proxy.
```

Claude adapter:

```markdown
## Claude-Specific Rules

- Prefer RTK-wrapped shell commands for external tools.
- Use Headroom routing when proxy is healthy.
- If exact raw output matters, request raw command output or Headroom passthrough.
```

Copilot adapter:

```markdown
## Copilot-Specific Rules

- Prefer RTK-wrapped external commands in terminal workflows.
- In VS Code, also use `.github/copilot-instructions.md` for repo-specific instructions.
- Keep generated Copilot instructions short; prioritize command policy, safety rules, and project conventions.
```

## Configure Claude, Codex, Copilot

Claude Code:

```bash
rtk init -g
headroom init -g claude --port 8787
```

Codex CLI:

```bash
rtk init -g --codex
headroom init -g codex --port 8787
```

Codex config example:

```toml
model_provider = "headroom"

[model_providers.headroom]
name = "Headroom"
base_url = "http://127.0.0.1:8787/v1"
wire_api = "responses"
```

GitHub Copilot CLI:

```bash
rtk init -g --copilot
headroom init -g copilot --port 8787
```

GitHub Copilot VS Code extension:

1. Add repo instructions at `.github/copilot-instructions.md`.
2. Put shared global rules in the Copilot custom instructions location supported by your editor/account.
3. Include the RTK command policy and Headroom bypass warning.
4. Keep project-specific build/test commands in repo docs, not only global instructions.

Claude app, Copilot app, and other chat apps:

1. Put the short global instruction set in app-level custom instructions.
2. Keep local tool install commands in repo docs.
3. Tell the app when Headroom is unavailable or intentionally bypassed.

## Quick Verification Checklist

```bash
rtk --version
rtk gain
headroom --help
headroom install status
curl http://127.0.0.1:8787/health
curl http://127.0.0.1:8787/stats
```

PowerShell:

```powershell
rtk --version
rtk gain
headroom --help
headroom install status
Invoke-RestMethod http://127.0.0.1:8787/health
Invoke-RestMethod http://127.0.0.1:8787/stats
```
