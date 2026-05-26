#Requires -Version 5.1
<#
.SYNOPSIS
  Enable RTK command-rewriting for the GitHub Copilot VS Code extension in the
  CURRENT project (per-repo, not machine-wide).

  Unlike update-python-and-context-tools.ps1 (which is machine/user scoped),
  this script intentionally writes INTO the project directory, because the
  VS Code Copilot extension only discovers customization from the workspace -
  there is no user-global Copilot instructions location.

  What `rtk init --copilot` installs here:
    .github\hooks\rtk-rewrite.json   PreToolUse hook -> "rtk hook copilot"
                                     (rewrites shell commands to `rtk ...` when
                                     Copilot's AGENT mode runs a terminal command)
    .github\copilot-instructions.md  RTK usage boilerplate

  IMPORTANT CAVEATS
    - This is RTK only. Headroom canNOT proxy the VS Code Copilot extension -
      the extension uses GitHub's proprietary endpoints and ignores
      OPENAI_BASE_URL / ANTHROPIC_BASE_URL. Headroom routing only applies to
      the Copilot CLI, which the machine-level script already configures.
    - `rtk init --copilot` OVERWRITES an existing .github\copilot-instructions.md.
      If this repo already has one, this script will refuse unless you pass
      -Force, and even then it backs the old file up to
      .github\copilot-instructions.md.bak first.
    - Restart VS Code (or reload the window) after running so the Copilot
      extension picks up the new hook.

.PARAMETER Force
  Overwrite an existing .github\copilot-instructions.md (a .bak copy is kept).

.PARAMETER DryRun
  Show what would happen without changing anything.

.PARAMETER Uninstall
  Remove the RTK Copilot artifacts (.github\hooks\rtk-rewrite.json and, if it
  is RTK's boilerplate, .github\copilot-instructions.md) from this project.

.EXAMPLE
  cd C:\src\my-repo
  ..\AI-Instructions-Scaffold\misc\enable-rtk-copilot-project.ps1
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$DryRun,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

function Write-OK   ([string]$m) { Write-Host "  OK  : $m" -ForegroundColor Green }
function Write-Warn ([string]$m) { Write-Host "  WARN: $m" -ForegroundColor Yellow }
function Write-Info ([string]$m) { Write-Host "  $m" }
function Write-Step ([string]$m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }

$projectRoot   = (Get-Location).Path
$gitDir        = Join-Path $projectRoot ".git"
$githubDir     = Join-Path $projectRoot ".github"
$hookFile      = Join-Path $githubDir   "hooks\rtk-rewrite.json"
$instrFile     = Join-Path $githubDir   "copilot-instructions.md"
$instrBak      = "$instrFile.bak"

Write-Step "RTK Copilot integration - project: $projectRoot"

# Guard: must look like a project (a git repo). This prevents the
# "ran it in the wrong folder" footgun that motivated this script.
if (-not (Test-Path $gitDir)) {
    Write-Warn "No .git directory found here - this does not look like a project root."
    Write-Warn "cd into the repo you want to configure, then re-run."
    exit 1
}

# rtk must be on PATH.
$rtk = Get-Command rtk -ErrorAction SilentlyContinue
if (-not $rtk) {
    Write-Warn "rtk not found on PATH. Run update-python-and-context-tools.ps1 first."
    exit 1
}

# -- Uninstall -----------------------------------------------------------------
if ($Uninstall) {
    Write-Step "Uninstall"
    if (Test-Path $hookFile) {
        if ($DryRun) { Write-Info "[DryRun] would remove $hookFile" }
        else { Remove-Item $hookFile -Force; Write-OK "Removed $hookFile" }
        $hooksDir = Split-Path $hookFile
        if ((Test-Path $hooksDir) -and -not (Get-ChildItem $hooksDir -Force)) {
            if (-not $DryRun) { Remove-Item $hooksDir -Force }
            Write-Info "Removed empty $hooksDir"
        }
    } else { Write-Info "No $hookFile to remove." }

    if (Test-Path $instrFile) {
        $first = (Get-Content $instrFile -TotalCount 1)
        if ($first -match 'RTK\s*[--]\s*Token-Optimized') {
            if ($DryRun) { Write-Info "[DryRun] would remove RTK-generated $instrFile" }
            else { Remove-Item $instrFile -Force; Write-OK "Removed RTK-generated $instrFile" }
        } else {
            Write-Warn "$instrFile is not RTK's boilerplate - left in place."
        }
    }
    if (Test-Path $instrBak) {
        Write-Info "A backup exists at $instrBak - restore it manually if needed."
    }
    Write-Step "Done - reload VS Code to deactivate."
    exit 0
}

# -- Install -------------------------------------------------------------------

# Protect an existing, non-RTK copilot-instructions.md.
if (Test-Path $instrFile) {
    $first = (Get-Content $instrFile -TotalCount 1)
    $isRtkBoilerplate = $first -match 'RTK\s*[--]\s*Token-Optimized'
    if (-not $isRtkBoilerplate) {
        if (-not $Force) {
            Write-Warn "$instrFile already exists and is not RTK boilerplate."
            Write-Warn "'rtk init --copilot' would OVERWRITE it. Re-run with -Force to proceed"
            Write-Warn "(a .bak copy will be kept), or merge RTK's rules in manually."
            exit 1
        }
        if ($DryRun) {
            Write-Info "[DryRun] would back up $instrFile -> $instrBak"
        } else {
            Copy-Item $instrFile $instrBak -Force
            Write-OK "Backed up existing instructions -> $instrBak"
        }
    }
}

Write-Step "rtk init --copilot"
if ($DryRun) {
    Write-Info "[DryRun] would run: rtk init --copilot   (cwd: $projectRoot)"
} else {
    # Run explicitly in the project root (no -g: this is project-scoped).
    & $rtk.Source init --copilot 2>&1 | ForEach-Object { Write-Info "  $_" }
    if (Test-Path $hookFile)  { Write-OK "Hook installed:        $hookFile" }
    if (Test-Path $instrFile) { Write-OK "Instructions installed: $instrFile" }
}

Write-Step "Next steps"
Write-Info "1. Reload VS Code (Developer: Reload Window) so the Copilot extension picks up the hook."
Write-Info "2. Decide whether to commit .github\hooks\rtk-rewrite.json + .github\copilot-instructions.md"
Write-Info "   or add them to .gitignore (the hook only affects Copilot AGENT-mode terminal commands)."
Write-Info "3. Headroom is NOT involved - it cannot proxy the VS Code Copilot extension (only the Copilot CLI)."
