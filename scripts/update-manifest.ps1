<#
.SYNOPSIS
    Updates _manifest.json token estimates by scanning all .md files.

.DESCRIPTION
    1. Scans all .md files (excluding sample-app/).
    2. Computes ceil(chars / 4) per file.
    3. Reads _manifest.json, updates estimatedTokens for matching paths.
    4. Adds new entries for untracked .md files with "phase": "unknown".
    5. Updates totalEstimatedTokens.
    6. Writes back with consistent formatting.
    7. Reports summary.

.EXAMPLE
    .\scripts\update-manifest.ps1
#>

param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $Root '_manifest.json'
if (-not (Test-Path $manifestPath)) {
    Write-Error "Manifest not found at $manifestPath"
    return
}

# Load manifest
$json = Get-Content $manifestPath -Raw -Encoding UTF8
$manifest = $json | ConvertFrom-Json

# Build lookup of existing entries by path
$fileEntries = @{}
foreach ($entry in $manifest.files) {
    $fileEntries[$entry.path] = $entry
}

# Scan .md files (exclude sample-app)
$mdFiles = Get-ChildItem -Path $Root -Filter '*.md' -Recurse |
    Where-Object { $_.FullName -notmatch [regex]::Escape((Join-Path $Root 'sample-app')) } |
    Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' }

$updated = 0
$added = 0

foreach ($file in $mdFiles) {
    $relativePath = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $chars = $content.Length
    $tokens = [int][Math]::Ceiling($chars / 4)

    if ($fileEntries.ContainsKey($relativePath)) {
        $entry = $fileEntries[$relativePath]
        if ([int]$entry.estimatedTokens -ne $tokens) {
            $entry.estimatedTokens = $tokens
            $updated++
        }
    }
    else {
        # Add new entry
        $newEntry = [PSCustomObject]@{
            path            = $relativePath
            phase           = 'unknown'
            estimatedTokens = $tokens
        }
        $manifest.files += $newEntry
        $fileEntries[$relativePath] = $newEntry
        $added++
    }
}

# Also update _manifest.json's own token estimate
$manifestRelative = '_manifest.json'
if ($fileEntries.ContainsKey($manifestRelative)) {
    $manifestContent = Get-Content $manifestPath -Raw -Encoding UTF8
    $manifestTokens = [int][Math]::Ceiling($manifestContent.Length / 4)
    $entry = $fileEntries[$manifestRelative]
    if ($entry.estimatedTokens -ne $manifestTokens) {
        $entry.estimatedTokens = $manifestTokens
        $updated++
    }
}

# Compute total
[int]$total = 0
foreach ($entry in $manifest.files) {
    $entry.estimatedTokens = [int]$entry.estimatedTokens
    $total += $entry.estimatedTokens
}
$manifest.totalEstimatedTokens = $total

# Serialize and write back
$outputJson = $manifest | ConvertTo-Json -Depth 10
# Ensure UTF-8 without BOM
[System.IO.File]::WriteAllText($manifestPath, $outputJson, [System.Text.UTF8Encoding]::new($false))

Write-Host "Updated $updated files, added $added new entries, total tokens: $total"
