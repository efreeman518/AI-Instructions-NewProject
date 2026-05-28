#Requires -Version 5.1
<#
.SYNOPSIS
  Idempotent setup: clean Python environment, update RTK and Headroom to
  LATEST stable versions (resolved at runtime), disable all telemetry, and
  configure all agent harnesses (Claude Code, Codex, Copilot).

  Safe to re-run at any time. headroom-ai version is resolved to the latest
  release that has a Windows-installable wheel - never triggers a source build.

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
$RtkBinDir       = "$env:USERPROFILE\.local\bin"      # stable user bin dir for rtk + headroom shims
$HeadroomRoot    = "$env:USERPROFILE\.headroom"       # headroom home: runtime, shim scripts, config
$HeadroomRuntime = "$HeadroomRoot\runtime"            # isolated Python venv for headroom-ai
$PackageSpec     = "$HeadroomRoot\package-spec.txt"   # records the pinned headroom-ai version
$RunProxyCmd     = "$HeadroomRoot\run-proxy.cmd"      # full proxy launcher (used by shortcuts)
$EnsureProxyCmd  = "$HeadroomRoot\headroom-proxy-ensure.cmd"  # lightweight hook (used by Copilot/Codex)
$HeadroomShim    = "$RtkBinDir\headroom.cmd"          # user-facing headroom command shim
$ShimPs1         = "$HeadroomRoot\headroom-shim.ps1"  # PowerShell backend for the shim

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
Write-Info "  headroom-ai : $HeadroomVersion  (wheel-installable on Windows)"
Write-Info "  RTK         : $RtkVersion"
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

# Finds a Python 3.13 interpreter for rebuilding the headroom runtime venv.
# Prefers py -3.13 (Install Manager) then py -3 then system python.
# Headroom-ai ships cp313 wheels; 3.13 is the safest base for binary installs.
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
    # Hard fallback to known install path
    $fb = "$env:LOCALAPPDATA\Programs\Python\Python313\python.exe"
    if (Test-Path -LiteralPath $fb) { return $fb }
    throw "No runnable Python 3.13 found for headroom runtime rebuild."
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
# Uses Python 3.13 as the base interpreter - headroom-ai ships cp313 binary wheels
# for Windows, so this avoids any MSVC source build requirement.
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
        if (-not $basePy) { Write-Fail "Cannot resolve Python 3.13 - skipping rebuild"; return }

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
            Write-Fail "headroom-ai wheel install failed. Version $HeadroomVersion may lack a cp313 Windows wheel."
            Write-Fail "Re-run the script - Phase 0 will re-query PyPI and pick a different version."
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
        Copy-Item -LiteralPath $rtkExe -Destination $inst -Force
        Write-OK "Synced: $inst"
    }
} "sync shadowing rtk locations"

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
Write-Info "  headroom-ai : $HeadroomVersion  (wheel-installable on Windows)"
Write-Info "  RTK         : $RtkVersion"
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
  python   -> resolves to intended runtime, import encodings OK
  pip      -> works
  rtk      -> v$RtkVersion or newer, telemetry disabled
  headroom -> v$HeadroomVersion (wheel-installable, shim runtime), telemetry disabled
  /livez + /readyz -> healthy
  /health  -> ready (rust_core:disabled is OK on Python 3.13 without Rust wheel)
  /stats   -> counters visible
  ANTHROPIC_BASE_URL + OPENAI_BASE_URL -> set via setx
  HEADROOM_TELEMETRY -> off
  HEADROOM_ENSURE_CMD -> points to lightweight proxy-ensure.cmd

Action required after this script:
  Restart Claude Code, Codex, and any IDE so they pick up the new setx vars.
"@