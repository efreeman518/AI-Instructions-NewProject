#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Set-DefenderExclusions.ps1 - Windows Defender exclusions for .NET developer workstations

.DESCRIPTION
    Adds path and process exclusions to Windows Defender to prevent excessive CPU usage
    during builds, package restores, and Docker operations. Also sets scan schedule and
    CPU cap to reduce background impact.

    Safe to re-run - duplicate exclusions are ignored by Defender.

.PARAMETER SourceRoot
    Root folder for all source code / git repos.
    Default: C:\Users\<current user>\source

.PARAMETER AdditionalSourcePaths
    Any extra source/project folders outside SourceRoot.
    Default: empty

.PARAMETER DockerDataRoot
    Docker Desktop data root. Default: C:\ProgramData\DockerDesktop

.PARAMETER ScanCpuCap
    Maximum CPU % Defender may use during scans. Default: 20

.PARAMETER ScanDay
    Day of week for scheduled full scan. Default: Saturday

.PARAMETER ScanTime
    Time for scheduled scan (HH:mm). Default: 03:00

.PARAMETER WhatIf
    Show what would be added without making changes.

.EXAMPLE
    .\Set-DefenderExclusions.ps1

.EXAMPLE
    .\Set-DefenderExclusions.ps1 -SourceRoot "D:\code" -ScanCpuCap 15

.EXAMPLE
    .\Set-DefenderExclusions.ps1 -WhatIf
#>

param(
    [string]   $SourceRoot            = "C:\Users\$env:USERNAME\source",
    [string[]] $AdditionalSourcePaths = @(),
    [string]   $DockerDataRoot        = "C:\ProgramData\DockerDesktop",
    [int]      $ScanCpuCap            = 20,
    [ValidateSet("Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Everyday")]
    [string]   $ScanDay               = "Saturday",
    [string]   $ScanTime              = "03:00",
    [switch]   $WhatIf
)

$ErrorActionPreference = "SilentlyContinue"

function Write-Header($title) {
    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan
}

function Add-PathExclusion($path, $label) {
    if (-not (Test-Path $path)) {
        Write-Host "  [SKIP]   $label - path not found: $path" -ForegroundColor DarkGray
        return
    }
    if ($WhatIf) {
        Write-Host "  [WHATIF] Would exclude path: $path" -ForegroundColor Yellow
    } else {
        Add-MpPreference -ExclusionPath $path -ErrorAction SilentlyContinue
        Write-Host "  [OK]     $label" -ForegroundColor Green
        Write-Host "           $path" -ForegroundColor DarkGray
    }
}

function Add-ProcessExclusion($process, $label) {
    if ($WhatIf) {
        Write-Host "  [WHATIF] Would exclude process: $process" -ForegroundColor Yellow
    } else {
        Add-MpPreference -ExclusionProcess $process -ErrorAction SilentlyContinue
        Write-Host "  [OK]     $label ($process)" -ForegroundColor Green
    }
}

function Add-ExtensionExclusion($ext, $label) {
    if ($WhatIf) {
        Write-Host "  [WHATIF] Would exclude extension: $ext" -ForegroundColor Yellow
    } else {
        Add-MpPreference -ExclusionExtension $ext -ErrorAction SilentlyContinue
        Write-Host "  [OK]     $label (*$ext)" -ForegroundColor Green
    }
}

$user = $env:USERNAME

Write-Host ""
Write-Host "  Defender Exclusions - .NET Developer Setup" -ForegroundColor White
Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  |  $env:COMPUTERNAME" -ForegroundColor DarkGray
if ($WhatIf) {
    Write-Host "  MODE: WhatIf - no changes will be made" -ForegroundColor Yellow
}


# --- 1. SOURCE CODE -----------------------------------------------------------
Write-Header "1/6  Source Code Paths"

Add-PathExclusion $SourceRoot "Source root"

foreach ($extra in $AdditionalSourcePaths) {
    Add-PathExclusion $extra "Additional source path"
}

# Common adjacent dev folders that may exist
foreach ($folder in @("repos","projects","dev","git","work","code")) {
    $path = "C:\Users\$user\$folder"
    if (Test-Path $path) {
        Add-PathExclusion $path "Dev folder: $folder"
    }
}


# --- 2. BUILD TOOLS & CACHES -------------------------------------------------
Write-Header "2/6  Build Tools & Package Caches"

