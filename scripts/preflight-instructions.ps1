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

    # Validate sample-app schemas (if present)
    $sampleDomainSpec = Join-Path $Root 'sample-app/domain-specification.md'
    $sampleResourceImpl = Join-Path $Root 'sample-app/resource-implementation.md'

    if (Test-Path $sampleDomainSpec) {
        Invoke-Step -Name 'Validate sample domain spec' -Action {
            & (Join-Path $Root 'scripts/validate-domain-spec.ps1') -FilePath $sampleDomainSpec
        }
    }

    if (Test-Path $sampleResourceImpl) {
        Invoke-Step -Name 'Validate sample resource impl' -Action {
            $domainArg = @{}
            if (Test-Path $sampleDomainSpec) { $domainArg['DomainSpecPath'] = $sampleDomainSpec }
            & (Join-Path $Root 'scripts/validate-resource-impl.ps1') -FilePath $sampleResourceImpl @domainArg
        }
    }

    Write-Output 'Preflight complete.'
}
finally {
    Pop-Location
}