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
    $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
    $lines = Get-Content $file.FullName -Encoding UTF8

    $inFence = $false
    $fenceLang = ''
    $fenceStartLine = 0
    $fenceLines = @()

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        $lineNumber = $index + 1

        if (-not $inFence) {
            $fenceStart = [regex]::Match($line, '^```(\w+)?\s*$')
            if ($fenceStart.Success) {
                $inFence = $true
                $fenceLang = $fenceStart.Groups[1].Value
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
    $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $linkTargets = [regex]::Matches($content, '\[[^\]]+\]\(([^)]+)\)')

    foreach ($match in $linkTargets) {
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
    $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
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
$placeholderDocPath = Join-Path $Root 'ai/placeholder-tokens.md'
if (-not (Test-Path $placeholderDocPath)) {
    $issues += Add-Issue -Category 'PlaceholderCoverage' -File 'ai/placeholder-tokens.md' -Message 'Missing placeholder token glossary.'
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
$canonicalTroubleshootingPath = Join-Path $Root 'support/troubleshooting.md'
if (-not (Test-Path $canonicalTroubleshootingPath)) {
    $issues += Add-Issue -Category 'GotchaDuplication' -File 'support/troubleshooting.md' -Message 'Missing canonical troubleshooting file.'
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
        $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
        if ($relativeFile -eq 'support/troubleshooting.md') { continue }

        $content = Get-Content $file.FullName -Raw -Encoding UTF8
        foreach ($phrase in $criticalPhrases) {
            if ($content -match [regex]::Escape($phrase)) {
                $issues += Add-Issue -Category 'GotchaDuplication' -File $relativeFile -Message "Critical gotcha phrase duplicated outside canonical file: $phrase"
            }
        }
    }
}

# -----------------------------------------------------------------------------
# 7) Skill file existence — verify all skills/ referenced in SKILL.md exist
# -----------------------------------------------------------------------------
$skillMdPath = Join-Path $Root 'ai/SKILL.md'
if (Test-Path $skillMdPath) {
    $skillContent = Get-Content $skillMdPath -Raw -Encoding UTF8
    $skillRefs = [regex]::Matches($skillContent, 'skills/([a-z0-9-]+)\.md') | ForEach-Object { $_.Value } | Sort-Object -Unique

    foreach ($ref in $skillRefs) {
        $fullPath = Join-Path $Root $ref
        if (-not (Test-Path $fullPath)) {
            $issues += Add-Issue -Category 'MissingSkill' -File 'ai/SKILL.md' -Message "Referenced skill file does not exist: $ref"
        }
    }
}

# -----------------------------------------------------------------------------
# 8) Manifest ↔ SKILL.md phase alignment
#    Every file in _manifest.json should exist on disk.
# -----------------------------------------------------------------------------
$manifestPath = Join-Path $Root '_manifest.json'
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $manifest.files) {
        $entryPath = Join-Path $Root $entry.path
        if (-not (Test-Path $entryPath)) {
            $issues += Add-Issue -Category 'ManifestSync' -File '_manifest.json' -Message "Manifest entry references missing file: $($entry.path)"
        }
    }

    # Check that all .md files (except sample-app/, bin/, obj/) are in the manifest
    $manifestPathSet = @{}
    foreach ($entry in $manifest.files) {
        $normalized = $entry.path.Replace('/', '\').ToLower()
        $manifestPathSet[$normalized] = $true
    }
    foreach ($file in $mdFiles) {
        $relativeFile = $file.FullName.Substring($Root.Length + 1).Replace('\', '/')
        if ($relativeFile -match '^sample-app/') { continue }
        if ($relativeFile -match '(^|/)bin/' -or $relativeFile -match '(^|/)obj/') { continue }
        if ($relativeFile -eq 'CHANGELOG.md') { continue }
        $lookupKey = $relativeFile.Replace('/', '\').ToLower()
        if (-not $manifestPathSet.ContainsKey($lookupKey)) {
            $issues += Add-Issue -Category 'ManifestSync' -File $relativeFile -Message "Markdown file exists but is not listed in _manifest.json."
        }
    }
}

# -----------------------------------------------------------------------------
# 9) Undefined placeholder tokens in templates
#    Find {Token} patterns in templates that are not in placeholder-tokens.md
# -----------------------------------------------------------------------------
if (Test-Path $placeholderDocPath) {
    $templateDir = Join-Path $Root 'templates'
    if (Test-Path $templateDir) {
        $templateFiles = Get-ChildItem -Path $templateDir -Recurse -File -Filter '*.md'
        foreach ($tFile in $templateFiles) {
            $tContent = Get-Content $tFile.FullName -Raw -Encoding UTF8
            $relativeFile = $tFile.FullName.Substring($Root.Length + 1).Replace('\', '/')

            # Find all {Token} patterns (PascalCase or camelCase, not inside code fences for common C# patterns)
            $tokenResults = [regex]::Matches($tContent, '\{([A-Z][A-Za-z0-9-]*)\}')
            foreach ($match in $tokenResults) {
                $token = "{$($match.Groups[1].Value)}"
                $innerToken = $match.Groups[1].Value
                # Skip common C# syntax, generics, and template-contextual tokens
                # These appear in code samples but are not user-level placeholder tokens
                $skipPatterns = @(
                    # C# keywords/common identifiers that appear in braces
                    'get', 'set', 'init', 'value', 'index', 'item', 'key',
                    'options', 'ctx', 'context', 'provider', 'sp', 'builder',
                    'db', 'app', 'config', 'services', 'host',
                    # C# generic type parameters
                    'T', 'TEntity', 'TDto', 'TContext', 'TAuditId', 'TTenantId',
                    'TProperty', 'TResult', 'TResponse', 'TRequest', 'TFilter',
                    'TProgram',
                    # Template-contextual tokens (derived at generation time, not user input)
                    'Namespace', 'ParentEntity', 'PropertyName', 'PropertyType',
                    'RuleName', 'Event', 'EventPayload', 'Target',
                    'NameMaxLength', 'DescriptionMaxLength', 'MaxChildren',
                    'DtoProperty', 'ErrorMessage', 'Condition',
                    'FieldName', 'FieldType', 'TableName', 'ColumnName',
                    'HandlerName', 'JobName', 'ScheduleExpression',
                    'ValidatorName', 'ServiceName', 'MethodName',
                    'IndexField', 'SearchField', 'ModelName', 'DeploymentName',
                    # Template example/placeholder tokens (appear in code samples)
                    'Parent', 'Related', 'Enum', 'Name', 'Id', 'Message',
                    'Action', 'EventName', 'Handler', 'User', 'Query', 'Mode',
                    'Count', 'DocumentId', 'EntityId', 'ExceptionType',
                    'AgentName', 'FunctionName', 'MessageCount', 'ResponseCount',
                    'Property1', 'Property2', 'SomeOtherProp'
                )
                if ($innerToken -in $skipPatterns) { continue }
                if ($token -notin $knownTokens) {
                    $issues += Add-Issue -Category 'UndefinedToken' -File $relativeFile -Message "Placeholder token not defined in glossary: $token"
                }
            }
        }
    }
}

# -----------------------------------------------------------------------------
# 10) Manifest invariants for load orchestration
# -----------------------------------------------------------------------------
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

    if (-not ($manifest.PSObject.Properties.Name -contains 'modeExclusions')) {
        $issues += Add-Issue -Category 'ManifestInvariants' -File '_manifest.json' -Message 'Manifest is missing top-level modeExclusions.'
    }
    else {
        foreach ($mode in @('lite', 'api-only')) {
            if (-not ($manifest.modeExclusions.PSObject.Properties.Name -contains $mode)) {
                $issues += Add-Issue -Category 'ManifestInvariants' -File '_manifest.json' -Message "modeExclusions is missing '$mode'."
                continue
            }

            foreach ($path in @($manifest.modeExclusions.$mode)) {
                if ($path -notmatch '^skills/.+\.md$') {
                    $issues += Add-Issue -Category 'ManifestInvariants' -File '_manifest.json' -Message "Mode exclusion '$path' for '$mode' must reference a skill markdown file."
                    continue
                }

                $fullPath = Join-Path $Root $path
                if (-not (Test-Path $fullPath)) {
                    $issues += Add-Issue -Category 'ManifestInvariants' -File '_manifest.json' -Message "Mode exclusion '$path' for '$mode' does not exist on disk."
                }
            }
        }
    }

    $identityEntry = @($manifest.files | Where-Object { $_.path -eq 'skills/identity-management.md' } | Select-Object -First 1)
    if (-not $identityEntry) {
        $issues += Add-Issue -Category 'ManifestInvariants' -File '_manifest.json' -Message 'skills/identity-management.md is missing from manifest.files.'
    }
    elseif ([string]$identityEntry.phase -ne 'phase-4f') {
        $issues += Add-Issue -Category 'ManifestInvariants' -File '_manifest.json' -Message 'skills/identity-management.md must be assigned to phase-4f.'
    }

    $phaseLoadPacksPath = Join-Path $Root 'phase-load-packs.json'
    if (-not (Test-Path $phaseLoadPacksPath)) {
        $issues += Add-Issue -Category 'ManifestInvariants' -File 'phase-load-packs.json' -Message 'Missing generated phase-load-packs.json.'
    }
    else {
        $phaseLoadPacks = Get-Content $phaseLoadPacksPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $generatedPhaseOrder = @($phaseLoadPacks.phaseOrder)
        $manifestPhases = @($manifest.files | ForEach-Object { [string]$_.phase } | Sort-Object -Unique)

        foreach ($phase in $manifestPhases) {
            if ($phase -notin $generatedPhaseOrder) {
                $issues += Add-Issue -Category 'ManifestInvariants' -File 'phase-load-packs.json' -Message "Generated phaseOrder is missing manifest phase '$phase'."
            }
        }

        foreach ($mode in @('full', 'lite', 'api-only')) {
            if (-not ($phaseLoadPacks.packs.PSObject.Properties.Name -contains $mode)) {
                $issues += Add-Issue -Category 'ManifestInvariants' -File 'phase-load-packs.json' -Message "Generated packs is missing mode '$mode'."
                continue
            }

            foreach ($phase in $manifestPhases) {
                if (-not ($phaseLoadPacks.packs.$mode.PSObject.Properties.Name -contains $phase)) {
                    $issues += Add-Issue -Category 'ManifestInvariants' -File 'phase-load-packs.json' -Message "Generated mode '$mode' is missing phase '$phase'."
                }
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
