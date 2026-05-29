#Requires -Version 5.1
<#
.SYNOPSIS
  Idempotent setup: clean Python environment, update RTK, Headroom, and the
  knowledge-graph tools (graphify, codegraph) to LATEST stable versions resolved
  at runtime, disable all telemetry, and configure all agent harnesses
  (Claude Code, Codex, Copilot).

  Safe to re-run at any time. headroom-ai resolves to the latest release that has
  a Windows-installable wheel for a SUPPORTED Python ABI (cp312/cp313 today; NOT
  cp314) and never triggers a source build. graphify (PyPI 'graphifyy', pure
  Python) installs into its own isolated venv so a Headroom runtime rebuild never
  wipes it. codegraph (npm '@colbymchenry/codegraph') installs globally via npm.

.PARAMETERS
  -DryRun              Audit all actions without making changes.
  -SkipPythonUpdate    Skip the Python Install Manager update step.
  -SkipVersionCheck    Use fallback versions instead of querying upstream.
  -HeadroomTimeout <n> Seconds to wait for headroom --version before giving up.
                       Default: 0 (wait forever). Pass 30 to cap cold-start waits.

.FALLBACK VERSIONS (used only when -SkipVersionCheck or a network query fails)
  headroom-ai : 0.20.15
  RTK         : 0.38.0
  graphify    : resolved live from PyPI; no pin (pure-Python, upgrade-in-place)
  codegraph   : resolved live from npm;  no pin (npm resolves platform)

.PYTHON ABI NOTE
  headroom-ai ships cp312/cp313 wheels only - it has NO cp314 wheel as of 0.22.x.
  The Headroom runtime venv MUST be built on Python <= 3.13. The base-Python
  resolver below explicitly prefers the newest installed Python that is <= 3.13
  and refuses 3.14+. If only Python 3.14+ is present, a Headroom rebuild cannot
  succeed and the script warns loudly. graphify has no such limit - it prefers
  the newest Python available.