# .NET / NuGet
Add-PathExclusion "C:\Program Files\dotnet"                                    ".NET SDK"
Add-PathExclusion "C:\Users\$user\.nuget"                                      "NuGet package cache"
Add-PathExclusion "C:\Users\$user\AppData\Local\NuGet"                         "NuGet local cache"
Add-PathExclusion "C:\Users\$user\AppData\Roaming\NuGet"                       "NuGet roaming cache"

# Visual Studio
Add-PathExclusion "C:\Users\$user\AppData\Local\Microsoft\VisualStudio"        "Visual Studio cache"
Add-PathExclusion "C:\Users\$user\AppData\Local\Temp\VSFeedbackIntelliCodeLogs" "VS IntelliCode logs"
Add-PathExclusion "C:\ProgramData\Microsoft\VisualStudio"                       "VS shared data"

# MSBuild outputs - exclude common build output folder names within source root
Add-PathExclusion "$SourceRoot\**\bin"                                         "Build outputs (bin)"
Add-PathExclusion "$SourceRoot\**\obj"                                         "Build intermediates (obj)"

# VS Code
Add-PathExclusion "C:\Users\$user\.vscode"                                     "VS Code user extensions"
Add-PathExclusion "C:\Users\$user\AppData\Roaming\Code"                        "VS Code app data"
Add-PathExclusion "C:\Users\$user\AppData\Local\Programs\Microsoft VS Code"    "VS Code installation"

# Node / npm
Add-PathExclusion "C:\Users\$user\AppData\Roaming\npm"                         "npm global packages"
Add-PathExclusion "C:\Users\$user\AppData\Local\npm-cache"                     "npm cache"
Add-PathExclusion "C:\Users\$user\AppData\Local\node_modules"                  "node_modules (local)"

# Python
Add-PathExclusion "C:\Users\$user\AppData\Local\pip"                           "pip cache"
Add-PathExclusion "C:\Python314"                                                "Python 3.14 (system)"

# Git
Add-PathExclusion "C:\Program Files\Git"                                        "Git installation"


# --- 3. DOCKER ----------------------------------------------------------------
Write-Header "3/6  Docker"

Add-PathExclusion "C:\Users\$user\AppData\Local\Docker"                        "Docker Desktop user data"
Add-PathExclusion "C:\ProgramData\Docker"                                       "Docker system data"
Add-PathExclusion $DockerDataRoot                                               "Docker Desktop root"
Add-PathExclusion "C:\Program Files\Docker"                                    "Docker Desktop installation"

# WSL (Docker uses WSL2 backend)
$wslPackages = Get-ChildItem "C:\Users\$user\AppData\Local\Packages" -Directory -ErrorAction SilentlyContinue |
               Where-Object { $_.Name -like "CanonicalGroupLimited*" -or $_.Name -like "*Ubuntu*" -or $_.Name -like "*Debian*" }
foreach ($pkg in $wslPackages) {
    Add-PathExclusion $pkg.FullName "WSL distro: $($pkg.Name)"
}

# WSL vhdx files specifically
Add-PathExclusion "C:\Users\$user\AppData\Local\Packages\CanonicalGroupLimited.Ubuntu*\LocalState" "WSL Ubuntu state"


# --- 4. ANDROID / MOBILE DEV -------------------------------------------------
Write-Header "4/6  Android & Mobile Dev"

Add-PathExclusion "C:\Users\$user\.android"                                    "Android SDK / AVD"
Add-PathExclusion "C:\Users\$user\AppData\Local\Android"                       "Android Studio data"


# --- 5. OTHER DEV TOOLS ------------------------------------------------------
Write-Header "5/6  Other Dev Tools & AI"

Add-PathExclusion "C:\Users\$user\.ollama"                                     "Ollama models"
Add-PathExclusion "C:\azurite"                                                  "Azurite Azure storage"
Add-PathExclusion "C:\Users\$user\AppData\Roaming\azuredatastudio"             "Azure Data Studio"
Add-PathExclusion "C:\Users\$user\.dotnet"                                     ".NET user tools"
Add-PathExclusion "C:\Users\$user\AppData\Local\GitCredentialManager"          "Git Credential Manager"
Add-PathExclusion "C:\Users\$user\AppData\Local\LinqPad"                       "LINQPad cache"


# --- PROCESS EXCLUSIONS -------------------------------------------------------
Write-Header "5b/6  Process Exclusions"

