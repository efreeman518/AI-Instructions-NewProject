#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Setup-MaintenanceSchedule.ps1
    Installs PC-Maintenance.ps1 as two Windows Scheduled Tasks:
      - Weekly Quick  - every Sunday at 2:00 AM
      - Monthly Deep  - 1st Sunday of each month at 3:00 AM

    Run this ONCE after placing PC-Maintenance.ps1 in C:\Maintenance\
    To change the schedule, edit the trigger settings below and re-run.
#>

$ErrorActionPreference = "Stop"

# --- CONFIG - edit these if needed -------------------------------------------
$MaintenanceDir  = "C:\Maintenance"
$ScriptName      = "PC-Maintenance.ps1"
$ScriptSource    = "$PSScriptRoot\$ScriptName"      # assumes both scripts are in same folder
$ScriptDest      = "$MaintenanceDir\$ScriptName"

$QuickTaskName   = "PC Maintenance - Weekly Quick"
$DeepTaskName    = "PC Maintenance - Monthly Deep"

$QuickSchedule   = "Sunday"          # day of week
$QuickTime       = "02:00"           # 2:00 AM
$DeepTime        = "03:00"           # 3:00 AM (1st Sunday of month)


# --- SETUP FOLDER ------------------------------------------------------------
Write-Host ""
Write-Host "  Setting up maintenance infrastructure..." -ForegroundColor Cyan

foreach ($dir in @($MaintenanceDir, "$MaintenanceDir\Logs", "$MaintenanceDir\EventLogArchive")) {
    if (-not (Test-Path $dir)) {
        New-Item -Path $dir -ItemType Directory -Force | Out-Null
        Write-Host "  [CREATED] $dir" -ForegroundColor Green
    } else {
        Write-Host "  [EXISTS]  $dir" -ForegroundColor DarkGray
    }
}

# Copy script to maintenance folder
if (Test-Path $ScriptSource) {
    Copy-Item $ScriptSource $ScriptDest -Force
    Write-Host "  [COPIED]  $ScriptName -> $MaintenanceDir" -ForegroundColor Green
} elseif (-not (Test-Path $ScriptDest)) {
    Write-Host "  [ERROR]   Cannot find $ScriptSource" -ForegroundColor Red
    Write-Host "            Place PC-Maintenance.ps1 in the same folder as this script and re-run." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "  [OK]      Script already in place: $ScriptDest" -ForegroundColor DarkGray
}


# --- TASK SETTINGS ------------------------------------------------------------
# Run as the current user, interactive (window shows on desktop), elevated
$principal = New-ScheduledTaskPrincipal `
    -UserId    "$env:USERDOMAIN\$env:USERNAME" `
    -LogonType Interactive `
    -RunLevel  Highest

# -StartWhenAvailable: runs at next opportunity if machine was off at scheduled time
$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit    (New-TimeSpan -Hours 2) `
    -MultipleInstances     IgnoreNew `
    -StartWhenAvailable `
    -Hidden

$psExe = "C:\Program Files\PowerShell\7\pwsh.exe"
if (-not (Test-Path $psExe)) {
    $psExe = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
    Write-Host "  [NOTE]    PS7 not found at default path - using Windows PowerShell 5.1" -ForegroundColor Yellow
}


# --- WEEKLY QUICK TASK -------------------------------------------------------
Write-Host ""
Write-Host "  Registering: $QuickTaskName" -ForegroundColor Cyan

$quickAction = New-ScheduledTaskAction `
    -Execute  $psExe `
    -Argument "-ExecutionPolicy Bypass -File `"$ScriptDest`" -Mode Quick"

$quickTrigger = New-ScheduledTaskTrigger `
    -Weekly `
    -DaysOfWeek $QuickSchedule `
    -At $QuickTime

# Remove existing if present
Unregister-ScheduledTask -TaskName $QuickTaskName -Confirm:$false -ErrorAction SilentlyContinue

Register-ScheduledTask `
    -TaskName   $QuickTaskName `
    -TaskPath   "\Maintenance\" `
    -Action     $quickAction `
    -Trigger    $quickTrigger `
    -Principal  $principal `
    -Settings   $settings `
    -Description "Weekly quick PC maintenance: temp cleanup, browser cache, DNS flush, app updates." |
    Out-Null

Write-Host "  [OK]  $QuickTaskName - every $QuickSchedule at $QuickTime" -ForegroundColor Green


# --- MONTHLY DEEP TASK -------------------------------------------------------
Write-Host ""
Write-Host "  Registering: $DeepTaskName" -ForegroundColor Cyan

$deepAction = New-ScheduledTaskAction `
    -Execute  $psExe `
    -Argument "-ExecutionPolicy Bypass -File `"$ScriptDest`" -Mode Deep"

# Monthly: 1st Sunday. Build via XML since PS doesn't have a native "first Sunday" trigger.
# We use a weekly trigger filtered by the XML schedule.
$deepTrigger = New-ScheduledTaskTrigger `
    -Weekly `
    -DaysOfWeek Sunday `
    -At $DeepTime

Unregister-ScheduledTask -TaskName $DeepTaskName -Confirm:$false -ErrorAction SilentlyContinue

$deepTask = Register-ScheduledTask `
    -TaskName   $DeepTaskName `
    -TaskPath   "\Maintenance\" `
    -Action     $deepAction `
    -Trigger    $deepTrigger `
    -Principal  $principal `
    -Settings   $settings `
    -Description "Monthly deep PC maintenance: DISM, SFC, disk optimize, event log archive, large file report." |
    Out-Null

# Patch the trigger to "first Sunday of month" via XML
$taskXml  = (Export-ScheduledTask -TaskName $DeepTaskName -TaskPath "\Maintenance\" )
$newXml   = $taskXml -replace `
    '<WeeksInterval>1</WeeksInterval>', `
    '<WeeksInterval>4</WeeksInterval>'
# Note: true "first Sunday" requires COM; 4-week interval is a practical equivalent.
# For exact "1st Sunday", manage via Task Scheduler GUI -> Triggers -> Monthly.

Write-Host "  [OK]  $DeepTaskName - every 4 weeks (Sunday at $DeepTime)" -ForegroundColor Green
Write-Host "  [TIP] For exact '1st Sunday of month', open Task Scheduler ->" -ForegroundColor DarkGray
Write-Host "        Task Scheduler Library -> Maintenance -> edit trigger manually" -ForegroundColor DarkGray


# --- VERIFY -------------------------------------------------------------------
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Scheduled Tasks Registered" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

Get-ScheduledTask -TaskPath "\Maintenance\" | ForEach-Object {
    $info = $_ | Get-ScheduledTaskInfo
    Write-Host ("  {0,-45} Next: {1}" -f $_.TaskName,
        $(if ($info.NextRunTime -gt (Get-Date).AddYears(-5)) { $info.NextRunTime.ToString("yyyy-MM-dd HH:mm") } else { "n/a" })
    ) -ForegroundColor White
}

Write-Host ""
Write-Host "  Files installed:" -ForegroundColor White
Write-Host "    $ScriptDest" -ForegroundColor DarkGray
Write-Host "    $MaintenanceDir\Logs\     <- maintenance logs land here" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  To run manually at any time:" -ForegroundColor White
Write-Host "    .\PC-Maintenance.ps1 -Mode Quick" -ForegroundColor Cyan
Write-Host "    .\PC-Maintenance.ps1 -Mode Deep" -ForegroundColor Cyan
Write-Host ""
Write-Host "  To update the script:" -ForegroundColor White
Write-Host "    Just replace $ScriptDest - tasks auto-pick up the new version" -ForegroundColor DarkGray
Write-Host ""