.TOOLING STRATEGY - two activation models

  This script installs FOUR context tools machine-wide, but they fall into two
  groups with deliberately different activation models. Understand the split:

  GROUP A - rtk + headroom: AUTO-ENABLED, MACHINE-GLOBAL, ALWAYS ON
    - rtk      compresses CLI command output (Bash tool calls).
    - headroom compresses prompt inputs (tool output, history) via a local proxy.
    - This script wires both into every agent harness (Claude Code, Codex, Copilot):
      rtk hooks + RTK.md, headroom ANTHROPIC_BASE_URL/OPENAI_BASE_URL routing.
    - They apply to EVERY repo, EVERY session, with zero per-repo action.
    - Safe to apply blindly: lossless, universal, no per-repo cost or judgment call.

  GROUP B - graphify + codegraph: OPT-IN, PER-REPO, MANUAL INIT
    - Knowledge-graph tools. Let an agent query code/doc relationships instead of
      grepping and reading raw files. They sit UPSTREAM of rtk/headroom (they reduce
      WHAT gets loaded; rtk/headroom compress what still does). No overlap.
    - This script installs the BINARIES only. It activates NOTHING in any repo.
    - A graph tool engages in a repo ONLY where you explicitly initialized it:
        graphify .          -> creates graphify-out/   (knowledge-heavy repos)
        codegraph init -i   -> creates .codegraph/      (code-heavy repos)
      Until that marker directory exists, the tool is inert in that repo.
    - Opt-in because they carry per-repo cost (build time, graphify model spend on
      docs, an artifact to maintain) and require a per-repo CHOICE (see below).
      Auto-enabling everywhere would burn that cost on repos where it does not pay.

  WHICH GRAPH TOOL (per repo, by LOC ratio)
    Measure, excluding generated/transient files:
      KNOWLEDGE = LOC of .instructions/ + .scaffold/ + docs/*.md
      CODE      = LOC of application src/ (*.cs,*.razor,*.ts,*.tsx,*.xaml)

      KNOWLEDGE >= CODE .............................. graphify
      CODE > KNOWLEDGE but CODE < 3x KNOWLEDGE ....... graphify
      CODE >= 3x KNOWLEDGE ........................... codegraph
      No .instructions/ or .scaffold/ ............... codegraph
      Brownfield adoption (src/ exists, no .scaffold/) codegraph; re-eval after Phase 1

    WHY: graphify = tree-sitter code parsing PLUS LLM semantic extraction of
    markdown/YAML/infra - it sees the WHOLE repo, including the .instructions/ and
    .scaffold/ knowledge layer and .razor/.xaml markup. codegraph = AST-only, 100%
    local, zero API cost, but BLIND to docs/YAML/markup. A freshly scaffolded app is
    knowledge-heavy -> graphify. A mature app where code dwarfs the static doc layer
    -> codegraph, with graphify reserved for periodic spec<->code consistency checks.

    Build at PHASE BOUNDARIES, not continuously (after Phase 1, after Phase 4, after a
    stabilized Phase 5 slice via 'graphify . --update'). Drift rule: when artifact and
    code disagree, code wins - fix the artifact, then re-extract the affected slice.

  WHERE THE GUIDANCE LIVES (mirrors the activation split)
    - rtk/headroom: ambient rules in CLAUDE.md / agent.md (always loaded).
    - graph tools : support/context-tooling.md, behind a START-AI.md pointer
                    (consulted per repo when deciding whether/which to initialize).

  PYTHON ABI NOTE (also see .PYTHON ABI NOTE above)
    headroom-ai = cp312/cp313 wheels only (no cp314); runtime venv builds on
    Python <= 3.13. graphify is pure-Python (any current Python). codegraph is npm.
    
.NOTES
  - Run from a fresh PowerShell, not inside an activated .venv.
  - Does NOT require Administrator for the user-global path.
  - Headroom proxy is stopped before update and restarted after.
  - Restart Claude Code, Codex, and any IDE after this script so they
    inherit the updated setx env vars.
#>

[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$SkipPythonUpdate,
    [switch]$SkipVersionCheck,
    [int]$HeadroomTimeout = 0   # 0 = wait forever; pass e.g. 30 to cap cold-start waits
)

$ErrorActionPreference = "Continue"
Set-StrictMode -Off

# Suppress headroom telemetry in this session immediately - before any headroom call
$env:HEADROOM_TELEMETRY         = "off"
$env:HEADROOM_REQUIRE_RUST_CORE = "false"

# -- Fallback versions - used only if live upstream queries fail ----------------
$FallbackHeadroomVersion = "0.20.15"
$FallbackRtkVersion      = "0.38.0"

# -- Fixed config ---------------------------------------------------------------
$ProxyPort       = 8787
$RtkBinDir       = "$env:USERPROFILE\.local\bin"      # stable user bin dir for rtk + headroom + graphify shims
$HeadroomRoot    = "$env:USERPROFILE\.headroom"       # headroom home: runtime, shim scripts, config
$HeadroomRuntime = "$HeadroomRoot\runtime"            # isolated Python venv for headroom-ai
$PackageSpec     = "$HeadroomRoot\package-spec.txt"   # records the pinned headroom-ai version
$RunProxyCmd     = "$HeadroomRoot\run-proxy.cmd"      # full proxy launcher (used by shortcuts)
$EnsureProxyCmd  = "$HeadroomRoot\headroom-proxy-ensure.cmd"  # lightweight hook (used by Copilot/Codex)
$HeadroomShim    = "$RtkBinDir\headroom.cmd"          # user-facing headroom command shim
$ShimPs1         = "$HeadroomRoot\headroom-shim.ps1"  # PowerShell backend for the shim

# graphify: isolated venv so a Headroom runtime rebuild never wipes its heavy deps
$GraphifyRoot    = "$env:USERPROFILE\.graphify"
$GraphifyRuntime = "$GraphifyRoot\runtime"
$GraphifyPy      = "$GraphifyRuntime\Scripts\python.exe"
$GraphifyExe     = "$GraphifyRuntime\Scripts\graphify.exe"

# -- Output helpers -------------------------------------------------------------
function Write-Step ([string]$m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-OK   ([string]$m) { Write-Host "  OK  : $m"  -ForegroundColor Green }
function Write-Warn ([string]$m) { Write-Host "  WARN: $m"  -ForegroundColor Yellow }
function Write-Fail ([string]$m) { Write-Host "  FAIL: $m"  -ForegroundColor Red }
function Write-Info ([string]$m) { Write-Host "  $m" }

# Wraps any action block - prints a dry-run notice instead of executing when -DryRun is set
function Invoke-Maybe ([scriptblock]$sb, [string]$desc) {
    if ($DryRun) { Write-Host "  [DryRun] $desc" -ForegroundColor DarkGray; return }
    & $sb
}

# Returns $true if version string $a is >= version string $b (strips non-numeric prefixes)
function Test-VersionGte ([string]$a, [string]$b) {
    try {
        $va = [version]($a -replace '^[^\d]*')
        $vb = [version]($b -replace '^[^\d]*')
        return ($va -ge $vb)
    } catch { return $false }
}

# Resolves the newest installed Python whose minor version is <= $MaxMinor.
# Used to keep the Headroom runtime on a Python with an installable wheel ABI
# (headroom-ai = cp312/cp313 only; passing -MaxMinor 13 refuses 3.14+).
# Pass -MaxMinor 0 for "newest available, no cap" (used by graphify, pure-Python).
# Returns the python.exe path, or $null if none qualifies.
function Resolve-PythonBase ([int]$MaxMinor = 0) {
    $candidates = @()

    # Enumerate via the py launcher (-0p lists installed runtimes with paths)
    try {
        $lines = & py -0p 2>$null
        foreach ($ln in $lines) {
            if ($ln -match '-V:3\.(\d+)\D.*?([A-Za-z]:\\[^\s].*python\.exe)') {
                $candidates += [pscustomobject]@{ Minor = [int]$Matches[1]; Path = $Matches[2].Trim() }
            }
        }
    } catch { }

    # Add common install locations as fallback discovery
    foreach ($glob in @(
        "$env:LOCALAPPDATA\Programs\Python\Python3*\python.exe",
        "C:\Python3*\python.exe"
    )) {
        Get-ChildItem -Path $glob -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.DirectoryName -match 'Python3(\d+)') {
                $candidates += [pscustomobject]@{ Minor = [int]$Matches[1]; Path = $_.FullName }
            }
        }
    }

    $eligible = $candidates |
        Where-Object { $_.Path -and (Test-Path -LiteralPath $_.Path) -and ($MaxMinor -le 0 -or $_.Minor -le $MaxMinor) } |
        Sort-Object Minor -Descending

    if ($eligible) { return $eligible[0].Path }
    return $null
}

# Stops any process listening on $port; returns $true if anything was stopped
function Stop-PortListener ([int]$port) {
    $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if (-not $listeners) { return $false }
    foreach ($l in $listeners) {
        $proc = Get-Process -Id $l.OwningProcess -ErrorAction SilentlyContinue
        Write-Warn "Stopping PID $($l.OwningProcess) ($($proc.Name)) on :$port"
        Invoke-Maybe {
            Stop-Process -Id $l.OwningProcess -Force -ErrorAction SilentlyContinue
        } "stop PID $($l.OwningProcess)"
    }
    Start-Sleep -Seconds 2
    return $true
}

# Runs headroom --version, optionally with a timeout.
# $HeadroomTimeout = 0 means wait forever (default); any positive value caps via a background job.
# The shim cold-starts a Python runtime on first call, which can take several seconds.
function Get-HeadroomVersion {
    if ($HeadroomTimeout -gt 0) {
        $job = Start-Job { headroom --version 2>&1 }
        if (Wait-Job $job -Timeout $HeadroomTimeout) {
            $v = Receive-Job $job
        } else {
            $v = $null
            Write-Warn "headroom --version timed out after ${HeadroomTimeout}s (shim cold-start)"
        }
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        return $v
    } else {
        return (headroom --version 2>&1)
    }
}

# ==============================================================================
# PHASE 0 - Resolve latest versions from upstream sources
# ==============================================================================
Write-Step "PHASE 0 - Resolve latest versions"

# -- headroom-ai: PyPI JSON API ------------------------------------------------
# The /pypi/<pkg>/json endpoint returns ALL releases with their file lists.
# We iterate releases to find the highest stable version that ships a wheel
# installable on Windows without a source build (no MSVC required).
# Acceptable wheel tags:
#   cp313-cp313-win_amd64   compiled Windows wheel
#   py3-none-any            pure Python, any platform
#   cp313-none-any          compiled but platform-neutral
# Excluded: manylinux / linux / macos / darwin - these won't install on Windows.
# NOTE: headroom-ai ships cp312/cp313 only (no cp314). The runtime venv is built
# on Python <= 3.13 (see Resolve-PythonBase -MaxMinor 13 in Phase 5).
$HeadroomVersion = $null
if (-not $SkipVersionCheck) {
    Write-Info "Querying PyPI for latest headroom-ai with Windows-installable wheel..."
    try {
        $pypi = Invoke-RestMethod "https://pypi.org/pypi/headroom-ai/json" `
                    -TimeoutSec 15 -ErrorAction Stop

        # PyPI releases deserializes as PSCustomObject in PowerShell, not a hashtable.
        # Must iterate via .PSObject.Properties; .Keys returns null on PSCustomObject.
        $candidateVersions = $pypi.releases.PSObject.Properties | ForEach-Object {
            $ver   = $_.Name
            $files = $_.Value
            $hasInstallableWheel = $files | Where-Object {
                $_.filename -match '\.whl$' -and
                $_.filename -notmatch 'manylinux|linux|macos|darwin'
            }
            if ($hasInstallableWheel) { $ver }
        } | Where-Object { $_ -and $_ -notmatch 'a\d|b\d|rc\d' } |  # stable releases only
            ForEach-Object {
                try { [version]($_ -replace '^[^\d]*') } catch { $null }
            } | Where-Object { $_ -ne $null } |
            Sort-Object -Descending

        if ($candidateVersions) {
            $HeadroomVersion = $candidateVersions[0].ToString()
            Write-OK "headroom-ai latest with installable Windows wheel: $HeadroomVersion"
        } else {
            Write-Warn "No installable Windows wheel found in any release - falling back"
        }
    } catch {
        Write-Warn "PyPI query failed: $($_.Exception.Message)"
    }
}
if (-not $HeadroomVersion) {
    $HeadroomVersion = $FallbackHeadroomVersion
    Write-Warn "Using fallback headroom-ai version: $HeadroomVersion"
}
$HeadroomSpec = "headroom-ai[proxy]==$HeadroomVersion"

# -- RTK: GitHub Releases API --------------------------------------------------
# Queries the latest release and finds the Windows x86_64 MSVC zip asset.
# Falls back to constructing the canonical URL if the asset list doesn't match.
$RtkVersion     = $null
$RtkDownloadUrl = $null
if (-not $SkipVersionCheck) {
    Write-Info "Querying GitHub for latest RTK release..."
    try {
        $ghRelease  = Invoke-RestMethod `
                        "https://api.github.com/repos/rtk-ai/rtk/releases/latest" `
                        -TimeoutSec 15 -ErrorAction Stop
        $RtkVersion = $ghRelease.tag_name -replace '^v'
        $asset = $ghRelease.assets |
                 Where-Object { $_.name -match "x86_64.*windows.*msvc.*\.zip" -or
                                $_.name -match "windows.*x86_64.*\.zip" } |
                 Select-Object -First 1
        $RtkDownloadUrl = if ($asset) {
            $asset.browser_download_url
        } else {
            # Construct canonical URL if asset pattern didn't match
            "https://github.com/rtk-ai/rtk/releases/download/v$RtkVersion/rtk-x86_64-pc-windows-msvc.zip"
        }
        Write-OK "RTK latest: $RtkVersion  ($( if ($asset) { $asset.name } else { 'URL constructed' } ))"
    } catch {
        Write-Warn "GitHub API query failed: $($_.Exception.Message)"
    }
}
if (-not $RtkVersion) {
    $RtkVersion     = $FallbackRtkVersion
    $RtkDownloadUrl = "https://github.com/rtk-ai/rtk/releases/download/v$RtkVersion/rtk-x86_64-pc-windows-msvc.zip"
    Write-Warn "Using fallback RTK version: $RtkVersion"
}

# -- graphify (PyPI 'graphifyy') + codegraph (npm) latest, for display + skip ---
# Pure-Python and npm respectively - no wheel-ABI pinning needed, unlike headroom.
$GraphifyLatest = $null
if (-not $SkipVersionCheck) {
    Write-Info "Querying PyPI for latest graphifyy..."
    try {
        $gp = Invoke-RestMethod "https://pypi.org/pypi/graphifyy/json" -TimeoutSec 15 -ErrorAction Stop
        $GraphifyLatest = $gp.info.version
        Write-OK "graphifyy latest: $GraphifyLatest"
    } catch { Write-Warn "PyPI graphifyy query failed: $($_.Exception.Message)" }
}
$CodegraphLatest = $null
if (-not $SkipVersionCheck) {
    Write-Info "Querying npm for latest @colbymchenry/codegraph..."
    try {
        $cg = Invoke-RestMethod "https://registry.npmjs.org/@colbymchenry/codegraph/latest" -TimeoutSec 15 -ErrorAction Stop
        $CodegraphLatest = $cg.version
        Write-OK "codegraph latest: $CodegraphLatest"
    } catch { Write-Warn "npm codegraph query failed: $($_.Exception.Message)" }
}

# -- Python: parse python.org downloads page for latest stable -----------------
# Used to give an accurate version comparison when checking if upgrade is needed.
# Not used to drive the actual install - that goes through py / Install Manager.
$PythonLatest = $null
if (-not $SkipVersionCheck -and -not $SkipPythonUpdate) {
    Write-Info "Querying python.org for latest stable Windows release..."
    try {
        $dlPage = Invoke-WebRequest "https://www.python.org/downloads/windows/" `
                      -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        $stableVersions = [regex]::Matches($dlPage.Content, 'Python (3\.\d+\.\d+)</a>') |
            ForEach-Object { $_.Groups[1].Value } |
            Where-Object   { $_ -notmatch 'a\d|b\d|rc\d' } |  # exclude pre-releases
            ForEach-Object { [version]$_ } |
            Sort-Object -Descending
        if ($stableVersions) {
            $PythonLatest = $stableVersions[0].ToString()
            Write-OK "Python latest stable: $PythonLatest"
        }
    } catch {
        Write-Warn "python.org query failed: $($_.Exception.Message)"
    }
}

Write-Info ""
Write-Info "Versions for this run:"
Write-Info "  headroom-ai : $HeadroomVersion  (wheel-installable on Windows, cp<=313)"
Write-Info "  RTK         : $RtkVersion"
Write-Info "  graphify    : $(if ($GraphifyLatest)  { $GraphifyLatest }  else { '(resolve at install)' })"
Write-Info "  codegraph   : $(if ($CodegraphLatest) { $CodegraphLatest } else { '(resolve at install)' })"
Write-Info "  Python      : $(if ($PythonLatest) { $PythonLatest } else { '(Install Manager decides)' })"

# ==============================================================================
# PHASE 1 - Inventory
# ==============================================================================
Write-Step "PHASE 1 - Inventory"

Write-Info "Python executables on PATH:"
where.exe python 2>$null | ForEach-Object { Write-Info "  python => $_" }
where.exe py     2>$null | ForEach-Object { Write-Info "  py     => $_" }

Write-Info "Active Python:"
try { Write-Info "  $(python --version 2>&1)" } catch { Write-Warn "python not found" }

Write-Info "py launcher runtimes:"
try { py -0p 2>&1 | ForEach-Object { Write-Info "  $_" } } catch { Write-Warn "py not found" }

# headroom-ai has no cp314 wheel; warn if no Python <= 3.13 is available for a
# future Headroom runtime rebuild. The current runtime keeps working until a
# version bump forces a rebuild - this surfaces the latent break early.
$py313 = Resolve-PythonBase -MaxMinor 13
if (-not $py313) {
    Write-Warn "No Python <= 3.13 installed. Headroom runtime cannot be rebuilt"
    Write-Warn "(headroom-ai ships cp312/cp313 wheels only, no cp314). Install 3.13:"
    Write-Warn "  winget install Python.Python.3.13"
    Write-Warn "The current Headroom runtime keeps working until its next version bump."
} else {
    Write-OK "Headroom-capable Python present (<=3.13): $py313"
}

Write-Info "RTK:"
try { Write-Info "  $(rtk --version 2>&1)" } catch { Write-Warn "rtk not found" }

# headroom --version cold-starts the shim's Python runtime on first call.
# Get-HeadroomVersion respects -HeadroomTimeout (0 = no timeout, the default).
Write-Info "Headroom:"
try {
    $hrVer = Get-HeadroomVersion
    if ($hrVer) { Write-Info "  $hrVer" }
} catch { Write-Warn "headroom not found" }

Write-Info "Headroom proxy:"
try {
    $h = Invoke-RestMethod "http://127.0.0.1:$ProxyPort/health" -TimeoutSec 3 -ErrorAction Stop
    Write-Info "  status=$($h.status)  version=$($h.version)  rust_core=$($h.rust_core)"
} catch { Write-Info "  not responding (stopped or not yet started)" }

Write-Info "Knowledge-graph tools:"
try { if (Test-Path -LiteralPath $GraphifyExe) { Write-Info "  graphify: $(& $GraphifyExe --version 2>&1)" } else { Write-Info "  graphify: not installed" } } catch { Write-Info "  graphify: probe failed" }
try { Write-Info "  codegraph: $(codegraph --version 2>&1)" } catch { Write-Info "  codegraph: not installed" }

Write-Info "Stale Python env vars:"
$staleFound = $false
foreach ($scope in "User","Machine") {
    foreach ($var in "PY_PYTHON","PY_PYTHON3","PYTHONHOME","PYTHONPATH","PYTHON_MANAGER_DEFAULT") {
        $val = [Environment]::GetEnvironmentVariable($var, $scope)
        if ($val) { Write-Warn "[$scope] $var=$val"; $staleFound = $true }
    }
}
if (-not $staleFound) { Write-OK "No stale Python env vars" }

# ==============================================================================
# PHASE 2 - Python cleanup: env pins + py.ini overrides
# ==============================================================================
Write-Step "PHASE 2 - Clear stale Python env pins and py.ini overrides"

$stamp = Get-Date -Format "yyyyMMddHHmmss"

# Clear user-scope env vars that can break a good Python install.
# PYTHONHOME / PYTHONPATH in particular cause "No module named encodings" errors
# if they point at a different Python version than the one being invoked.
foreach ($var in "PY_PYTHON","PY_PYTHON3","PYTHONHOME","PYTHONPATH","PYTHON_MANAGER_DEFAULT") {
    $val = [Environment]::GetEnvironmentVariable($var, "User")
    if ($val) {
        Write-Warn "Clearing [User] $var = $val"
        Invoke-Maybe { [Environment]::SetEnvironmentVariable($var, $null, "User") } "clear $var"
    }
}

# py.ini files can pin the launcher to an old minor version (e.g. [defaults] python=3.9).
# Back them up with a timestamp suffix before removing so they can be recovered if needed.
foreach ($ini in @("$env:LocalAppData\py.ini","$env:AppData\py.ini","C:\Windows\py.ini")) {
    if (Test-Path -LiteralPath $ini) {
        $content = Get-Content -LiteralPath $ini -Raw
        Write-Warn "Found py.ini at ${ini}: $($content.Trim())"
        Invoke-Maybe {
            Copy-Item -LiteralPath $ini -Destination "$ini.bak-$stamp" -Force
            Remove-Item -LiteralPath $ini -Force
            Write-OK "Backed up and removed: $ini"
        } "backup+remove $ini"
    }
}

# ==============================================================================
# PHASE 3 - Python runtime update
# ==============================================================================
Write-Step "PHASE 3 - Python runtime update"

if ($SkipPythonUpdate) {
    Write-Info "Skipping (-SkipPythonUpdate set)"
} else {
    # Distinguish the new Python Install Manager (supports 'py list', 'py install')
    # from the legacy Python Launcher (C:\Windows\py.exe) which only supports 'py -X.Y'.
    $isNewMgr = $false
    try {
        # Test 'py install --help' specifically - legacy py.exe emits "WARNING.*legacy" on
        # any 'install' subcommand, while the new Install Manager returns real help text.
        $installHelp = & py install --help 2>&1 | Out-String
        $isNewMgr = $installHelp -notmatch "WARNING.*legacy" -and $installHelp -notmatch "unavailable"
    } catch { }

    if ($isNewMgr) {
        Write-Info "Python Install Manager detected. Updating runtimes..."
        Invoke-Maybe {
            py install --configure -y 2>&1 | ForEach-Object { Write-Info "  $_" }
            if ($PythonLatest) {
                Write-Info "  Installing $PythonLatest ..."
                py install $PythonLatest 2>&1 | ForEach-Object { Write-Info "  $_" }
            }
            py install --update  2>&1 | ForEach-Object { Write-Info "  $_" }
            py install --refresh 2>&1 | ForEach-Object { Write-Info "  $_" }
        } "py install --update"
        Write-Info "Installed runtimes:"
        py -0p 2>&1 | ForEach-Object { Write-Info "  $_" }
    } else {
        # Legacy py.exe - cannot run 'py install'. Check if Python is at least current.
        Write-Warn "Legacy Python Launcher detected (C:\Windows\py.exe) - 'py install' unavailable."
        try {
            $current = ((python --version 2>&1) -replace 'Python ').Trim()
            Write-OK "Active Python: $current"
            if ($PythonLatest) {
                if (Test-VersionGte -a $current -b $PythonLatest) {
                    Write-OK "Already at or above latest stable ($PythonLatest) - no upgrade needed"
                } else {
                    Write-Warn "Installed: $current  |  Latest: $PythonLatest"
                    Write-Warn "To upgrade Python and unlock 'py install' management:"
                    Write-Warn "  1. Settings -> Installed Apps -> remove 'Python Launcher'"
                    Write-Warn "  2. winget install 9NQ7512CXL7T -e --accept-package-agreements --accept-source-agreements"
                    Write-Warn "  3. Re-run this script"
                }
            }
        } catch { Write-Warn "python not found on PATH" }
    }
}

Write-Info "Final Python state:"
try { Write-Info "  exe: $(python -c 'import sys;print(sys.executable)' 2>&1)" } catch { }
try { Write-Info "  ver: $(python --version 2>&1)" } catch { }
try { Write-Info "  pip: $(python -m pip --version 2>&1)" } catch { }

# ==============================================================================
# PHASE 4 - Stop Headroom proxy
# ==============================================================================
Write-Step "PHASE 4 - Stop Headroom proxy on :$ProxyPort"

# Must stop before updating the runtime so pip doesn't hit locked files.
Invoke-Maybe {
    $stopped = Stop-PortListener -port $ProxyPort
    if ($stopped) { Write-OK "Proxy stopped" }
    else          { Write-Info "No proxy was listening on :$ProxyPort" }
} "stop proxy"

# ==============================================================================
# PHASE 5 - Headroom shim files + runtime rebuild
# ==============================================================================
Write-Step "PHASE 5 - Headroom shim files and runtime (target: $HeadroomVersion)"

Invoke-Maybe {
    New-Item -ItemType Directory -Force -Path $RtkBinDir, $HeadroomRoot | Out-Null
} "create dirs"

# Check if runtime already matches target version - skip the venv rebuild if so.
# The venv rebuild wipes and recreates the entire runtime, which takes 1-3 minutes.
$runtimeHR    = "$HeadroomRuntime\Scripts\headroom.exe"
$needsRebuild = $true
if (Test-Path -LiteralPath $runtimeHR) {
    try {
        $installedHR = (& $runtimeHR --version 2>&1).Trim()
        if ($installedHR -match [regex]::Escape($HeadroomVersion)) {
            $needsRebuild = $false
            Write-OK "Runtime already at $HeadroomVersion - skipping rebuild"
        } else {
            Write-Info "Runtime is '$installedHR', target is $HeadroomVersion - rebuilding"
        }
    } catch { Write-Info "Runtime probe failed - rebuilding" }
}

# Always rewrite all shim files - they're small and idempotent. This ensures
# any behavioural fix (telemetry, hook fast-path, env vars) is always current.

Write-Info "Writing package-spec.txt..."
Invoke-Maybe {
    # Records the exact headroom-ai version installed in the runtime venv.
    # The shim's Reset-HeadroomRuntime function reads this to rebuild from scratch.
    Set-Content -LiteralPath $PackageSpec -Encoding ASCII -Value $HeadroomSpec
} "write package-spec.txt ($HeadroomSpec)"

Write-Info "Writing headroom.cmd..."
Invoke-Maybe {
    # Thin batch wrapper so 'headroom' resolves from any shell without needing
    # Python on PATH. Delegates to the PowerShell shim for all logic.
    Set-Content -LiteralPath $HeadroomShim -Encoding ASCII -Value @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%USERPROFILE%\.headroom\headroom-shim.ps1" %*
exit /b %ERRORLEVEL%
'@
} "write headroom.cmd"

Write-Info "Writing headroom-shim.ps1..."
Invoke-Maybe {
    Set-Content -LiteralPath $ShimPs1 -Encoding UTF8 -Value @'
param([Parameter(ValueFromRemainingArguments=$true)][string[]]$HeadroomArgs)
$ErrorActionPreference = "Stop"

$root            = Join-Path $env:USERPROFILE ".headroom"
$runtime         = Join-Path $root "runtime"
$runtimePython   = Join-Path $runtime "Scripts\python.exe"
$runtimeHeadroom = Join-Path $runtime "Scripts\headroom.exe"
$packageSpecPath = Join-Path $root "package-spec.txt"

# Finds the newest installed Python <= 3.13 for rebuilding the headroom runtime.
# headroom-ai ships cp312/cp313 wheels only (no cp314), so 3.14+ is refused -
# an --only-binary install on 3.14 would hard-fail. Prefers py -3.13, then
# enumerates py -0p and known install dirs for the highest minor <= 13.
function Resolve-BasePython {
    # Fast path: explicit 3.13 via launcher
    try {
        $p = & py -3.13 -c "import sys;print(sys.executable)" 2>$null
        $p = ($p | Select-Object -Last 1); if ($p) { $p = $p.Trim() }
        if ($LASTEXITCODE -eq 0 -and $p -and (Test-Path -LiteralPath $p)) { return $p }
    } catch { }
    # Enumerate launcher runtimes + known dirs, pick highest minor <= 13
    $cands = @()
    try {
        foreach ($ln in (& py -0p 2>$null)) {
            if ($ln -match '-V:3\.(\d+)\D.*?([A-Za-z]:\\[^\s].*python\.exe)') {
                $cands += [pscustomobject]@{ Minor = [int]$Matches[1]; Path = $Matches[2].Trim() }
            }
        }
    } catch { }
    foreach ($glob in @("$env:LOCALAPPDATA\Programs\Python\Python3*\python.exe","C:\Python3*\python.exe")) {
        Get-ChildItem -Path $glob -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.DirectoryName -match 'Python3(\d+)') { $cands += [pscustomobject]@{ Minor = [int]$Matches[1]; Path = $_.FullName } }
        }
    }
    $hit = $cands | Where-Object { $_.Path -and (Test-Path -LiteralPath $_.Path) -and $_.Minor -le 13 } |
           Sort-Object Minor -Descending | Select-Object -First 1
    if ($hit) { return $hit.Path }
    throw "No Python <= 3.13 found for headroom runtime rebuild (headroom-ai has no cp314 wheel). Install 3.13: winget install Python.Python.3.13"
}

# Wipes and rebuilds the runtime venv from scratch using the current package-spec.txt.
# Uses --only-binary=:all: to guarantee no MSVC source build occurs.
function Reset-HeadroomRuntime {
    $base = Resolve-BasePython
    if (Test-Path -LiteralPath $runtime) { Remove-Item -LiteralPath $runtime -Recurse -Force }
    & $base -m venv $runtime
    & $runtimePython -m pip install --upgrade pip --quiet
    $spec = (Get-Content -LiteralPath $packageSpecPath -Raw).Trim()
    & $runtimePython -m pip install --upgrade --only-binary=:all: $spec
    if ($LASTEXITCODE -ne 0) { throw "headroom-ai install failed" }
}

# Checks that headroom.exe is present and runnable in the runtime venv.
function Test-HeadroomRuntime {
    if (-not (Test-Path -LiteralPath $runtimeHeadroom)) { return $false }
    & $runtimeHeadroom --version *> $null
    return ($LASTEXITCODE -eq 0)
}

# Checks if the proxy is already serving on :8787.
function Test-ProxyReady {
    try {
        $r = Invoke-WebRequest -Uri "http://127.0.0.1:8787/readyz" -UseBasicParsing -TimeoutSec 2
        return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300)
    } catch { return $false }
}

# Manual runtime reset: headroom shim reset
if ($HeadroomArgs.Count -ge 2 -and
    $HeadroomArgs[0] -eq "shim" -and $HeadroomArgs[1] -eq "reset") {
    Reset-HeadroomRuntime; exit 0
}

# Self-heal: rebuild runtime if missing or broken before any real command
if (-not (Test-HeadroomRuntime)) { Reset-HeadroomRuntime }

# Fast-path hook: called by Copilot/Codex on every request to ensure the proxy is running.
# Skip full runtime load - just check the port directly and start proxy if needed.
# The lightweight headroom-proxy-ensure.cmd is now the preferred hook target (< 1s),
# but this branch handles the case where the shim is still invoked directly.
if ($HeadroomArgs.Count -ge 3 -and
    $HeadroomArgs[0] -eq "init" -and
    $HeadroomArgs[1] -eq "hook" -and
    $HeadroomArgs[2] -eq "ensure") {
    $listening = (netstat -an 2>$null | Select-String ":8787 " | Select-String "LISTENING")
    if (-not $listening) {
        Start-Process -FilePath "$root\run-proxy.cmd" `
            -WorkingDirectory $env:USERPROFILE -WindowStyle Hidden
    }
    exit 0
}

# Normal headroom command - pass through to the runtime executable
$env:HEADROOM_TELEMETRY         = "off"
$env:HEADROOM_REQUIRE_RUST_CORE = "false"
& $runtimeHeadroom @HeadroomArgs
exit $LASTEXITCODE
'@
} "write headroom-shim.ps1"

Write-Info "Writing run-proxy.cmd..."
Invoke-Maybe {
    # Full proxy launcher used by Desktop/Startup shortcuts.
    # Sets all noise-suppression env vars so the console window stays clean.
    Set-Content -LiteralPath $RunProxyCmd -Encoding ASCII -Value @'
@echo off
title Headroom Proxy (port 8787)
set HEADROOM_TELEMETRY=off
set HEADROOM_REQUIRE_RUST_CORE=false
set TRANSFORMERS_VERBOSITY=error
set TOKENIZERS_PARALLELISM=false
set HF_HUB_VERBOSITY=error
set HF_HUB_DISABLE_PROGRESS_BARS=1
set HUGGINGFACE_HUB_VERBOSITY=error
set HTTPX_LOG_LEVEL=warning
set PYTHONWARNINGS=ignore::UserWarning
echo === Headroom Proxy launching at %date% %time% ===
echo.
call "%USERPROFILE%\.local\bin\headroom.cmd" proxy --port 8787 --host 127.0.0.1 --no-telemetry --memory --learn --memory-db-path "%USERPROFILE%\.headroom\memory.db"
set EXITCODE=%errorlevel%
echo.
echo === Headroom Proxy exited (exit code %EXITCODE%) ===
echo Press any key to close...
pause >nul
'@
} "write run-proxy.cmd"

Write-Info "Writing headroom-proxy-ensure.cmd (lightweight hook for Copilot/Codex)..."
Invoke-Maybe {
    # This is the preferred hook target for Copilot and Codex.
    # The default 'headroom init hook ensure' routes through headroom.cmd -> PowerShell ->
    # Python runtime, which takes 3-8s on a cold start and trips Copilot's 15s hook timeout.
    # This .cmd does a direct netstat port check in <100ms with no PS or Python startup cost.
    Set-Content -LiteralPath $EnsureProxyCmd -Encoding ASCII -Value @'
@echo off
:: Lightweight proxy-ensure hook - no PowerShell startup, just a TCP port check.
:: Called by Copilot/Codex hooks on every request; must complete well under 15s.
set PORT=8787
netstat -an 2>nul | findstr /C:":%PORT% " | findstr /C:"LISTENING" >nul 2>&1
if %ERRORLEVEL% equ 0 exit /b 0
:: Port not listening - start the proxy hidden and exit immediately.
:: The proxy window opens asynchronously; the hook does not wait for it to be ready.
start "" /b cmd /c ""%USERPROFILE%\.headroom\run-proxy.cmd"" >nul 2>&1
exit /b 0
'@
} "write headroom-proxy-ensure.cmd"

# Ensure .local\bin is on user PATH so headroom.cmd and rtk.exe resolve from any shell
$userPath  = [Environment]::GetEnvironmentVariable("Path","User")
$pathParts = $userPath -split ";" | Where-Object { $_ }
if ($pathParts -notcontains $RtkBinDir) {
    Write-Warn "$RtkBinDir not on user PATH - adding"
    Invoke-Maybe {
        [Environment]::SetEnvironmentVariable("Path","$RtkBinDir;$userPath","User")
    } "add $RtkBinDir to user PATH"
}

# Rebuild the headroom runtime venv if the installed version doesn't match the target.
# Builds on the newest Python <= 3.13 - headroom-ai ships cp312/cp313 wheels only
# (no cp314), so a 3.14 base would hard-fail under --only-binary.
if ($needsRebuild) {
    Invoke-Maybe {
        $basePy = Resolve-PythonBase -MaxMinor 13
        if (-not $basePy) {
            Write-Fail "No Python <= 3.13 found. headroom-ai has no cp314 wheel, so the"
            Write-Fail "Headroom runtime cannot be (re)built on Python 3.14+."
            Write-Fail "Install Python 3.13 (winget install Python.Python.3.13) and re-run."
            return
        }

        Write-Info "Base Python: $basePy ($(& $basePy --version 2>&1))"
        $runtimePy = "$HeadroomRuntime\Scripts\python.exe"

        Write-Info "Wiping old runtime..."
        if (Test-Path -LiteralPath $HeadroomRuntime) {
            Remove-Item -LiteralPath $HeadroomRuntime -Recurse -Force
        }

        Write-Info "Creating venv..."
        & $basePy -m venv $HeadroomRuntime
        if ($LASTEXITCODE -ne 0) { Write-Fail "venv creation failed"; return }

        & $runtimePy -m pip install --upgrade pip --quiet

        # --only-binary=:all: prevents pip from attempting a source build.
        # Phase 0 already verified this version has an installable wheel, so this should succeed.
        Write-Info "Installing $HeadroomSpec (binary-only - wheel verified in Phase 0)..."
        & $runtimePy -m pip install --only-binary=:all: $HeadroomSpec
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "headroom-ai wheel install failed. Version $HeadroomVersion may lack a cp<=313 Windows wheel for this base Python."
            Write-Fail "Confirm a Python <= 3.13 is installed, then re-run."
            return
        }

        if (-not (Test-Path -LiteralPath $runtimeHR)) {
            Write-Fail "headroom.exe missing after install: $runtimeHR"; return
        }
        Write-OK "Runtime headroom: $(& $runtimeHR --version 2>&1)"
    } "rebuild headroom runtime to $HeadroomVersion"
}

# ==============================================================================
# PHASE 6 - RTK: install or skip if already at/above latest
# ==============================================================================
Write-Step "PHASE 6 - RTK (target: v$RtkVersion)"

$rtkExe     = "$RtkBinDir\rtk.exe"
$currentRtk = $null
try { $currentRtk = (& rtk --version 2>&1).Trim() } catch { }

# Skip download if already at or above the target - Test-VersionGte handles
# cases where RTK is newer than the resolved version (e.g. 0.39.0 vs 0.38.0).
if ($currentRtk -and (Test-VersionGte -a $currentRtk -b $RtkVersion)) {
    Write-OK "RTK already at or above v${RtkVersion}: $currentRtk - skipping download"
} else {
    Write-Info "Current RTK: '$currentRtk' - installing v${RtkVersion}..."
    Invoke-Maybe {
        $tmpZip = "$env:TEMP\rtk-$RtkVersion.zip"
        $tmpDir = "$env:TEMP\rtk-extract-$RtkVersion"

        Write-Info "  Downloading: $RtkDownloadUrl"
        try {
            Invoke-WebRequest -Uri $RtkDownloadUrl -OutFile $tmpZip -UseBasicParsing -ErrorAction Stop
        } catch {
            Write-Fail "Download failed: $($_.Exception.Message)"; return
        }

        if (Test-Path -LiteralPath $tmpDir) { Remove-Item -LiteralPath $tmpDir -Recurse -Force }
        Expand-Archive -LiteralPath $tmpZip -DestinationPath $tmpDir -Force

        $extracted = Get-ChildItem -LiteralPath $tmpDir -Filter "rtk.exe" -Recurse |
                     Select-Object -First 1
        if (-not $extracted) { Write-Fail "rtk.exe not found in zip at $tmpDir"; return }

        New-Item -ItemType Directory -Force -Path $RtkBinDir | Out-Null
        Copy-Item -LiteralPath $extracted.FullName -Destination $rtkExe -Force
        Remove-Item $tmpZip, $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-OK "RTK installed: $(& $rtkExe --version 2>&1)"
    } "download+install rtk v$RtkVersion"
}

# Sync any shadowing rtk.exe locations so PATH-resolved rtk always matches
# the canonical version in $RtkBinDir (e.g. a copy in OneDrive or another bin dir).
Invoke-Maybe {
    $env:Path = "$RtkBinDir;" + [Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
                [Environment]::GetEnvironmentVariable("Path","User")
    $allInstances = @(where.exe rtk 2>$null) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    foreach ($inst in $allInstances) {
        if ($inst -ieq $rtkExe) { continue }
        Write-Warn "Shadowing rtk found: $inst - syncing to v$RtkVersion"
        # Stop any rtk.exe process running from this path before overwriting
        Get-Process -Name "rtk" -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -ieq $inst } |
            ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 300
        try {
            Copy-Item -LiteralPath $rtkExe -Destination $inst -Force -ErrorAction Stop
            Write-OK "Synced: $inst"
        } catch {
            Write-Fail "Could not sync $inst - $($_.Exception.Message)"
        }
    }
} "sync shadowing rtk locations"

# ==============================================================================
# PHASE 6.5 - Knowledge-graph tools: graphify (PyPI) + codegraph (npm)
# ==============================================================================
Write-Step "PHASE 6.5 - Knowledge-graph tools (graphify, codegraph)"

# -- graphify: PyPI 'graphifyy' (double-y), CLI 'graphify'. Isolated venv so a
# Headroom runtime rebuild (Phase 5) never wipes its heavy deps (numpy/scipy/
# tree-sitter). No ABI cap - graphifyy is pure-Python, any current Python works.
Invoke-Maybe { New-Item -ItemType Directory -Force -Path $GraphifyRoot | Out-Null } "create .graphify dir"

$gBasePy = Resolve-PythonBase -MaxMinor 0
if (-not $gBasePy) {
    Write-Warn "No Python base found for graphify - skipping (optional)"
} else {
    # Skip-if-current: only touch the venv when missing or behind latest.
    $gCurrent = $null
    if (Test-Path -LiteralPath $GraphifyExe) {
        try { $gCurrent = ((& $GraphifyExe --version 2>&1) -replace '^\D*').Trim() } catch { }
    }
    $gNeeds = $true
    if ($gCurrent -and $GraphifyLatest -and (Test-VersionGte -a $gCurrent -b $GraphifyLatest)) {
        Write-OK "graphify already at or above v${GraphifyLatest}: $gCurrent - skipping"
        $gNeeds = $false
    }
    if ($gNeeds) {
        Invoke-Maybe {
            if (-not (Test-Path -LiteralPath $GraphifyPy)) {
                Write-Info "Creating graphify venv ($gBasePy, $(& $gBasePy --version 2>&1))..."
                & $gBasePy -m venv $GraphifyRuntime
                & $GraphifyPy -m pip install --upgrade pip --quiet
            }
            Write-Info "Installing/updating graphifyy..."
            & $GraphifyPy -m pip install --upgrade graphifyy 2>&1 | ForEach-Object { Write-Info "  $_" }
            if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $GraphifyExe)) {
                Write-OK "graphify: $(& $GraphifyExe --version 2>&1)"
            } else {
                Write-Warn "graphifyy install failed - graphify unavailable this run (optional)"
            }
        } "install/update graphifyy (isolated venv)"
    }
    # Shim so 'graphify' resolves from any shell via the stable bin dir
    Invoke-Maybe {
        Set-Content -LiteralPath "$RtkBinDir\graphify.cmd" -Encoding ASCII -Value @'
@echo off
"%USERPROFILE%\.graphify\runtime\Scripts\graphify.exe" %*
exit /b %ERRORLEVEL%
'@
    } "write graphify.cmd shim"
}

# -- codegraph: npm global '@colbymchenry/codegraph', CLI 'codegraph'. Node 18+.
# AST-only, 100% local, no API key. Per-project index lives under .codegraph/.
$npm = Get-Command npm -ErrorAction SilentlyContinue
if (-not $npm) {
    Write-Warn "npm not on PATH - skipping codegraph (install Node.js 18+ to enable)"
} else {
    $cgCurrent = $null
    try { $cgCurrent = ((codegraph --version 2>&1) -replace '^\D*').Trim() } catch { }
    if ($cgCurrent -and $CodegraphLatest -and (Test-VersionGte -a $cgCurrent -b $CodegraphLatest)) {
        Write-OK "codegraph already at or above v${CodegraphLatest}: $cgCurrent - skipping"
    } else {
        Write-Info "Installing/updating @colbymchenry/codegraph (npm global)..."
        Invoke-Maybe {
            npm install -g "@colbymchenry/codegraph@latest" 2>&1 | ForEach-Object { Write-Info "  $_" }
            if ($LASTEXITCODE -eq 0) { Write-OK "codegraph: $(codegraph --version 2>&1)" }
            else { Write-Warn "codegraph install/probe failed - skipping" }
        } "npm install -g codegraph"
    }
}

# ==============================================================================
# PHASE 7 - Disable telemetry + configure all agent harnesses
# ==============================================================================
Write-Step "PHASE 7 - Disable telemetry + configure all agent harnesses"

# Refresh session PATH so rtk + headroom resolve from their install locations
$env:Path = "$RtkBinDir;" +
            [Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
            [Environment]::GetEnvironmentVariable("Path","User")

# Disable RTK telemetry BEFORE any init calls - rtk init phones home once per day
# and hangs visibly for several seconds while doing so.
Write-Info "Disabling RTK telemetry..."
Invoke-Maybe {
    rtk telemetry disable 2>&1 | ForEach-Object { Write-Info "  $_" }
    Write-OK "RTK telemetry disabled"
} "rtk telemetry disable"

# Disable Headroom telemetry via CLI and also persist via setx so all future
# shells and the proxy window inherit HEADROOM_TELEMETRY=off automatically.
Write-Info "Disabling Headroom telemetry..."
Invoke-Maybe {
    $telOut = headroom telemetry disable 2>&1
    if ($LASTEXITCODE -eq 0) {
        $telOut | ForEach-Object { Write-Info "  $_" }
        Write-OK "Headroom telemetry CLI disabled"
    } else {
        Write-Warn "headroom telemetry subcommand not available in this version - using env var only"
    }
    setx HEADROOM_TELEMETRY "off" | Out-Null
    Write-OK "HEADROOM_TELEMETRY=off persisted via setx"
} "headroom telemetry disable"

# RTK harness init - registers hooks and instruction files for each agent
foreach ($flag in "--auto-patch","--codex","--copilot") {
    Write-Info "rtk init -g $flag"
    Invoke-Maybe {
        rtk init -g $flag 2>&1 | ForEach-Object { Write-Info "  $_" }
    } "rtk init -g $flag"
}
Invoke-Maybe {
    rtk init --show 2>&1 | ForEach-Object { Write-Info "  $_" }
} "rtk init --show"

# headroom init -g copilot reads ~/.copilot/config.json using plain json.loads().
# The Copilot CLI writes this file as JSONC (with // comments), which json.loads()
# rejects with "Expecting value: line 1 column 1 (char 0)".
# Strip comment lines before headroom runs; Copilot CLI re-adds them on next write.
$copilotConfig = "$env:USERPROFILE\.copilot\config.json"
if (Test-Path $copilotConfig) {
    $raw      = Get-Content $copilotConfig -Raw
    $stripped = ($raw -split "`n" | Where-Object { $_ -notmatch '^\s*//' }) -join "`n"
    if ($stripped -ne $raw) {
        Set-Content $copilotConfig $stripped -Encoding UTF8 -NoNewline
        Write-Warn "Stripped JSONC comments from $copilotConfig so headroom can parse it"
    }
}

