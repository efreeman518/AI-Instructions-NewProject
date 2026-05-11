# Clean Python on Windows

Use this runbook when a Windows machine has stale Python installs, broken launchers,
or repo-specific workarounds that make AI-agent scripts unreliable across
repositories.

Goal: one current Python that works from any repo, plus only the older runtimes
that are intentionally required by projects. Do not depend on a repo `.venv` as
the machine's Python launcher.

## Sources to Check First

- Latest Windows releases: <https://www.python.org/downloads/windows/>
- Windows install and launcher behavior: <https://docs.python.org/3/using/windows.html>

Use `[latest]` below as a placeholder for the latest stable Windows release
shown on python.org. Replace it only at execution time, after checking the
download page.

## Target State

Choose one supported setup:

1. **User-global Python Install Manager**: preferred for a single developer
   account. Works across all repos for that user. It installs and updates
   runtimes through `py` / `pymanager`.
2. **Machine-wide traditional CPython install**: use when all users, services,
   CI agents, or admin-managed shells need the same Python. Prefer a no-space
   target like `C:\Python[latest]` for scripted installs to avoid quoting mistakes.

Either setup is acceptable. Avoid leaving both fighting for `python`, `py`, and
`pip`.

## Inventory Current State

Run from a fresh PowerShell session. Use Administrator only when inspecting or
changing machine-wide state.

```powershell
$ErrorActionPreference = "Continue"

"Commands"
where.exe python
where.exe py
Get-Command python, py, pip -All

"Runtime probes"
python --version
python -c "import sys, encodings; print(sys.executable); print(sys.prefix); print(sys.version)"
python -m pip --version
py -0p
py -3 -c "import sys; print(sys.executable); print(sys.prefix)"

"PATH"
[Environment]::GetEnvironmentVariable("Path", "User") -split ";"
[Environment]::GetEnvironmentVariable("Path", "Machine") -split ";"

"Python registry"
Get-ChildItem -Recurse HKCU:\Software\Python -ErrorAction SilentlyContinue
Get-ChildItem -Recurse HKLM:\Software\Python -ErrorAction SilentlyContinue

"Common install folders"
Get-ChildItem "$env:LocalAppData\Programs\Python" -ErrorAction SilentlyContinue
Get-ChildItem "$env:LocalAppData\Python" -ErrorAction SilentlyContinue
Get-ChildItem "$env:AppData\Python" -ErrorAction SilentlyContinue
Get-ChildItem "C:\Python*" -ErrorAction SilentlyContinue
Get-ChildItem "C:\Program Files\Python*" -ErrorAction SilentlyContinue
```

If `python` opens the Microsoft Store or resolves to
`%LocalAppData%\Microsoft\WindowsApps\python.exe`, use **Manage App Execution
Aliases** to disable stale Python aliases, or put the intended real Python
directory earlier on PATH. Do not delete the WindowsApps directory.

## Remove Launcher Pins and Bad Environment Overrides

Old machines often have `py.ini` or environment variables forcing `py` to an old
minor version. Inspect these before reinstalling.

```powershell
$pyIniCandidates = @(
  "$env:LocalAppData\py.ini",
  "$env:AppData\py.ini",
  "C:\Windows\py.ini"
)

$launcherInis = Get-Command py -All -ErrorAction SilentlyContinue |
  ForEach-Object { Join-Path (Split-Path $_.Source) "py.ini" }

($pyIniCandidates + $launcherInis) |
  Sort-Object -Unique |
  ForEach-Object {
    if (Test-Path -LiteralPath $_) {
      "== $_"
      Get-Content -LiteralPath $_
    }
  }

"Python-related environment variables"
"User", "Machine" | ForEach-Object {
  $scope = $_
  "[$scope]"
  "PY_PYTHON", "PY_PYTHON3", "PYTHONHOME", "PYTHONPATH", "PYTHON_MANAGER_DEFAULT" |
    ForEach-Object { "$_=$([Environment]::GetEnvironmentVariable($_, $scope))" }
}
```

Cleanup rules:

- If `py.ini` pins `[defaults] python=3.9` or similar and no project needs it,
  back it up and remove it.
- If `PY_PYTHON`, `PY_PYTHON3`, or `PYTHON_MANAGER_DEFAULT` pins an old version,
  clear it or update it.
- Clear `PYTHONHOME` and `PYTHONPATH` unless the machine intentionally embeds
  Python. These variables can break a good install and produce errors like
  `No module named encodings`.

```powershell
# Back up and remove stale py.ini files after inspection.
$stamp = Get-Date -Format "yyyyMMddHHmmss"
foreach ($file in $pyIniCandidates) {
  if (Test-Path -LiteralPath $file) {
    Copy-Item -LiteralPath $file -Destination "$file.bak-$stamp"
    Remove-Item -LiteralPath $file
  }
}

# Clear user-level pins. Use "Machine" instead of "User" only from an elevated shell.
[Environment]::SetEnvironmentVariable("PY_PYTHON", $null, "User")
[Environment]::SetEnvironmentVariable("PY_PYTHON3", $null, "User")
[Environment]::SetEnvironmentVariable("PYTHONHOME", $null, "User")
[Environment]::SetEnvironmentVariable("PYTHONPATH", $null, "User")
[Environment]::SetEnvironmentVariable("PYTHON_MANAGER_DEFAULT", $null, "User")
```

## Uninstall Old or Broken Installs

Prefer normal uninstall paths first:

- Settings -> Apps -> Installed apps -> remove old Python runtimes and Python
  Launcher entries.
- For Python Install Manager runtimes:

