param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$OutputFile = 'phase-load-packs.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $Root '_manifest.json'
$outputPath = Join-Path $Root $OutputFile

if (-not (Test-Path $manifestPath)) {
    throw "Manifest not found: $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

if (-not ($manifest.PSObject.Properties.Name -contains 'modeExclusions')) {
    throw 'Manifest is missing top-level modeExclusions metadata.'
}

$manifestFileSet = @{}
foreach ($entry in $manifest.files) {
    $manifestFileSet[[string]$entry.path] = $true
}

function Get-ModeExclusions {
    param(
        [psobject]$Manifest,
        [string]$Mode,
        [hashtable]$ManifestFileSet
    )

    if (-not ($Manifest.modeExclusions.PSObject.Properties.Name -contains $Mode)) {
        throw "Manifest modeExclusions is missing '$Mode'."
    }

    $paths = @($Manifest.modeExclusions.$Mode | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) | Sort-Object -Unique
    foreach ($path in $paths) {
        if (-not $ManifestFileSet.ContainsKey([string]$path)) {
            throw "Mode exclusion '$path' for mode '$Mode' does not exist in manifest.files."
        }
    }

    return $paths
}

$excludedLite = Get-ModeExclusions -Manifest $manifest -Mode 'lite' -ManifestFileSet $manifestFileSet
$excludedApiOnly = Get-ModeExclusions -Manifest $manifest -Mode 'api-only' -ManifestFileSet $manifestFileSet

$phaseOrder = @(
    'metadata',
    'session-bootstrap',
    'phase-1',
    'phase-2',
    'phase-3',
    'phase-4-base',
    'phase-4a',
    'phase-4a-optional',
    'phase-4b',
    'phase-4c',
    'phase-4d',
    'phase-4d-optional',
    'phase-4e',
    'phase-4e-optional',
    'phase-4f',
    'phase-4g',
    'support-only',
    'on-demand'
)

$grouped = @{}
foreach ($phase in $phaseOrder) {
    $grouped[$phase] = @()
}

foreach ($entry in $manifest.files) {
    $phase = [string]$entry.phase
    if (-not $grouped.ContainsKey($phase)) {
        $grouped[$phase] = @()
    }

    $grouped[$phase] += [string]$entry.path
}

foreach ($phase in @($grouped.Keys)) {
    $grouped[$phase] = @($grouped[$phase] | Select-Object -Unique)
}

function Get-ModePack {
    param(
        [hashtable]$Base,
        [string[]]$ExcludedSkills
    )

    $result = [ordered]@{}
    foreach ($phase in $Base.Keys) {
        $paths = @($Base[$phase])

        if ($ExcludedSkills.Count -gt 0) {
            $paths = @($paths | Where-Object { $_ -notin $ExcludedSkills })
        }

        $result[$phase] = $paths
    }

    return $result
}

$fullPack = Get-ModePack -Base $grouped -ExcludedSkills @()
$litePack = Get-ModePack -Base $grouped -ExcludedSkills $excludedLite
$apiOnlyPack = Get-ModePack -Base $grouped -ExcludedSkills $excludedApiOnly

$output = [ordered]@{
    source = [ordered]@{
        manifest = '_manifest.json'
        generator = 'scripts/generate-phase-load-packs.ps1'
    }
    contextBudget = $manifest.contextBudget
    phaseOrder = $phaseOrder
    modeExclusions = [ordered]@{
        lite = $excludedLite
        'api-only' = $excludedApiOnly
    }
    packs = [ordered]@{
        full = $fullPack
        lite = $litePack
        'api-only' = $apiOnlyPack
    }
}

$json = $output | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($outputPath, $json, [System.Text.UTF8Encoding]::new($false))

Write-Output "Generated $OutputFile"
