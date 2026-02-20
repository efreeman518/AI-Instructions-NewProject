<#
.SYNOPSIS
    Assigns an Azure managed identity to an Entra ID app role.

.DESCRIPTION
    Pattern: DevOps automation script for managed identity → app role assignment.
    
    When a Container App or App Service needs to call another API protected by Entra ID,
    the calling app's managed identity must be assigned to an app role on the target
    API's app registration. This script automates that assignment.

    Auth flow:
    1. Caller app has a system-assigned or user-assigned managed identity.
    2. Target API has an Entra ID app registration with defined app roles.
    3. This script assigns the caller's MI service principal to the target's app role.
    4. At runtime, the caller acquires a client credential token with the role claim.

    Prerequisites:
    - Azure CLI installed and logged in (az login)
    - Microsoft.Graph PowerShell module installed (Install-Module Microsoft.Graph)
    - Caller must have Application Administrator or Global Administrator role

.PARAMETER ManagedIdentityName
    Display name of the managed identity (system MI = app name, user MI = MI resource name).

.PARAMETER AppRegistrationClientId
    Client (application) ID of the TARGET API's Entra ID app registration.

.PARAMETER AppRoleName
    Name of the app role to assign (e.g., "Api.ReadWrite", "Scheduler.Execute").

.PARAMETER ResourceType
    Type of Azure resource: "WebApp" or "ContainerApp". Determines how to resolve the MI object ID.

.PARAMETER ManagedIdentityObjectIdOverride
    Optional: directly provide the MI's service principal object ID (skips auto-resolution).

.EXAMPLE
    .\assign-managed-identity-to-app-role.ps1 `
        -ManagedIdentityName "taskflow-api-prod" `
        -AppRegistrationClientId "00000000-0000-0000-0000-000000000001" `
        -AppRoleName "Api.ReadWrite" `
        -ResourceType "ContainerApp"

.NOTES
    Convention: DevOps support scripts live in sampleapp/support/.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManagedIdentityName,

    [Parameter(Mandatory = $true)]
    [string]$AppRegistrationClientId,

    [Parameter(Mandatory = $true)]
    [string]$AppRoleName,

    [Parameter(Mandatory = $false)]
    [ValidateSet("WebApp", "ContainerApp")]
    [string]$ResourceType = "ContainerApp",

    [Parameter(Mandatory = $false)]
    [string]$ManagedIdentityObjectIdOverride
)

$ErrorActionPreference = "Stop"

# ═══════════════════════════════════════════════════════════════
# Step 1: Resolve the managed identity's service principal object ID.
# ═══════════════════════════════════════════════════════════════

if ($ManagedIdentityObjectIdOverride) {
    $miObjectId = $ManagedIdentityObjectIdOverride
    Write-Host "Using provided MI object ID: $miObjectId" -ForegroundColor Cyan
}
else {
    Write-Host "Resolving managed identity '$ManagedIdentityName' ($ResourceType)..." -ForegroundColor Cyan

    # Pattern: Look up the service principal by display name.
    $sp = Get-MgServicePrincipal -Filter "displayName eq '$ManagedIdentityName'" -Top 1
    if (-not $sp) {
        throw "Service principal not found for managed identity: $ManagedIdentityName"
    }
    $miObjectId = $sp.Id
    Write-Host "Resolved MI object ID: $miObjectId" -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════
# Step 2: Find the target API's service principal and app role.
# ═══════════════════════════════════════════════════════════════

Write-Host "Looking up target API app registration: $AppRegistrationClientId..." -ForegroundColor Cyan

$targetSp = Get-MgServicePrincipal -Filter "appId eq '$AppRegistrationClientId'" -Top 1
if (-not $targetSp) {
    throw "Service principal not found for app registration: $AppRegistrationClientId"
}

$appRole = $targetSp.AppRoles | Where-Object { $_.Value -eq $AppRoleName }
if (-not $appRole) {
    Write-Host "Available roles:" -ForegroundColor Yellow
    $targetSp.AppRoles | ForEach-Object { Write-Host "  - $($_.Value) ($($_.Id))" }
    throw "App role '$AppRoleName' not found on app registration $AppRegistrationClientId"
}

Write-Host "Found app role: $($appRole.Value) (ID: $($appRole.Id))" -ForegroundColor Green

# ═══════════════════════════════════════════════════════════════
# Step 3: Check for existing assignment (idempotent).
# ═══════════════════════════════════════════════════════════════

$existingAssignment = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId |
    Where-Object { $_.AppRoleId -eq $appRole.Id -and $_.ResourceId -eq $targetSp.Id }

if ($existingAssignment) {
    Write-Host "Assignment already exists — skipping." -ForegroundColor Yellow
    Write-Host "  Principal: $ManagedIdentityName ($miObjectId)"
    Write-Host "  Role:      $AppRoleName ($($appRole.Id))"
    Write-Host "  Resource:  $($targetSp.DisplayName) ($($targetSp.Id))"
    exit 0
}

# ═══════════════════════════════════════════════════════════════
# Step 4: Create the app role assignment.
# ═══════════════════════════════════════════════════════════════

Write-Host "Creating app role assignment..." -ForegroundColor Cyan

$params = @{
    PrincipalId = $miObjectId
    ResourceId  = $targetSp.Id
    AppRoleId   = $appRole.Id
}

New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId -BodyParameter $params

Write-Host "`nAssignment complete!" -ForegroundColor Green
Write-Host "  Principal: $ManagedIdentityName ($miObjectId)"
Write-Host "  Role:      $AppRoleName ($($appRole.Id))"
Write-Host "  Resource:  $($targetSp.DisplayName) ($($targetSp.Id))"
