param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$OutputFile = 'phase-load-packs.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $Root '_manifest.json'
$skillPath = Join-Path $Root 'SKILL.md'
$outputPath = Join-Path $Root $OutputFile

if (-not (Test-Path $manifestPath)) {
    throw "Manifest not found: $manifestPath"
}
if (-not (Test-Path $skillPath)) {
    throw "SKILL.md not found: $skillPath"
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$skillLines = Get-Content $skillPath -Encoding UTF8

function Get-ModeExclusions {
    param(
        [string[]]$Lines,
        [string]$Mode
    )

    $header = "### ``$Mode``"
    $start = -1

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].Trim() -eq $header) {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        return @()
    }

    $end = $Lines.Count - 1
    for ($i = $start + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i].Trim().StartsWith("In ``$Mode`` mode")) {
            $end = $i
            break
        }
    }

    $paths = @()
    for ($i = $start; $i -le $end; $i++) {
        $line = $Lines[$i]
        $matches = [regex]::Matches($line, '`([^`]+\.md)`')
        foreach ($match in $matches) {
            $candidate = $match.Groups[1].Value
            if ($candidate -like 'skills/*.md') {
                $paths += $candidate
            }
        }
    }

    return $paths | Sort-Object -Unique
}

$excludedLite = Get-ModeExclusions -Lines $skillLines -Mode 'lite'
$excludedApiOnly = Get-ModeExclusions -Lines $skillLines -Mode 'api-only'

$phaseOrder = @(
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
        skill = 'SKILL.md'
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