$processes = @(
    @{ Name = "devenv.exe";              Label = "Visual Studio" },
    @{ Name = "MSBuild.exe";             Label = "MSBuild" },
    @{ Name = "VBCSCompiler.exe";        Label = "Roslyn compiler" },
    @{ Name = "dotnet.exe";              Label = ".NET CLI" },
    @{ Name = "code.exe";                Label = "VS Code" },
    @{ Name = "node.exe";                Label = "Node.js" },
    @{ Name = "docker.exe";              Label = "Docker CLI" },
    @{ Name = "com.docker.service";      Label = "Docker service" },
    @{ Name = "Docker Desktop.exe";      Label = "Docker Desktop" },
    @{ Name = "dockerd.exe";             Label = "Docker daemon" },
    @{ Name = "pwsh.exe";                Label = "PowerShell 7" },
    @{ Name = "powershell.exe";          Label = "PowerShell 5" },
    @{ Name = "git.exe";                 Label = "Git" },
    @{ Name = "ServiceHub.Host.exe";     Label = "VS ServiceHub" },
    @{ Name = "ServiceHub.SettingsHost.exe"; Label = "VS Settings host" },
    @{ Name = "ServiceHub.RoslynCodeAnalysisService.exe"; Label = "Roslyn analysis" },
    @{ Name = "Microsoft.ServiceHub.Controller.exe"; Label = "VS ServiceHub controller" },
    @{ Name = "PerfWatson2.exe";         Label = "VS perf monitor" },
    @{ Name = "vstest.console.exe";      Label = "VS test runner" },
    @{ Name = "testhost.exe";            Label = "Test host" },
    @{ Name = "npm.cmd";                 Label = "npm" },
    @{ Name = "python.exe";              Label = "Python" },
    @{ Name = "ollama.exe";              Label = "Ollama" }
)

foreach ($proc in $processes) {
    Add-ProcessExclusion $proc.Name $proc.Label
}


# --- 6. SCAN SCHEDULE & CPU CAP ----------------------------------------------
Write-Header "6/6  Scan Schedule & CPU Cap"

if ($WhatIf) {
    Write-Host "  [WHATIF] Would set scan CPU cap to $ScanCpuCap%" -ForegroundColor Yellow
    Write-Host "  [WHATIF] Would set scan schedule: $ScanDay at $ScanTime" -ForegroundColor Yellow
} else {
    # CPU cap during scans
    Set-MpPreference -ScanAvgCPULoadFactor $ScanCpuCap
    Write-Host "  [OK]     Scan CPU cap set to $ScanCpuCap%" -ForegroundColor Green

    # Schedule
    $dayMap = @{
        Sunday=0; Monday=1; Tuesday=2; Wednesday=3
        Thursday=4; Friday=5; Saturday=6; Everyday=8
    }
    $scanTimeSpan = [TimeSpan]::Parse($ScanTime)
    Set-MpPreference -ScanScheduleDay $dayMap[$ScanDay]
    Set-MpPreference -ScanScheduleTime $scanTimeSpan
    Write-Host "  [OK]     Scan scheduled: $ScanDay at $ScanTime" -ForegroundColor Green

    # Disable scanning of network drives (common in dev environments)
    Set-MpPreference -DisableScanningNetworkFiles $true
    Write-Host "  [OK]     Network file scanning disabled" -ForegroundColor Green

    # Reduce scan on removable drives
    Set-MpPreference -DisableRemovableDriveScanning $true
    Write-Host "  [OK]     Removable drive scanning disabled" -ForegroundColor Green
}


# --- SUMMARY -----------------------------------------------------------------
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Complete" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

if (-not $WhatIf) {
    $currentPaths = (Get-MpPreference).ExclusionPath
    $currentProcs = (Get-MpPreference).ExclusionProcess
    Write-Host "  Total path exclusions:    $($currentPaths.Count)" -ForegroundColor White
    Write-Host "  Total process exclusions: $($currentProcs.Count)" -ForegroundColor White
    Write-Host ""
    Write-Host "  To review all exclusions:" -ForegroundColor DarkGray
    Write-Host "    Get-MpPreference | Select-Object -ExpandProperty ExclusionPath" -ForegroundColor White
    Write-Host "    Get-MpPreference | Select-Object -ExpandProperty ExclusionProcess" -ForegroundColor White
    Write-Host ""
    Write-Host "  To remove a specific exclusion:" -ForegroundColor DarkGray
    Write-Host "    Remove-MpPreference -ExclusionPath 'C:\path\to\remove'" -ForegroundColor White
    Write-Host "    Remove-MpPreference -ExclusionProcess 'process.exe'" -ForegroundColor White
}
Write-Host ""
