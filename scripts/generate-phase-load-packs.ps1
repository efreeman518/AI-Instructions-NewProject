param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$OutputFile = 'phase-load-packs.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'python-command.ps1')

if (-not (Test-Path $Root)) {
    Write-Error "Root directory not found: $Root"
    return
}

$Root = (Resolve-Path $Root).Path

$python = Resolve-PythonCommand
$scriptPath = Join-Path $PSScriptRoot 'generate-phase-load-packs.py'

& $python.Executable @($python.PrefixArgs) $scriptPath --root $Root --output-file $OutputFile
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
