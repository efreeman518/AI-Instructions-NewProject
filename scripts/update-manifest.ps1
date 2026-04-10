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

$python = Resolve-PythonCommand
$scriptPath = Join-Path $PSScriptRoot 'update-manifest.py'

& $python.Executable @($python.PrefixArgs) $scriptPath --root $Root
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
