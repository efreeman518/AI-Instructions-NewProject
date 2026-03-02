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

$mdFiles = Get-ChildItem -Path $Root -Recurse -File -Filter '*.md'

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
# 3) Broken local markdown links
# -----------------------------------------------------------------------------
foreach ($file in $mdFiles) {
    $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\\', '/')
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $matches = [regex]::Matches($content, '\[[^\]]+\]\(([^)]+)\)')

    foreach ($match in $matches) {
        $target = $match.Groups[1].Value

        if ($target -match '^(http|https|mailto):') { continue }
        if ($target.StartsWith('#')) { continue }

        $targetNoAnchor = $target.Split('#')[0]
        if ([string]::IsNullOrWhiteSpace($targetNoAnchor)) { continue }

        $resolved = Join-Path $file.DirectoryName $targetNoAnchor
        $resolved = [System.IO.Path]::GetFullPath($resolved)

        if (-not (Test-Path $resolved)) {
            $issues += Add-Issue -Category 'Links' -File $relativeFile -Message "Broken local link target: $targetNoAnchor"
        }
    }
}

# -----------------------------------------------------------------------------
# 4) Terminology drift checks
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
# 5) Placeholder token coverage
# -----------------------------------------------------------------------------
$placeholderDocPath = Join-Path $Root 'placeholder-tokens.md'
if (-not (Test-Path $placeholderDocPath)) {
    $issues += Add-Issue -Category 'PlaceholderCoverage' -File 'placeholder-tokens.md' -Message 'Missing placeholder token glossary.'
}
else {
    $placeholderContent = Get-Content $placeholderDocPath -Raw -Encoding UTF8
    $knownTokens = [regex]::Matches($placeholderContent, '\{[A-Za-z][A-Za-z0-9-]*\}') | ForEach-Object Value | Sort-Object -Unique

    $requiredCanonicalTokens = @(
        '{Project}', '{Org}', '{App}', '{Host}', '{Entity}', '{entity}', '{Entities}', '{entities}',
        '{entity-route}', '{ChildEntity}', '{childEntity}', '{Children}', '{Feature}', '{Gateway}',
        '{SolutionName}', '{entra-tenant-id}', '{api-client-id}', '{Agent}', '{agent-route}',
        '{Tool}', '{SearchIndex}'
    )

    foreach ($token in $requiredCanonicalTokens) {
        if ($token -notin $knownTokens) {
            $issues += Add-Issue -Category 'PlaceholderCoverage' -File 'placeholder-tokens.md' -Message "Missing canonical token in glossary: $token"
        }
    }

    $coverageFiles = @(
        (Get-ChildItem -Path (Join-Path $Root 'templates') -Recurse -File -Filter '*.md' -ErrorAction SilentlyContinue),
        (Get-ChildItem -Path (Join-Path $Root 'skills') -Recurse -File -Filter '*.md' -ErrorAction SilentlyContinue),
        (Get-ChildItem -Path $Root -File -Filter '*.md' -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'placeholder-tokens.md' })
    ) | Select-Object -Unique

    $coverageText = ($coverageFiles | ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"

    foreach ($token in $requiredCanonicalTokens) {
        if ($coverageText -notmatch [regex]::Escape($token)) {
            $issues += Add-Issue -Category 'PlaceholderCoverage' -File 'templates/|skills/' -Message "Canonical token is not referenced in docs/templates: $token"
        }
    }
}

# -----------------------------------------------------------------------------
# 6) Duplicate critical gotcha entries outside canonical file
# -----------------------------------------------------------------------------
$canonicalGotchasPath = Join-Path $Root 'test-gotchas.md'
if (-not (Test-Path $canonicalGotchasPath)) {
    $issues += Add-Issue -Category 'GotchaDuplication' -File 'test-gotchas.md' -Message 'Missing canonical test gotchas file.'
}
else {
    $criticalPhrases = @(
        'Search returns empty/0 results',
        'Search returns 500 / negative OFFSET',
        'All writes return `NotImplementedException`',
        'Rate-limited 429 in integration tests',
        'StructureValidator not found'
    )

    foreach ($file in $mdFiles) {
        $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\\', '/')
        if ($relativeFile -eq 'test-gotchas.md') { continue }

        $content = Get-Content $file.FullName -Raw -Encoding UTF8
        foreach ($phrase in $criticalPhrases) {
            if ($content -match [regex]::Escape($phrase)) {
                $issues += Add-Issue -Category 'GotchaDuplication' -File $relativeFile -Message "Critical gotcha phrase duplicated outside canonical file: $phrase"
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