# Headroom harness init - writes global instruction files for each agent
foreach ($agent in "claude","codex","copilot") {
    Write-Info "headroom init -g $agent"
    Invoke-Maybe {
        $initOut = headroom init -g $agent 2>&1
        $initOut | ForEach-Object { Write-Info "  $_" }
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "headroom init -g $agent exited $LASTEXITCODE - continuing"
        }
    } "headroom init -g $agent"
}

# Patch the Copilot hook to use the lightweight proxy-ensure.cmd instead of the
# default 'headroom init hook ensure' command. The default routes through the PS
# shim + Python runtime and can take 3-8s, tripping Copilot's 15s hook timeout.
# headroom-proxy-ensure.cmd does a netstat port check in under 100ms.
Write-Info "Patching Copilot hook to use lightweight proxy-ensure.cmd..."
Invoke-Maybe {
    # Headroom writes its Copilot hook config to a global location during init.
    # We patch it to point at the lightweight ensure cmd if the key exists.
    $globalHook = "$env:USERPROFILE\.config\headroom\copilot-hook.json"
    foreach ($f in @($globalHook)) {
        if (Test-Path -LiteralPath $f) {
            $json = Get-Content -LiteralPath $f -Raw | ConvertFrom-Json
            if ($json.hookCommand -or $json.hook_command) {
                $json | Add-Member -Force -NotePropertyName "hookCommand" -NotePropertyValue $EnsureProxyCmd
                $json | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $f -Encoding UTF8
                Write-OK "Patched hook config: $f"
            }
        }
    }
    # Also persist the path via setx so any hook runner that reads this env var
    # can find the lightweight cmd without needing the full headroom chain.
    setx HEADROOM_ENSURE_CMD $EnsureProxyCmd | Out-Null
    Write-OK "HEADROOM_ENSURE_CMD set to: $EnsureProxyCmd"
} "patch copilot hook to lightweight ensure cmd"