```powershell
py list
py uninstall [old]
# Only when intentionally removing every install-manager runtime:
py uninstall --purge
```

Find traditional installer entries:

```powershell
$uninstallRoots = @(
  "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
  "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
  "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
)

foreach ($root in $uninstallRoots) {
  Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue |
    ForEach-Object {
      $item = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
      if ($item.DisplayName -like "Python *") {
        [pscustomobject]@{
          Key = $_.PSChildName
          Name = $item.DisplayName
          Version = $item.DisplayVersion
          Location = $item.InstallLocation
          Uninstall = $item.UninstallString
        }
      }
    }
}
```

Use the listed uninstallers. Do not delete install folders first; that can leave
Windows Installer believing files are present when they are not.

## Install User-Global Python with Python Install Manager

Use this for normal developer workstations.

```powershell
# Install Python Install Manager with winget.
winget install 9NQ7512CXL7T -e --accept-package-agreements --accept-source-agreements --disable-interactivity

# Refresh shell, then configure and install the latest stable Python 3 runtime.
py install --configure -y
py list --online [latest]
py install [latest]
py install --update
py install --refresh
```

If `py` is still owned by the legacy launcher, use `pymanager` for the same
commands or uninstall the old **Python Launcher** entry from Installed apps.

## Install Machine-Wide Python with the Traditional Installer

Use this for shared build agents, service accounts, or machines where all users
need the same Python. Download the latest stable 64-bit Windows installer from
python.org.

Run in Administrator PowerShell. Replace `[latest]` with the version you
downloaded.

```powershell
$installer = "$env:TEMP\python-[latest]-amd64.exe"
$target = "C:\Python[latest]"

$args = @(
  "/quiet",
  "InstallAllUsers=1",
  "TargetDir=$target",
  "PrependPath=1",
  "Include_launcher=1",
  "InstallLauncherAllUsers=1",
  "Include_exe=1",
  "Include_lib=1",
  "Include_pip=1",
  "Include_tcltk=0",
  "Include_test=0",
  "Include_dev=0",
  "Include_doc=0"
)

$p = Start-Process -FilePath $installer -ArgumentList $args -Wait -PassThru
$p.ExitCode
```

Lessons learned:

- Include `Include_lib=1`. If `Lib\encodings` is missing, Python can print a
  version but fail on real imports.
- Prefer `C:\Python[latest]` for unattended installs. A badly quoted
  `C:\Program Files\...` target can create an invalid `C:\Program` install.
- If `pip` is missing after install, repair with:

```powershell
C:\Python[latest]\python.exe -m ensurepip --upgrade
C:\Python[latest]\python.exe -m pip --version
```

If the all-users `py.exe` launcher does not land on PATH, make sure the real
Python directory is on Machine PATH and use `python` or direct paths. As a last
resort, copy the official `py.exe` / `pyw.exe` produced by the installer into
the same machine directory that is already on Machine PATH.

## Repair PATH

For user-global Python Install Manager, PATH should usually include:

```text
%LocalAppData%\Microsoft\WindowsApps
%LocalAppData%\Python\bin
```

For traditional machine-wide Python, Machine PATH should include:

```text
C:\Python[latest]\Scripts\
C:\Python[latest]\
```

Set Machine PATH carefully:

```powershell
$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$wanted = @("C:\Python[latest]\Scripts\", "C:\Python[latest]\")
$parts = @($wanted + ($machinePath -split ";" | Where-Object { $_ -and $_ -notin $wanted }))
[Environment]::SetEnvironmentVariable("Path", ($parts -join ";"), "Machine")
```

Restart terminals and IDEs after PATH changes. Long-lived agent terminals often
retain stale PATH until restarted.

## Remove Orphan Folders Only After Verification

After uninstalling and repairing PATH, inspect common orphan locations:

```powershell
$candidates = @(
  "C:\Program",
  "C:\Python[old]"
)

$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")

foreach ($candidate in $candidates) {
  if (Test-Path -LiteralPath $candidate) {
    "== $candidate"
    "PATH reference: $($machinePath.Contains($candidate) -or $userPath.Contains($candidate))"
    Get-ChildItem -LiteralPath $candidate -Force
  }
}
```

Only delete an orphan when all of these are true:

- no PATH entry references it,
- no `HKCU:\Software\Python` or `HKLM:\Software\Python` install path references
  it,
- the folder contents are clearly Python-only,
- no active process is running from it.

Deletion example, after explicit confirmation:

```powershell
Remove-Item -LiteralPath "C:\Program" -Recurse -Force
```

## Final Verification

Run from `C:\tmp` or another neutral directory, not from a repo.

```powershell
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [Environment]::GetEnvironmentVariable("Path", "User")

Set-Location C:\tmp

where.exe python
where.exe py

python -c "import sys, encodings, pathlib; print(sys.executable); print(sys.prefix); print(pathlib.Path.cwd()); print(sys.version)"
python -m pip --version

py -0p
py -3 -c "import sys; print(sys.executable); print(sys.prefix)"
```

Pass criteria:

- `python` resolves to the intended user-global or machine-wide runtime.
- `py` resolves to the intended install manager or launcher, or is intentionally
  absent and not required.
- `import encodings` succeeds.
- `python -m pip --version` succeeds.
- Verification works outside any repo and without activating `.venv`.

## Recreate Project Virtual Environments After Cleanup

Existing `.venv` directories may point to an old base interpreter. Recreate them
per repo only after the global/user Python is healthy.

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe --version
```

Do not use a repo `.venv` as the system cleanup proof. It is only a project
consumer of the machine/user-global Python.
