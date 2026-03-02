param(
    [Parameter(Mandatory = $true)]
    [string]$Phase,

    [ValidateSet('full', 'lite', 'api-only')]
    [string]$Mode = 'full',

    [switch]$IncludeGateway,
    [switch]$IncludeFunctionApp,
    [switch]$IncludeScheduler,
    [switch]$IncludeUnoUI,
    [switch]$IncludeAiServices,

    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packsPath = Join-Path $Root 'phase-load-packs.json'
$manifestPath = Join-Path $Root '_manifest.json'

if (-not (Test-Path $packsPath)) {
    & (Join-Path $Root 'scripts/generate-phase-load-packs.ps1') -Root $Root | Out-Null
}

$packs = Get-Content $packsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

if ($Phase -notin $packs.phaseOrder) {
    throw "Unknown phase '$Phase'."
}

$modePack = $packs.packs.$Mode
$paths = @($modePack.$Phase)

if ($Phase -eq 'phase-4c' -and -not $IncludeGateway.IsPresent) {
    $paths = @($paths | Where-Object { $_ -ne 'skills/gateway.md' })
}

if ($Phase -eq 'phase-4d') {
    if (-not $IncludeScheduler.IsPresent) {
        $paths = @($paths | Where-Object { $_ -ne 'skills/background-services.md' })
    }

    if (-not $IncludeFunctionApp.IsPresent) {
        $paths = @($paths | Where-Object { $_ -ne 'skills/function-app.md' })
    }

    if (-not $IncludeUnoUI.IsPresent) {
        $paths = @($paths | Where-Object { $_ -ne 'skills/uno-ui.md' })
    }
}

if ($Phase -eq 'phase-4d-optional' -and -not $IncludeUnoUI.IsPresent) {
    $paths = @()
}

if ($Phase -eq 'phase-4g' -and -not $IncludeAiServices.IsPresent) {
    $paths = @()
}

$tokenMap = @{}
foreach ($entry in $manifest.files) {
    $tokenMap[[string]$entry.path] = [int]$entry.estimatedTokens
}

$totalTokens = 0
$items = @(foreach ($path in $paths) {
    $tokens = if ($tokenMap.ContainsKey($path)) { $tokenMap[$path] } else { 0 }
    $totalTokens += $tokens

    [PSCustomObject]@{
        path = $path
        estimatedTokens = $tokens
    }
})

if ($AsJson) {
    [PSCustomObject]@{
        phase = $Phase
        mode = $Mode
        totalEstimatedTokens = $totalTokens
        files = $items
    } | ConvertTo-Json -Depth 6
    exit 0
}

Write-Output "Phase: $Phase"
Write-Output "Mode: $Mode"
Write-Output "TotalEstimatedTokens: $totalTokens"

if ($items.Count -eq 0) {
    Write-Output 'No files selected for this phase with current feature flags.'
    exit 0
}

$items | Format-Table -AutoSize path, estimatedTokens | Out-String | Write-Output