# Set durable routing env vars so all agents send requests through the proxy.
# setx writes to the registry - effective in all new shells and agent processes.
Write-Info "Setting ANTHROPIC_BASE_URL + OPENAI_BASE_URL via setx..."
Invoke-Maybe {
    setx ANTHROPIC_BASE_URL "http://127.0.0.1:$ProxyPort"    | Out-Null
    setx OPENAI_BASE_URL    "http://127.0.0.1:$ProxyPort/v1" | Out-Null
    Write-OK "Routing env vars set (effective in new shells/agents)"
} "setx routing vars"

# ==============================================================================
# PHASE 8 - Desktop + Startup shortcuts
# ==============================================================================
Write-Step "PHASE 8 - Desktop and Startup shortcuts"

# Create shortcuts in both Desktop and Startup so the proxy can be launched
# manually and also auto-starts on Windows login.
Invoke-Maybe {
    $shell = New-Object -ComObject WScript.Shell
    foreach ($dir in @([Environment]::GetFolderPath("Desktop"),
                       [Environment]::GetFolderPath("Startup"))) {
        $lnk = "$dir\Headroom Proxy.lnk"
        $sc  = $shell.CreateShortcut($lnk)
        $sc.TargetPath       = $RunProxyCmd
        $sc.WorkingDirectory = $env:USERPROFILE
        $sc.WindowStyle      = 1
        $sc.Save()
        Write-OK "Shortcut: $lnk"
    }
} "create Desktop + Startup shortcuts"

