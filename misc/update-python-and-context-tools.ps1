#Requires -Version 5.1
<#
.SYNOPSIS
  Idempotent setup: clean Python environment, update RTK and Headroom to
  LATEST stable versions (resolved at runtime), disable all telemetry, and
  configure user-global agent harness integrations (Claude Code, Codex, and
  the Copilot CLI). VS Code Copilot is intentionally NOT configured here —
  it has no machine-level instructions location, so it would only leave a
  stray project-relative .github/ folder.

  Machine/user scoped: this script writes only to %USERPROFILE%, machine/user
  env vars, and global tool configs — never to the current directory. It is
  safe to run from anywhere.

  Safe to re-run at any time. headroom-ai version is resolved to the latest
  release that has a Windows cp313 binary wheel — never a source-build version.

.PARAMETERS
  -DryRun              Audit all actions without making changes.
  -SkipPythonUpdate    Skip the Python Install Manager update step.
  -SkipVersionCheck    Use fallback versions instead of querying upstream.
  -HeadroomTimeout <n> Seconds to wait for headroom --version before giving up.
                       Default: 0 (wait forever). Pass 30 to cap cold-start waits.

.FALLBACK VERSIONS (used only when -SkipVersionCheck or a network query fails)
  headroom-ai : 0.20.15
  RTK         : 0.38.0

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
    [int]$HeadroomTimeout = 0   # 0 = wait forever; pass 30 to cap cold-start waits
)

$ErrorActionPreference = "Continue"
Set-StrictMode -Off

# Telemetry off in this session immediately
$env:HEADROOM_TELEMETRY         = "off"
$env:HEADROOM_REQUIRE_RUST_CORE = "false"

# ── Fallback versions (used only if live resolution fails) ─────────────────────
$FallbackHeadroomVersion = "0.20.15"
$FallbackRtkVersion      = "0.38.0"

# ── Fixed config ───────────────────────────────────────────────────────────────
$ProxyPort       = 8787
$RtkBinDir       = "$env:USERPROFILE\.local\bin"
$HeadroomRoot    = "$env:USERPROFILE\.headroom"
$HeadroomRuntime = "$HeadroomRoot\runtime"
$PackageSpec     = "$HeadroomRoot\package-spec.txt"
$RunProxyCmd     = "$HeadroomRoot\run-proxy.cmd"
$HeadroomShim    = "$RtkBinDir\headroom.cmd"
$ShimPs1         = "$HeadroomRoot\headroom-shim.ps1"

