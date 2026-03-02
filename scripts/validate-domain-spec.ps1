param(
    [Parameter(Mandatory)]
    [string]$FilePath
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
$lines = Get-Content $FilePath -Encoding UTF8

# -------------------------------------------------------------------------
# 1) Required top-level keys
# -------------------------------------------------------------------------
$requiredKeys = @('projectName', 'entities')
foreach ($key in $requiredKeys) {
    if ($content -notmatch "(?m)^${key}:") {
        $issues += Add-Issue -Category 'MissingKey' -Message "Required top-level key missing: $key"
    }
}

# -------------------------------------------------------------------------
# 2) Entity name conflict check (C# reserved type names)
# -------------------------------------------------------------------------
$reservedNames = @('Task', 'Thread', 'Timer', 'Type', 'String', 'Object', 'Action', 'Attribute', 'File', 'Path', 'Event', 'Delegate')
$entityMatches = [regex]::Matches($content, '(?m)^\s+-?\s*name:\s*(\w+)')
foreach ($match in $entityMatches) {
    $entityName = $match.Groups[1].Value
    if ($entityName -in $reservedNames) {
        $issues += Add-Issue -Category 'NamingConflict' -Message "Entity name '$entityName' conflicts with C# framework type. Use a domain-specific alternative."
    }
}

# -------------------------------------------------------------------------
# 3) Entity must have at least one property defined
# -------------------------------------------------------------------------
$entityBlockPattern = '(?m)^\s+-?\s*name:\s*\w+'
$entityBlocks = [regex]::Matches($content, $entityBlockPattern)
foreach ($match in $entityBlocks) {
    $startIndex = $match.Index + $match.Length
    # Check if properties: appears before next entity or end
    $nextEntityMatch = [regex]::Match($content.Substring($startIndex), '(?m)^\s+-?\s*name:\s*\w+')
    $blockEnd = if ($nextEntityMatch.Success) { $startIndex + $nextEntityMatch.Index } else { $content.Length }
    $block = $content.Substring($startIndex, $blockEnd - $startIndex)
    if ($block -notmatch 'properties:') {
        $entityName = [regex]::Match($match.Value, 'name:\s*(\w+)').Groups[1].Value
        $issues += Add-Issue -Category 'IncompleteEntity' -Message "Entity '$entityName' has no properties defined."
    }
}

# -------------------------------------------------------------------------
# 4) Relationship target references exist
# -------------------------------------------------------------------------
$allEntityNames = @()
foreach ($match in $entityMatches) {
    $allEntityNames += $match.Groups[1].Value
}

$relationshipTargets = [regex]::Matches($content, '(?m)(?:target|entity):\s*(\w+)')
foreach ($match in $relationshipTargets) {
    $target = $match.Groups[1].Value
    if ($target -notin $allEntityNames -and $target -ne 'self') {
        $issues += Add-Issue -Category 'BrokenReference' -Message "Relationship target '$target' not found in entity definitions."
    }
}

# -------------------------------------------------------------------------
# 5) Duplicate entity names
# -------------------------------------------------------------------------
$entityNameCounts = $allEntityNames | Group-Object
foreach ($group in $entityNameCounts) {
    if ($group.Count -gt 1) {
        $issues += Add-Issue -Category 'DuplicateEntity' -Message "Duplicate entity name: $($group.Name) (appears $($group.Count) times)."
    }
}

# -------------------------------------------------------------------------
# Output
# -------------------------------------------------------------------------
if ($issues.Count -eq 0) {
    Write-Output "PASS: domain specification validation passed for $FilePath"
    exit 0
}

Write-Output "FAIL: domain specification validation found $($issues.Count) issue(s)."
$issues |
    Sort-Object Category |
    Format-Table -AutoSize Category, Message |
    Out-String |
    Write-Output

exit 1
