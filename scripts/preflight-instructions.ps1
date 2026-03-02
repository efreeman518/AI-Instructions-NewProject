param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Output "==> $Name"
    & $Action
}

Push-Location $Root
try {
    Invoke-Step -Name 'Refresh manifest tokens' -Action {
        & (Join-Path $Root 'scripts/update-manifest.ps1') -Root $Root
    }

    Invoke-Step -Name 'Generate phase load packs' -Action {
        & (Join-Path $Root 'scripts/generate-phase-load-packs.ps1') -Root $Root
    }

    Invoke-Step -Name 'Run instruction lint' -Action {
        & (Join-Path $Root 'scripts/lint-instructions.ps1') -Root $Root
    }

    Write-Output 'Preflight complete.'
}
finally {
    Pop-Location
}