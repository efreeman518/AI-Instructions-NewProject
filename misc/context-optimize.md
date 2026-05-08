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

Windows note: RTK filters work on native Windows, but shell auto-rewrite hooks are limited. WSL gives fuller hook support.

## Configure RTK For Agents

Run the target-specific init commands you use:

```bash
# Claude Code
rtk init -g

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
