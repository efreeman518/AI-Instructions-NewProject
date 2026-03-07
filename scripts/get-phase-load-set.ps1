param(
    [Parameter(Mandatory = $true)]
    [string]$Phase,

    [ValidateSet('full', 'lite', 'api-only')]
    [string]$Mode = 'full',

    [switch]$IncludeGateway,
    [switch]$IncludeFunctionApp,
    [switch]$IncludeScheduler,
    [switch]$IncludeUnoUI,
    [switch]$IncludeAspire,
    [switch]$IncludeAiServices,
    [switch]$IncludeAiSearch,
    [switch]$IncludeAgents,

    [ValidateSet('default', 'extended', 'compact')]
    [string]$BudgetProfile = 'default',

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
if (-not $modePack) {
    throw "Unknown mode '$Mode' in phase-load-packs.json."
}

if (-not ($packs.contextBudget.PSObject.Properties.Name -contains $BudgetProfile)) {
    throw "Unknown budget profile '$BudgetProfile'."
}

function Filter-RequestedPaths {
    param(
        [string[]]$Paths,
        [string]$RequestedPhase,
        [string]$RequestedMode
    )

    $filtered = @($Paths)

    if ($RequestedPhase -eq 'phase-4c') {
        if (-not $IncludeGateway.IsPresent) {
            $filtered = @($filtered | Where-Object { $_ -ne 'skills/gateway.md' })
        }

        if ($RequestedMode -eq 'api-only' -and -not $IncludeAspire.IsPresent) {
            $filtered = @($filtered | Where-Object { $_ -ne 'skills/aspire.md' })
        }
    }

    if ($RequestedPhase -eq 'phase-4d') {
        if (-not $IncludeScheduler.IsPresent) {
            $filtered = @($filtered | Where-Object { $_ -ne 'skills/background-services.md' })
        }

        if (-not $IncludeFunctionApp.IsPresent) {
            $filtered = @($filtered | Where-Object { $_ -ne 'skills/function-app.md' })
        }

        if (-not $IncludeUnoUI.IsPresent) {
            $filtered = @($filtered | Where-Object { $_ -ne 'skills/uno-ui.md' })
        }
    }

    if ($RequestedPhase -eq 'phase-4d-optional' -and -not $IncludeUnoUI.IsPresent) {
        return @()
    }

    if ($RequestedPhase -eq 'phase-4g' -and -not $IncludeAiServices.IsPresent) {
        return @()
    }

    if ($RequestedPhase -eq 'phase-4g' -and $IncludeAiServices.IsPresent) {
        $hasGranularAiSelection = $IncludeAiSearch.IsPresent -or $IncludeAgents.IsPresent
        if ($hasGranularAiSelection) {
            $allowedPaths = [System.Collections.Generic.HashSet[string]]::new()
            $allowedPaths.Add('skills/ai-integration.md') | Out-Null

            if ($IncludeAiSearch.IsPresent) {
                $allowedPaths.Add('templates/ai-search-template.md') | Out-Null
            }

            if ($IncludeAgents.IsPresent) {
                $allowedPaths.Add('templates/agent-template.md') | Out-Null
            }

            $filtered = @($filtered | Where-Object { $allowedPaths.Contains([string]$_) })
        }
    }

    return $filtered
}

$requestedPaths = Filter-RequestedPaths -Paths @($modePack.$Phase) -RequestedPhase $Phase -RequestedMode $Mode

$entryMap = @{}
foreach ($entry in $manifest.files) {
    $entryMap[[string]$entry.path] = $entry
}

$modeExclusionSet = @{}
if ($packs.modeExclusions.PSObject.Properties.Name -contains $Mode) {
    foreach ($path in @($packs.modeExclusions.$Mode)) {
        $modeExclusionSet[[string]$path] = $true
    }
}

function Get-EntryDependencies {
    param([psobject]$Entry)

    $dependencies = @()
    if ($Entry.PSObject.Properties.Name -contains 'dependencies' -and $null -ne $Entry.dependencies) {
        $dependencies += @($Entry.dependencies)
    }

    if ($Entry.PSObject.Properties.Name -contains 'requires' -and $null -ne $Entry.requires) {
        $dependencies += @($Entry.requires)
    }

    return @($dependencies | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Add-ResolvedPath {
    param(
        [string]$Path,
        [System.Collections.Generic.HashSet[string]]$Visited,
        [System.Collections.Generic.HashSet[string]]$Visiting,
        [System.Collections.Generic.List[string]]$OrderedPaths
    )

    if ($Visited.Contains($Path)) {
        return
    }

    if ($Visiting.Contains($Path)) {
        throw "Dependency cycle detected while resolving '$Path'."
    }

    if (-not $entryMap.ContainsKey($Path)) {
        throw "Dependency '$Path' is not present in _manifest.json."
    }

    if ($modeExclusionSet.ContainsKey($Path)) {
        throw "Dependency '$Path' is excluded by mode '$Mode'. Adjust modeExclusions or the dependency graph."
    }

    $Visiting.Add($Path) | Out-Null
    foreach ($dependency in Get-EntryDependencies -Entry $entryMap[$Path]) {
        Add-ResolvedPath -Path ([string]$dependency) -Visited $Visited -Visiting $Visiting -OrderedPaths $OrderedPaths
    }

    $Visiting.Remove($Path) | Out-Null
    $Visited.Add($Path) | Out-Null
    $OrderedPaths.Add($Path) | Out-Null
}

$orderedPaths = [System.Collections.Generic.List[string]]::new()
$visited = [System.Collections.Generic.HashSet[string]]::new()
$visiting = [System.Collections.Generic.HashSet[string]]::new()
$requestedSet = [System.Collections.Generic.HashSet[string]]::new()

foreach ($path in $requestedPaths) {
    $requestedSet.Add([string]$path) | Out-Null
}

foreach ($path in $requestedPaths) {
    Add-ResolvedPath -Path ([string]$path) -Visited $visited -Visiting $visiting -OrderedPaths $orderedPaths
}

$tokenMap = @{}
foreach ($entry in $manifest.files) {
    $tokenMap[[string]$entry.path] = [int]$entry.estimatedTokens
}

$budgetLimit = [int]$packs.contextBudget.$BudgetProfile
$totalTokens = 0
$items = @(foreach ($path in $orderedPaths) {
    $tokens = if ($tokenMap.ContainsKey($path)) { $tokenMap[$path] } else { 0 }
    $totalTokens += $tokens

    [PSCustomObject]@{
        path = $path
        phase = [string]$entryMap[$path].phase
        selection = if ($requestedSet.Contains($path)) { 'requested' } else { 'dependency' }
        estimatedTokens = $tokens
    }
})

$withinBudget = $totalTokens -le $budgetLimit

if ($AsJson) {
    [PSCustomObject]@{
        phase = $Phase
        mode = $Mode
        budgetProfile = $BudgetProfile
        budgetLimit = $budgetLimit
        withinBudget = $withinBudget
        totalEstimatedTokens = $totalTokens
        requestedFiles = @($items | Where-Object { $_.selection -eq 'requested' })
        dependencyFiles = @($items | Where-Object { $_.selection -eq 'dependency' })
        files = $items
    } | ConvertTo-Json -Depth 6
    exit 0
}

Write-Output "Phase: $Phase"
Write-Output "Mode: $Mode"
Write-Output "BudgetProfile: $BudgetProfile"
Write-Output "BudgetLimit: $budgetLimit"
Write-Output "WithinBudget: $withinBudget"
Write-Output "TotalEstimatedTokens: $totalTokens"

if ($items.Count -eq 0) {
    Write-Output 'No files selected for this phase with current feature flags.'
    exit 0
}

$items | Format-Table -AutoSize path, phase, selection, estimatedTokens | Out-String | Write-Output

if (-not $withinBudget) {
    Write-Warning "Selected load set exceeds the $BudgetProfile budget by $($totalTokens - $budgetLimit) tokens."
}
