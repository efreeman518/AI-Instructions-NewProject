param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lint-rules.ps1')

$issues = Get-LintIssues -Root $Root

# -----------------------------------------------------------------------------
# Output
# -----------------------------------------------------------------------------
if ($issues.Count -eq 0) {
    Write-Output 'PASS: instruction lint checks passed.'
    exit 0
}

Write-Output "FAIL: instruction lint checks found $($issues.Count) issue(s)."
$issues |
    Sort-Object Category, File, Line |
    Format-Table -AutoSize Category, File, Line, Message |
    Out-String |
    Write-Output

exit 1
