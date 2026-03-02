param(
    [Parameter(Mandatory)]
    [string]$FilePath,

    [string]$DomainSpecPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-Issue {
    param(
        [string]$Category,
        [string]$Message,
        [int]$Line = 0
    )
    [PSCustomObject]@{
        Category = $Category
        File     = $FilePath
        Line     = $Line
        Message  = $Message
    }
}

if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

$issues = @()
$content = Get-Content $FilePath -Raw -Encoding UTF8

# -------------------------------------------------------------------------
# 1) Required top-level keys
# -------------------------------------------------------------------------
$requiredKeys = @('scaffoldMode', 'testingProfile', 'entities')
foreach ($key in $requiredKeys) {
    if ($content -notmatch "(?m)^${key}:") {
        $issues += Add-Issue -Category 'MissingKey' -Message "Required top-level key missing: $key"
    }
}

# -------------------------------------------------------------------------
# 2) scaffoldMode must be valid
# -------------------------------------------------------------------------
$modeMatch = [regex]::Match($content, '(?m)^scaffoldMode:\s*([\w-]+)')
if ($modeMatch.Success) {
    $mode = $modeMatch.Groups[1].Value
    if ($mode -notin @('full', 'lite', 'api-only')) {
        $issues += Add-Issue -Category 'InvalidValue' -Message "scaffoldMode '$mode' is not valid. Use: full, lite, api-only."
    }
}

# -------------------------------------------------------------------------
# 3) testingProfile must be valid
# -------------------------------------------------------------------------
$profileMatch = [regex]::Match($content, '(?m)^testingProfile:\s*(\w+)')
if ($profileMatch.Success) {
    $profile = $profileMatch.Groups[1].Value
    if ($profile -notin @('minimal', 'balanced', 'comprehensive')) {
        $issues += Add-Issue -Category 'InvalidValue' -Message "testingProfile '$profile' is not valid. Use: minimal, balanced, comprehensive."
    }
}

# -------------------------------------------------------------------------
# 4) Entity dataStore must be a known type
# -------------------------------------------------------------------------
$storeMatches = [regex]::Matches($content, '(?m)dataStore:\s*(\w+)')
$validStores = @('sql', 'sqlServer', 'cosmosDb', 'tableStorage', 'blobStorage', 'redis', 'inMemory')
foreach ($match in $storeMatches) {
    $store = $match.Groups[1].Value
    if ($store -notin $validStores) {
        $issues += Add-Issue -Category 'InvalidValue' -Message "dataStore '$store' is not a recognized store type. Valid: $($validStores -join ', ')."
    }
}

# -------------------------------------------------------------------------
# 5) Cross-reference: entities match domain spec (if provided)
# -------------------------------------------------------------------------
if ($DomainSpecPath -and (Test-Path $DomainSpecPath)) {
    $domainContent = Get-Content $DomainSpecPath -Raw -Encoding UTF8
    # Scope to ## Entities section only (skip Business Rules, Events, State Machines)
    $domainEntitiesSection = ''
    $domainSectionMatch = [regex]::Match($domainContent, '(?ms)^## Entities\s*\r?\n(.*?)(?=\r?\n## |\z)')
    if ($domainSectionMatch.Success) {
        $domainEntitiesSection = $domainSectionMatch.Groups[1].Value
    }
    $domainEntities = [regex]::Matches($domainEntitiesSection, '(?m)^\s+-?\s*name:\s*(\w+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

    # Scope to ## Entity-to-Store Mapping section only (skip Aspire, API endpoints, services)
    $storeMapSection = ''
    $storeMapMatch = [regex]::Match($content, '(?ms)^## Entity-to-Store Mapping\s*\r?\n(.*?)(?=\r?\n## |\z)')
    if ($storeMapMatch.Success) {
        $storeMapSection = $storeMapMatch.Groups[1].Value
    }
    $resourceEntities = @()
    $reEntityMatches = [regex]::Matches($storeMapSection, '(?m)^  -\s*name:\s*(\w+)')
    foreach ($match in $reEntityMatches) {
        $resourceEntities += $match.Groups[1].Value
    }
    $resourceEntities = $resourceEntities | Sort-Object -Unique

    foreach ($domainEntity in $domainEntities) {
        if ($domainEntity -notin $resourceEntities) {
            $issues += Add-Issue -Category 'CrossReference' -Message "Domain entity '$domainEntity' not found in resource implementation."
        }
    }

    foreach ($resEntity in $resourceEntities) {
        if ($resEntity -notin $domainEntities) {
            $issues += Add-Issue -Category 'CrossReference' -Message "Resource entity '$resEntity' not found in domain specification."
        }
    }
}

# -------------------------------------------------------------------------
# 6) maxLength values must be positive integers
# -------------------------------------------------------------------------
$maxLengthMatches = [regex]::Matches($content, '(?m)maxLength:\s*(-?\d+)')
foreach ($match in $maxLengthMatches) {
    $val = [int]$match.Groups[1].Value
    if ($val -le 0) {
        $issues += Add-Issue -Category 'InvalidValue' -Message "maxLength value $val must be a positive integer."
    }
}

# -------------------------------------------------------------------------
# 7) precision format check (e.g., "decimal(10,4)")
# -------------------------------------------------------------------------
$precisionMatches = [regex]::Matches($content, '(?m)precision:\s*"?decimal\((\d+),(\d+)\)"?')
foreach ($match in $precisionMatches) {
    $p = [int]$match.Groups[1].Value
    $s = [int]$match.Groups[2].Value
    if ($s -ge $p) {
        $issues += Add-Issue -Category 'InvalidValue' -Message "Decimal precision scale ($s) must be less than precision ($p)."
    }
}

# -------------------------------------------------------------------------
# Output
# -------------------------------------------------------------------------
if ($issues.Count -eq 0) {
    Write-Output "PASS: resource implementation validation passed for $FilePath"
    exit 0
}

Write-Output "FAIL: resource implementation validation found $($issues.Count) issue(s)."
$issues |
    Sort-Object Category |
    Format-Table -AutoSize Category, Message |
    Out-String |
    Write-Output

exit 1