# ── Helpers ────────────────────────────────────────────────────────────────────
function Write-Step ([string]$m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-OK   ([string]$m) { Write-Host "  OK  : $m"  -ForegroundColor Green }
function Write-Warn ([string]$m) { Write-Host "  WARN: $m"  -ForegroundColor Yellow }
function Write-Fail ([string]$m) { Write-Host "  FAIL: $m"  -ForegroundColor Red }
function Write-Info ([string]$m) { Write-Host "  $m" }

function Invoke-Maybe ([scriptblock]$sb, [string]$desc) {
    if ($DryRun) { Write-Host "  [DryRun] $desc" -ForegroundColor DarkGray; return }
    & $sb
}

# True if a JSON-ish file actually contains // or /* */ comments (JSONC).
# The GitHub Copilot CLI writes its ~/.copilot/config.json as JSONC ("// This
# file is managed automatically."), which crashes headroom-ai 0.20.x's
# `init copilot` (plain json.loads -> JSONDecodeError at line 1 col 1).
function Test-IsJsonc ([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    try {
        $raw = Get-Content -LiteralPath $path -Raw -ErrorAction Stop
        # crude but sufficient: a // or /* near the start, before the first {
        $head = if ($raw.Length -gt 400) { $raw.Substring(0,400) } else { $raw }
        return ($head -match '(^|\r?\n)\s*//' -or $head -match '/\*')
    } catch { return $false }
}

function Test-VersionGte ([string]$a, [string]$b) {
    try {
        $va = [version]($a -replace '^[^\d]*')
        $vb = [version]($b -replace '^[^\d]*')
        return ($va -ge $vb)
    } catch { return $false }
}

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

# Run headroom --version, respecting $HeadroomTimeout (0 = no timeout)
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

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 0 — Resolve latest versions from upstream sources
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 0 — Resolve latest versions"

# ── headroom-ai: find latest version with a Windows cp313 binary wheel ─────────
# The PyPI /json endpoint only returns the latest release. We need /pypi/<pkg>/json
# which includes all releases so we can filter by wheel availability.
$HeadroomVersion = $null
if (-not $SkipVersionCheck) {
    Write-Info "Querying PyPI for latest headroom-ai with Windows cp313 wheel..."
    try {
        $pypi = Invoke-RestMethod "https://pypi.org/pypi/headroom-ai/json" `
                    -TimeoutSec 15 -ErrorAction Stop

        # releases deserializes as PSCustomObject in PowerShell — iterate via PSObject.Properties
        $candidateVersions = $pypi.releases.PSObject.Properties | ForEach-Object {
            $ver   = $_.Name
            $files = $_.Value
            # Accept any wheel that can install on Windows without a source build:
            #   - cp313-cp313-win_amd64  (compiled Windows wheel)
            #   - py3-none-any           (pure Python, any platform)
            #   - cp313-none-any         (compiled, any platform)
            # Exclude: manylinux / linux / macos / darwin (won't install on Windows)
            $hasInstallableWheel = $files | Where-Object {
                $_.filename -match '\.whl$' -and
                $_.filename -notmatch 'manylinux|linux|macos|darwin'
            }
            if ($hasInstallableWheel) { $ver }
        } | Where-Object { $_ -and $_ -notmatch 'a\d|b\d|rc\d' } |  # stable only
            ForEach-Object {
                try { [version]($_ -replace '^[^\d]*') } catch { $null }
            } | Where-Object { $_ -ne $null } |
            Sort-Object -Descending

        if ($candidateVersions) {
            $HeadroomVersion = $candidateVersions[0].ToString()
            Write-OK "headroom-ai latest with installable Windows wheel: $HeadroomVersion"
        } else {
            Write-Warn "No cp313 Windows wheel found in any release — falling back"
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

# ── RTK: GitHub Releases API ──────────────────────────────────────────────────
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

# ── Python: parse python.org for latest stable ────────────────────────────────
$PythonLatest = $null
if (-not $SkipVersionCheck -and -not $SkipPythonUpdate) {
    Write-Info "Querying python.org for latest stable Windows release..."
    try {
        $dlPage = Invoke-WebRequest "https://www.python.org/downloads/windows/" `
                      -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        $stableVersions = [regex]::Matches($dlPage.Content, 'Python (3\.\d+\.\d+)</a>') |
            ForEach-Object { $_.Groups[1].Value } |
            Where-Object   { $_ -notmatch 'a\d|b\d|rc\d' } |
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
Write-Info "  headroom-ai : $HeadroomVersion  (wheel-installable on Windows)"
Write-Info "  RTK         : $RtkVersion"
Write-Info "  Python      : $(if ($PythonLatest) { $PythonLatest } else { '(Install Manager decides)' })"

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 1 — Inventory
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 1 — Inventory"

Write-Info "Python executables on PATH:"
where.exe python 2>$null | ForEach-Object { Write-Info "  python => $_" }
where.exe py     2>$null | ForEach-Object { Write-Info "  py     => $_" }

Write-Info "Active Python:"
try { Write-Info "  $(python --version 2>&1)" } catch { Write-Warn "python not found" }

Write-Info "py launcher runtimes:"
try { py -0p 2>&1 | ForEach-Object { Write-Info "  $_" } } catch { Write-Warn "py not found" }

Write-Info "RTK:"
try { Write-Info "  $(rtk --version 2>&1)" } catch { Write-Warn "rtk not found" }

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

Write-Info "Stale Python env vars:"
$staleFound = $false
foreach ($scope in "User","Machine") {
    foreach ($var in "PY_PYTHON","PY_PYTHON3","PYTHONHOME","PYTHONPATH","PYTHON_MANAGER_DEFAULT") {
        $val = [Environment]::GetEnvironmentVariable($var, $scope)
        if ($val) { Write-Warn "[$scope] $var=$val"; $staleFound = $true }
    }
}
if (-not $staleFound) { Write-OK "No stale Python env vars" }

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 2 — Python cleanup: env pins + py.ini overrides
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 2 — Clear stale Python env pins and py.ini overrides"

$stamp = Get-Date -Format "yyyyMMddHHmmss"

foreach ($var in "PY_PYTHON","PY_PYTHON3","PYTHONHOME","PYTHONPATH","PYTHON_MANAGER_DEFAULT") {
    $val = [Environment]::GetEnvironmentVariable($var, "User")
    if ($val) {
        Write-Warn "Clearing [User] $var = $val"
        Invoke-Maybe { [Environment]::SetEnvironmentVariable($var, $null, "User") } "clear $var"
    }
}

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

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 3 — Python runtime update
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 3 — Python runtime update"

if ($SkipPythonUpdate) {
    Write-Info "Skipping (-SkipPythonUpdate set)"
} else {
    # Detect the new Python Install Manager vs the legacy C:\Windows\py.exe.
    # NOTE: `& py list 2>&1` returns an ARRAY of lines. `$array -notmatch "x"`
    # returns the SUBSET of lines that don't match — always truthy when any
    # line differs — so the old `-notmatch ... -and -notmatch ...` test was
    # effectively always $true and we ran broken `py install ...` on legacy
    # launchers. Join to one string and POSITIVELY test for the legacy
    # signature instead.
    $isNewMgr = $false
    try {
        $pyListText = (& py list 2>&1 | Out-String)
        $looksLegacy = ($pyListText -match 'legacy py\.exe' -or
                        $pyListText -match "command is unavailable" -or
                        $pyListText -match "can't open file")
        $isNewMgr = -not $looksLegacy
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
        Write-Warn "Legacy Python Launcher detected (C:\Windows\py.exe) — 'py install' unavailable."
        try {
            $current = ((python --version 2>&1) -replace 'Python ').Trim()
            Write-OK "Active Python: $current"
            if ($PythonLatest) {
                if (Test-VersionGte -a $current -b $PythonLatest) {
                    Write-OK "Already at or above latest stable ($PythonLatest)"
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

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 4 — Stop Headroom proxy
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 4 — Stop Headroom proxy on :$ProxyPort"

Invoke-Maybe {
    $stopped = Stop-PortListener -port $ProxyPort
    if ($stopped) { Write-OK "Proxy stopped" }
    else          { Write-Info "No proxy was listening on :$ProxyPort" }
} "stop proxy"

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 5 — Headroom shim files + runtime rebuild
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 5 — Headroom shim files and runtime (target: $HeadroomVersion)"

Invoke-Maybe {
    New-Item -ItemType Directory -Force -Path $RtkBinDir, $HeadroomRoot | Out-Null
} "create dirs"

# Check if runtime already matches target version
$runtimeHR    = "$HeadroomRuntime\Scripts\headroom.exe"
$needsRebuild = $true
if (Test-Path -LiteralPath $runtimeHR) {
    try {
        $installedHR = (& $runtimeHR --version 2>&1).Trim()
        if ($installedHR -match [regex]::Escape($HeadroomVersion)) {
            $needsRebuild = $false
            Write-OK "Runtime already at $HeadroomVersion — skipping rebuild"
        } else {
            Write-Info "Runtime is '$installedHR', target is $HeadroomVersion — rebuilding"
        }
    } catch { Write-Info "Runtime probe failed — rebuilding" }
}

# Always rewrite shim files (idempotent, trivially cheap)
Write-Info "Writing package-spec.txt..."
Invoke-Maybe {
    Set-Content -LiteralPath $PackageSpec -Encoding ASCII -Value $HeadroomSpec
} "write package-spec.txt ($HeadroomSpec)"

Write-Info "Writing headroom.cmd..."
Invoke-Maybe {
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

function Resolve-BasePython {
    foreach ($c in @(
        @{cmd="py";    args=@("-3.13","-c","import sys;print(sys.executable)")},
        @{cmd="py";    args=@("-3",   "-c","import sys;print(sys.executable)")},
        @{cmd="python";args=@(        "-c","import sys;print(sys.executable)")}
    )) {
        try {
            $out  = & $c.cmd @($c.args) 2>$null
            $last = ($out | Select-Object -Last 1)
            if ($null -ne $last) { $last = $last.Trim() }
            if ($LASTEXITCODE -eq 0 -and $last -and (Test-Path -LiteralPath $last)) { return $last }
        } catch { }
    }
    $fb = "$env:LOCALAPPDATA\Programs\Python\Python313\python.exe"
    if (Test-Path -LiteralPath $fb) { return $fb }
    throw "No runnable Python 3.13 found for headroom runtime rebuild."
}

function Reset-HeadroomRuntime {
    $base = Resolve-BasePython
    if (Test-Path -LiteralPath $runtime) { Remove-Item -LiteralPath $runtime -Recurse -Force }
    & $base -m venv $runtime
    & $runtimePython -m pip install --upgrade pip --quiet
    $spec = (Get-Content -LiteralPath $packageSpecPath -Raw).Trim()
    & $runtimePython -m pip install --upgrade --only-binary=:all: $spec
    if ($LASTEXITCODE -ne 0) { throw "headroom-ai install failed" }
}

function Test-HeadroomRuntime {
    if (-not (Test-Path -LiteralPath $runtimeHeadroom)) { return $false }
    & $runtimeHeadroom --version *> $null
    return ($LASTEXITCODE -eq 0)
}

function Test-ProxyReady {
    try {
        $r = Invoke-WebRequest -Uri "http://127.0.0.1:8787/readyz" -UseBasicParsing -TimeoutSec 2
        return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300)
    } catch { return $false }
}

if ($HeadroomArgs.Count -ge 2 -and
    $HeadroomArgs[0] -eq "shim" -and $HeadroomArgs[1] -eq "reset") {
    Reset-HeadroomRuntime; exit 0
}

if (-not (Test-HeadroomRuntime)) { Reset-HeadroomRuntime }

if ($HeadroomArgs.Count -ge 3 -and
    $HeadroomArgs[0] -eq "init" -and
    $HeadroomArgs[1] -eq "hook" -and
    $HeadroomArgs[2] -eq "ensure") {
    if (-not (Test-ProxyReady)) {
        Start-Process -FilePath "$root\run-proxy.cmd" `
            -WorkingDirectory $env:USERPROFILE -WindowStyle Hidden
    }
    exit 0
}

$env:HEADROOM_TELEMETRY         = "off"
$env:HEADROOM_REQUIRE_RUST_CORE = "false"
& $runtimeHeadroom @HeadroomArgs
exit $LASTEXITCODE
'@
} "write headroom-shim.ps1"

Write-Info "Writing run-proxy.cmd..."
Invoke-Maybe {
    Set-Content -LiteralPath $RunProxyCmd -Encoding ASCII -Value @'
@echo off
title Headroom Proxy (port 8787)
set HEADROOM_TELEMETRY=off
set HEADROOM_REQUIRE_RUST_CORE=false
set TRANSFORMERS_VERBOSITY=error
set TOKENIZERS_PARALLELISM=false
echo === Headroom Proxy launching at %date% %time% ===
echo.
call "%USERPROFILE%\.local\bin\headroom.cmd" proxy --port 8787 --host 127.0.0.1 --no-telemetry
set EXITCODE=%errorlevel%
echo.
echo === Headroom Proxy exited (exit code %EXITCODE%) ===
echo Press any key to close...
pause >nul
'@
} "write run-proxy.cmd"

# Ensure .local\bin on user PATH
$userPath  = [Environment]::GetEnvironmentVariable("Path","User")
$pathParts = $userPath -split ";" | Where-Object { $_ }
if ($pathParts -notcontains $RtkBinDir) {
    Write-Warn "$RtkBinDir not on user PATH — adding"
    Invoke-Maybe {
        [Environment]::SetEnvironmentVariable("Path","$RtkBinDir;$userPath","User")
    } "add $RtkBinDir to user PATH"
}

# Rebuild runtime if needed
if ($needsRebuild) {
    Invoke-Maybe {
        $basePy = $null
        foreach ($c in @(
            @{cmd="py";    args=@("-3.13","-c","import sys;print(sys.executable)")},
            @{cmd="py";    args=@("-3",   "-c","import sys;print(sys.executable)")},
            @{cmd="python";args=@(        "-c","import sys;print(sys.executable)")}
        )) {
            try {
                $out  = & $c.cmd @($c.args) 2>$null
                $last = ($out | Select-Object -Last 1)
                if ($null -ne $last) { $last = $last.Trim() }
                if ($LASTEXITCODE -eq 0 -and $last -and (Test-Path -LiteralPath $last)) {
                    $basePy = $last; break
                }
            } catch { }
        }
        if (-not $basePy) {
            $fb = "$env:LOCALAPPDATA\Programs\Python\Python313\python.exe"
            if (Test-Path -LiteralPath $fb) { $basePy = $fb }
        }
        if (-not $basePy) { Write-Fail "Cannot resolve Python 3.13 — skipping rebuild"; return }

        Write-Info "Base Python: $basePy ($(& $basePy --version 2>&1))"
        $runtimePy = "$HeadroomRuntime\Scripts\python.exe"

        if (Test-Path -LiteralPath $HeadroomRuntime) {
            Remove-Item -LiteralPath $HeadroomRuntime -Recurse -Force
        }
        & $basePy -m venv $HeadroomRuntime
        if ($LASTEXITCODE -ne 0) { Write-Fail "venv creation failed"; return }

        & $runtimePy -m pip install --upgrade pip --quiet

        Write-Info "Installing $HeadroomSpec (binary-only — wheel already verified)..."
        & $runtimePy -m pip install --only-binary=:all: $HeadroomSpec
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "headroom-ai wheel install failed. Version $HeadroomVersion may lack a cp313 Windows wheel."
            Write-Fail "Re-run the script — Phase 0 will re-query PyPI and pick a different version."
            return
        }

        if (-not (Test-Path -LiteralPath $runtimeHR)) {
            Write-Fail "headroom.exe missing after install: $runtimeHR"; return
        }
        Write-OK "Runtime headroom: $(& $runtimeHR --version 2>&1)"
    } "rebuild headroom runtime to $HeadroomVersion"
}

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 6 — RTK: install or skip if already at/above latest
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 6 — RTK (target: v$RtkVersion)"

$rtkExe     = "$RtkBinDir\rtk.exe"
$currentRtk = $null
try { $currentRtk = (& rtk --version 2>&1).Trim() } catch { }

if ($currentRtk -and (Test-VersionGte -a $currentRtk -b $RtkVersion)) {
    Write-OK "RTK already at or above v${RtkVersion}: $currentRtk — skipping download"
} else {
    Write-Info "Current RTK: '$currentRtk' — installing v${RtkVersion}..."
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

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 7 — Disable telemetry + configure all agent harnesses
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 7 — Disable telemetry + configure all agent harnesses"

# Refresh session PATH so rtk + headroom resolve
$env:Path = "$RtkBinDir;" +
            [Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
            [Environment]::GetEnvironmentVariable("Path","User")

# Disable RTK telemetry first — before any init that might phone home
Write-Info "Disabling RTK telemetry..."
Invoke-Maybe {
    rtk telemetry disable 2>&1 | ForEach-Object { Write-Info "  $_" }
    Write-OK "RTK telemetry disabled"
} "rtk telemetry disable"

# Disable Headroom telemetry: CLI + persist via setx
Write-Info "Disabling Headroom telemetry..."
Invoke-Maybe {
    headroom telemetry disable 2>&1 | ForEach-Object { Write-Info "  $_" }
    setx HEADROOM_TELEMETRY "off" | Out-Null
    Write-OK "Headroom telemetry disabled (CLI + HEADROOM_TELEMETRY=off persisted)"
} "headroom telemetry disable"

# Harness init writes to user-global config dirs, but some sub-commands
# (notably `rtk init --copilot`) also scaffold a project-relative `.github/`
# in the CURRENT directory. This script is machine/user scoped — it must not
# leave artifacts in whatever folder it happened to be launched from. Run the
# init block from $env:USERPROFILE so any stray relative writes land in HOME
# (harmless, and where user-global config belongs anyway), then restore.
Push-Location $env:USERPROFILE
try {
    # RTK harness init
    # NOTE: --copilot is intentionally excluded. VS Code Copilot has no
    # user-global instructions location (its only convention is a
    # project-level .github/copilot-instructions.md), so there is nothing
    # for it to do at machine scope — it would only create a stray
    # .github/ folder. Per-project Copilot setup, if wanted, is a separate
    # `rtk init --copilot` run inside that project.
    foreach ($flag in "--auto-patch","--codex") {
        Write-Info "rtk init -g $flag"
        Invoke-Maybe {
            rtk init -g $flag 2>&1 | ForEach-Object { Write-Info "  $_" }
        } "rtk init -g $flag"
    }
    Invoke-Maybe {
        rtk init --show 2>&1 | ForEach-Object { Write-Info "  $_" }
    } "rtk init --show"

    # Headroom harness init (copilot here targets the Copilot CLI, which
    # does have a user-global config — kept). The copilot target is fragile:
    # headroom-ai 0.20.x's `init copilot` does a plain json.loads on
    # ~/.copilot/config.json, which the Copilot CLI writes as JSONC
    # ("// This file is managed automatically."). That throws a JSONDecodeError
    # traceback. We don't rewrite Copilot's own managed file; instead, skip the
    # copilot init when the config is JSONC and surface a one-line warning, and
    # trap any other failure so it doesn't dump a Python traceback into the log.
    $copilotConfig = Join-Path $env:USERPROFILE ".copilot\config.json"
    foreach ($agent in "claude","codex","copilot") {
        if ($agent -eq "copilot" -and (Test-IsJsonc $copilotConfig)) {
            Write-Warn "Skipping 'headroom init -g copilot': $copilotConfig is JSONC (// comments)."
            Write-Warn "  headroom-ai $HeadroomVersion can't parse it (json.loads -> JSONDecodeError)."
            Write-Warn "  Workaround: temporarily strip the leading // comment lines from that file,"
            Write-Warn "  run 'headroom init -g copilot' by hand, then it's fine; or wait for a"
            Write-Warn "  headroom-ai release that handles JSONC (the 0.21.x line currently has no"
            Write-Warn "  Windows wheel, so this machine stays on $HeadroomVersion)."
            continue
        }
        Write-Info "headroom init -g $agent"
        Invoke-Maybe {
            try {
                headroom init -g $agent 2>&1 | ForEach-Object { Write-Info "  $_" }
                if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
                    Write-Warn "headroom init -g $agent exited $LASTEXITCODE (see lines above)"
                }
            } catch {
                Write-Warn "headroom init -g $agent failed: $($_.Exception.Message)"
            }
        } "headroom init -g $agent"
    }
} finally {
    Pop-Location
}

# Durable routing env vars
Write-Info "Setting ANTHROPIC_BASE_URL + OPENAI_BASE_URL via setx..."
Invoke-Maybe {
    setx ANTHROPIC_BASE_URL "http://127.0.0.1:$ProxyPort"    | Out-Null
    setx OPENAI_BASE_URL    "http://127.0.0.1:$ProxyPort/v1" | Out-Null
    Write-OK "Routing env vars set (effective in new shells/agents)"
} "setx routing vars"

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 8 — Desktop + Startup shortcuts
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 8 — Desktop and Startup shortcuts"

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

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 9 — Start Headroom proxy
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 9 — Start Headroom proxy"

Invoke-Maybe {
    Stop-PortListener -port $ProxyPort | Out-Null
    $env:HEADROOM_TELEMETRY         = "off"
    $env:HEADROOM_REQUIRE_RUST_CORE = "false"
    Start-Process -FilePath $RunProxyCmd -WorkingDirectory $env:USERPROFILE -WindowStyle Normal
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
    else        { Write-Warn "Proxy did not respond to /readyz — check the console window" }
} "start headroom proxy"

# ══════════════════════════════════════════════════════════════════════════════
# PHASE 10 — Final verification
# ══════════════════════════════════════════════════════════════════════════════
Write-Step "PHASE 10 — Final verification"

$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
            [Environment]::GetEnvironmentVariable("Path","User")

Write-Info ""
Write-Info "── Target versions resolved this run ──────────────────────"
Write-Info "  headroom-ai : $HeadroomVersion  (wheel-installable on Windows)"
Write-Info "  RTK         : $RtkVersion"
Write-Info "  Python      : $(if ($PythonLatest) { $PythonLatest } else { '(Install Manager managed)' })"

Write-Info ""
Write-Info "── Python ─────────────────────────────────────────────────"
try {
    Write-Info "  where    : $((where.exe python 2>$null) -join ' | ')"
    Write-Info "  python   : $(python -c 'import sys,encodings; print(sys.executable,"|",sys.version.split()[0])' 2>&1)"
    Write-Info "  pip      : $(python -m pip --version 2>&1)"
    py -0p 2>&1 | ForEach-Object { Write-Info "  $_" }
} catch { Write-Warn "python probe: $_" }

try {
    $pyListFinal = & py list 2>&1
    if ($pyListFinal -match "WARNING.*legacy") {
        # Only warn if Python is also out of date; if current, just note it at INFO level
        $currentPyVer = ((python --version 2>&1) -replace 'Python ').Trim()
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

Write-Info ""
Write-Info "── RTK ────────────────────────────────────────────────────"
try {
    Write-Info "  version  : $(rtk --version 2>&1)"
    rtk gain 2>&1 | Select-Object -First 5 | ForEach-Object { Write-Info "  $_" }
    rtk init --show 2>&1 | ForEach-Object { Write-Info "  $_" }
} catch { Write-Warn "rtk probe: $_" }

Write-Info ""
Write-Info "── Headroom (shim) ────────────────────────────────────────"
try {
    Write-Info "  version  : $(Get-HeadroomVersion)"
    # 'No deployment profile named default' is expected when using Startup shortcut (not a service)
    $hsStatus = headroom install status 2>&1
    $hsStatus | Where-Object { $_ -notmatch "No deployment profile" } |
        ForEach-Object { Write-Info "    $_" }
    if ($hsStatus -match "No deployment profile") {
        Write-Info "    (no managed service profile — using Startup shortcut, which is correct)"
    }
} catch { Write-Warn "headroom probe: $_" }

Write-Info ""
Write-Info "── Proxy endpoints ────────────────────────────────────────"
foreach ($ep in "/livez","/readyz","/health","/stats") {
    try {
        $resp = Invoke-RestMethod "http://127.0.0.1:$ProxyPort$ep" -TimeoutSec 4 -ErrorAction Stop
        switch ($ep) {
            "/livez"  { Write-OK "/livez  : status=$($resp.status) alive=$($resp.alive) version=$($resp.version)" }
            "/readyz" { Write-OK "/readyz : status=$($resp.status) ready=$($resp.ready) version=$($resp.version) rust_core=$($resp.rust_core)" }
            "/health" { Write-OK "/health : status=$($resp.status) version=$($resp.version) rust_core=$($resp.rust_core)" }
            "/stats"  {
                # Extract scalar fields only — avoids JSON depth truncation on nested objects
                Write-OK "/stats  : requests_total=$($resp.requests_total) tokens_saved_total=$($resp.tokens_saved_total) cache_hits=$($resp.cache_hits) uptime_seconds=$($resp.uptime_seconds)"
            }
        }
    } catch { Write-Warn "${ep} : $($_.Exception.Message)" }
}

Write-Info ""
Write-Info "── Routing + telemetry env vars (user scope) ──────────────"
Write-Info "  ANTHROPIC_BASE_URL  : $([Environment]::GetEnvironmentVariable('ANTHROPIC_BASE_URL','User'))"
Write-Info "  OPENAI_BASE_URL     : $([Environment]::GetEnvironmentVariable('OPENAI_BASE_URL','User'))"
Write-Info "  HEADROOM_TELEMETRY  : $([Environment]::GetEnvironmentVariable('HEADROOM_TELEMETRY','User'))"

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host @"

Pass criteria:
  python   -> resolves to intended runtime, import encodings OK
  pip      -> works
  rtk      -> v$RtkVersion or newer, telemetry disabled
  headroom -> v$HeadroomVersion (wheel-installable, shim runtime), telemetry disabled
  /livez + /readyz -> healthy
  /health  -> ready (rust_core:disabled is OK)
  /stats   -> counters visible
  ANTHROPIC_BASE_URL + OPENAI_BASE_URL -> set via setx
  HEADROOM_TELEMETRY -> off

Action required after this script:
  Restart Claude Code, Codex, and any IDE so they pick up the new setx vars.
"@