# ==============================================================================
# PHASE 9 - Start Headroom proxy
# ==============================================================================
Write-Step "PHASE 9 - Start Headroom proxy"

Invoke-Maybe {
    # Clear the port first in case anything is still holding it from before Phase 4
    Stop-PortListener -port $ProxyPort | Out-Null

    $env:HEADROOM_TELEMETRY         = "off"
    $env:HEADROOM_REQUIRE_RUST_CORE = "false"
    Start-Process -FilePath $RunProxyCmd -WorkingDirectory $env:USERPROFILE -WindowStyle Normal

    # Poll /readyz for up to 20s - proxy needs a few seconds to initialize
    Write-Info "Proxy window launched. Polling /readyz (up to 20s)..."
    $ready = $false
    for ($i = 0; $i -lt 5 -and -not $ready; $i++) {
        Start-Sleep -Seconds 4
        try {
            $null = Invoke-RestMethod "http://127.0.0.1:$ProxyPort/readyz" `
                        -TimeoutSec 3 -ErrorAction Stop
            $ready = $true
        } catch { }
    }

    if ($ready) { Write-OK "Proxy ready on :$ProxyPort" }
    else        { Write-Warn "Proxy did not respond to /readyz - check the console window" }
} "start headroom proxy"

# ==============================================================================
# PHASE 10 - Final verification
# ==============================================================================
Write-Step "PHASE 10 - Final verification"

# Reload PATH so verification uses the same state any new shell will see
$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
            [Environment]::GetEnvironmentVariable("Path","User")

Write-Info ""
Write-Info "-- Target versions resolved this run ----------------------"
Write-Info "  headroom-ai : $HeadroomVersion  (wheel-installable on Windows, cp<=313)"
Write-Info "  RTK         : $RtkVersion"
Write-Info "  graphify    : $(if ($GraphifyLatest)  { $GraphifyLatest }  else { '(resolved at install)' })"
Write-Info "  codegraph   : $(if ($CodegraphLatest) { $CodegraphLatest } else { '(resolved at install)' })"
Write-Info "  Python      : $(if ($PythonLatest) { $PythonLatest } else { '(Install Manager managed)' })"

Write-Info ""
Write-Info "-- Python -------------------------------------------------"
try {
    Write-Info "  where    : $((where.exe python 2>$null) -join ' | ')"
    Write-Info "  python   : $(python -c 'import sys,encodings; print(sys.executable,"|",sys.version.split()[0])' 2>&1)"
    Write-Info "  pip      : $(python -m pip --version 2>&1)"
    py -0p 2>&1 | ForEach-Object { Write-Info "  $_" }
} catch { Write-Warn "python probe: $_" }

try {
    $pyListFinal = & py list 2>&1
    if ($pyListFinal -match "WARNING.*legacy") {
        # Only escalate to WARN if Python itself is also out of date.
        # If Python is current, the legacy launcher is a cosmetic issue, not an action item.
        $currentPyVer    = ((python --version 2>&1) -replace 'Python ').Trim()
        $pythonOutOfDate = $PythonLatest -and -not (Test-VersionGte -a $currentPyVer -b $PythonLatest)
        if ($pythonOutOfDate) {
            Write-Warn "Legacy Python Launcher + Python out of date (installed: $currentPyVer, latest: $PythonLatest)."
            Write-Warn "To upgrade and enable 'py install' management:"
            Write-Warn "  1. Settings -> Installed Apps -> remove 'Python Launcher'"
            Write-Warn "  2. winget install 9NQ7512CXL7T -e --accept-package-agreements --accept-source-agreements"
            Write-Warn "  3. Re-run this script"
        } else {
            Write-Info "  Note: legacy py.exe launcher active (C:\Windows\py.exe); Python $currentPyVer is current."
            Write-Info "  Optional: replace launcher with Install Manager for 'py install' support."
        }
    }
} catch { }

# headroom-ai needs Python <= 3.13 (no cp314 wheel). Confirm one is available so
# the next Headroom version bump can rebuild the runtime.
$py313Final = Resolve-PythonBase -MaxMinor 13
if ($py313Final) { Write-OK "Headroom-capable Python (<=3.13): $py313Final" }
else { Write-Warn "No Python <=3.13 - Headroom cannot rebuild on next version bump. Install: winget install Python.Python.3.13" }

Write-Info ""
Write-Info "-- RTK ----------------------------------------------------"
try {
    Write-Info "  version  : $(rtk --version 2>&1)"
    rtk gain 2>&1 | Select-Object -First 5 | ForEach-Object { Write-Info "  $_" }
    rtk init --show 2>&1 | ForEach-Object { Write-Info "  $_" }
} catch { Write-Warn "rtk probe: $_" }

Write-Info ""
Write-Info "-- Headroom (shim) ----------------------------------------"
try {
    Write-Info "  version  : $(Get-HeadroomVersion)"

    # 'No deployment profile named default' is expected - it means headroom is not
    # installed as a Windows service or scheduled task, which is correct here.
    # We use the Startup folder shortcut instead, which is simpler and more visible.
    $hsStatus = headroom install status 2>&1
    $hsStatus | Where-Object { $_ -notmatch "No deployment profile" } |
        ForEach-Object { Write-Info "    $_" }
    if ($hsStatus -match "No deployment profile") {
        Write-Info "    (no managed service profile - using Startup shortcut, which is correct)"
    }
} catch { Write-Warn "headroom probe: $_" }

Write-Info ""
Write-Info "-- Knowledge-graph tools ----------------------------------"
try {
    $gv = & "$RtkBinDir\graphify.cmd" --version 2>&1
    if ($LASTEXITCODE -eq 0) { Write-OK "graphify : $gv" } else { Write-Info "  graphify : not installed (optional)" }
} catch { Write-Info "  graphify : not installed (optional)" }
try {
    $cgv = (codegraph --version 2>&1)
    if ($LASTEXITCODE -eq 0) { Write-OK "codegraph: $cgv" } else { Write-Info "  codegraph: not installed (optional)" }
} catch { Write-Info "  codegraph: not installed (optional)" }

Write-Info ""
Write-Info "-- Proxy endpoints ----------------------------------------"
foreach ($ep in "/livez","/readyz","/health","/stats") {
    try {
        $resp = Invoke-RestMethod "http://127.0.0.1:$ProxyPort$ep" -TimeoutSec 4 -ErrorAction Stop
        switch ($ep) {
            "/livez"  { Write-OK "/livez  : status=$($resp.status) alive=$($resp.alive) version=$($resp.version)" }
            "/readyz" { Write-OK "/readyz : status=$($resp.status) ready=$($resp.ready) version=$($resp.version) rust_core=$($resp.rust_core)" }
            "/health" { Write-OK "/health : status=$($resp.status) version=$($resp.version) rust_core=$($resp.rust_core)" }
            "/stats"  {
                # Extract named scalar fields only - avoids JSON depth truncation warnings
                # that occur when serializing the full nested stats object.
                Write-OK "/stats  : requests_total=$($resp.requests_total) tokens_saved_total=$($resp.tokens_saved_total) cache_hits=$($resp.cache_hits) uptime_seconds=$($resp.uptime_seconds)"
            }
        }
    } catch { Write-Warn "${ep} : $($_.Exception.Message)" }
}

Write-Info ""
Write-Info "-- Routing + telemetry env vars (user scope) --------------"
Write-Info "  ANTHROPIC_BASE_URL  : $([Environment]::GetEnvironmentVariable('ANTHROPIC_BASE_URL','User'))"
Write-Info "  OPENAI_BASE_URL     : $([Environment]::GetEnvironmentVariable('OPENAI_BASE_URL','User'))"
Write-Info "  HEADROOM_TELEMETRY  : $([Environment]::GetEnvironmentVariable('HEADROOM_TELEMETRY','User'))"
Write-Info "  HEADROOM_ENSURE_CMD : $([Environment]::GetEnvironmentVariable('HEADROOM_ENSURE_CMD','User'))"

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host @"

Pass criteria:
  python    -> resolves to intended runtime, import encodings OK
  pip       -> works
  rtk       -> v$RtkVersion or newer, telemetry disabled
  headroom  -> v$HeadroomVersion (wheel-installable, shim runtime), telemetry disabled
  graphify  -> installed in isolated venv (.graphify\runtime), shim on PATH
  codegraph -> installed via npm global (Node 18+); skipped if npm absent
  /livez + /readyz -> healthy
  /health   -> ready (rust_core:disabled is OK on Python without Rust wheel)
  /stats    -> counters visible
  ANTHROPIC_BASE_URL + OPENAI_BASE_URL -> set via setx
  HEADROOM_TELEMETRY -> off
  HEADROOM_ENSURE_CMD -> points to lightweight proxy-ensure.cmd
  Python <=3.13 present -> required for future Headroom runtime rebuilds (no cp314 wheel)

Action required after this script:
  Restart Claude Code, Codex, and any IDE so they pick up the new setx vars.
  Per-repo (not done here): 'graphify install' once, then 'graphify .' or
  'codegraph init -i' per project. See support/context-tooling.md for selection.
"@