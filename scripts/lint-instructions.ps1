param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-Issue {
    param(
        [string]$Category,
        [string]$File,
        [string]$Message,
        [int]$Line = 0
    )

    [PSCustomObject]@{
        Category = $Category
        File     = $File
        Line     = $Line
        Message  = $Message
    }
}

$issues = @()

# -----------------------------------------------------------------------------
# 1) Template symbol contract checks
# -----------------------------------------------------------------------------
$agentTemplatePath = Join-Path $Root 'templates/agent-template.md'
$serviceTemplatePath = Join-Path $Root 'templates/service-template.md'

if (-not (Test-Path $agentTemplatePath)) {
    $issues += Add-Issue -Category 'TemplateContract' -File 'templates/agent-template.md' -Message 'Missing required template file.'
}
if (-not (Test-Path $serviceTemplatePath)) {
    $issues += Add-Issue -Category 'TemplateContract' -File 'templates/service-template.md' -Message 'Missing required template file.'
}

if ((Test-Path $agentTemplatePath) -and (Test-Path $serviceTemplatePath)) {
    $agentContent = Get-Content $agentTemplatePath -Raw -Encoding UTF8
    $serviceContent = Get-Content $serviceTemplatePath -Raw -Encoding UTF8

    if ($agentContent -match 'GetByIdAsync\(') {
        $issues += Add-Issue -Category 'TemplateContract' -File 'templates/agent-template.md' -Message 'Found GetByIdAsync(...) reference. Use GetAsync(...) to match service template contract.'
    }

    if ($agentContent -notmatch 'GetAsync\(Guid\.Parse\(id\),\s*ct\)') {
        $issues += Add-Issue -Category 'TemplateContract' -File 'templates/agent-template.md' -Message 'Agent template does not show GetAsync(Guid.Parse(id), ct) usage.'
    }

    if ($serviceContent -notmatch 'Task<Result<DefaultResponse<\{Entity\}Dto>>>\s+GetAsync\(') {
        $issues += Add-Issue -Category 'TemplateContract' -File 'templates/service-template.md' -Message 'Service template is missing expected GetAsync(...) contract signature.'
    }
}

# -----------------------------------------------------------------------------
# 2) Markdown shell fence checks
#    Flag bash fences that contain PowerShell-specific syntax.
# -----------------------------------------------------------------------------
$mdFiles = Get-ChildItem -Path $Root -Recurse -File -Filter '*.md'

foreach ($file in $mdFiles) {
    $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\\', '/')
    $lines = Get-Content $file.FullName -Encoding UTF8

    $inFence = $false
    $fenceLang = ''
    $fenceStartLine = 0
    $fenceLines = @()

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        $lineNumber = $index + 1

        if (-not $inFence) {
            if ($line -match '^```(\w+)?\s*$') {
                $inFence = $true
                $fenceLang = $Matches[1]
                $fenceStartLine = $lineNumber
                $fenceLines = @()
            }
            continue
        }

        if ($line -match '^```\s*$') {
            if ($fenceLang -eq 'bash') {
                $hasPowerShellSyntax = $false
                foreach ($fenceLine in $fenceLines) {
                    if ($fenceLine -match '\$env:' -or $fenceLine -match '\bSet-Item\b' -or $fenceLine -match '\bGet-ChildItem\b') {
                        $hasPowerShellSyntax = $true
                        break
                    }
                }

                if ($hasPowerShellSyntax) {
                    $issues += Add-Issue -Category 'ShellFence' -File $relativeFile -Line $fenceStartLine -Message 'bash code fence contains PowerShell-specific syntax.'
                }
            }

            $inFence = $false
            $fenceLang = ''
            $fenceStartLine = 0
            $fenceLines = @()
            continue
        }

        $fenceLines += $line
    }
}

# -----------------------------------------------------------------------------
# 3) Terminology drift checks
# -----------------------------------------------------------------------------
$driftPatterns = @(
    @{
        Pattern  = 'UPDATE_INSTRUCTIONS\.md'
        Message  = 'Use UPDATE-INSTRUCTIONS.md (hyphen), not UPDATE_INSTRUCTIONS.md.'
        Category = 'Terminology'
    },
    @{
        Pattern  = 'codeunless'
        Message  = 'Typo detected: use "code unless".'
        Category = 'Terminology'
    }
)

foreach ($file in $mdFiles) {
    $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\\', '/')
    $lines = Get-Content $file.FullName -Encoding UTF8

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        $lineNumber = $index + 1

        foreach ($rule in $driftPatterns) {
            if ($line -match $rule.Pattern) {
                $issues += Add-Issue -Category $rule.Category -File $relativeFile -Line $lineNumber -Message $rule.Message
            }
        }
    }
}

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