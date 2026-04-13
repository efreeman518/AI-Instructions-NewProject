param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'python-command.ps1')

if (-not (Test-Path $Root)) {
    Write-Error "Root directory not found: $Root"
    return
}

$Root = (Resolve-Path $Root).Path

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
    $python = Resolve-PythonCommand

    Invoke-Step -Name 'Refresh manifest tokens' -Action {
        & (Join-Path $Root 'scripts/update-manifest.ps1') -Root $Root
    }

    Invoke-Step -Name 'Generate phase load packs' -Action {
        & (Join-Path $Root 'scripts/generate-phase-load-packs.ps1') -Root $Root
    }

    Invoke-Step -Name 'Report context budgets' -Action {
        & $python.Executable @($python.PrefixArgs) (Join-Path $Root 'scripts/report-context-budgets.py') --root $Root --mode full
    }

    Invoke-Step -Name 'Run instruction lint' -Action {
        & (Join-Path $Root 'scripts/lint-instructions.ps1') -Root $Root
    }

    Invoke-Step -Name 'Run script unit tests' -Action {
        & $python.Executable @($python.PrefixArgs) -m unittest discover -s tests -p 'test_*.py'
    }

    Write-Output 'Preflight complete.'
}
finally {
    Pop-Location
}