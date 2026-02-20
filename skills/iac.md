# Infrastructure as Code — Azure Bicep

## Overview

This skill generates **Bicep templates** to deploy the scaffolded .NET application to Azure. The IaC must mirror the resources defined in the Aspire AppHost so that local dev and cloud deployment stay consistent. If Aspire is not used, derive the resource list from `deployTarget`, `database`, `caching`, and the enabled project inputs.

## Prerequisites

- Application scaffolding is complete (at least through the API skill)
- If using Aspire, the AppHost is defined and working locally
- Azure CLI installed (`az --version`)
- Bicep CLI installed (`az bicep version`) — comes with Azure CLI

---

## Aspire → Azure Resource Mapping

The Aspire AppHost defines resources for local development. The Bicep templates must provision the **equivalent Azure resources**. Use this mapping table as the source of truth:

| Aspire Resource | Aspire Code | Azure Resource | Bicep Resource Type |
|-----------------|-------------|----------------|---------------------|
| SQL Server + Database | `AddSqlServer()` → `.AddDatabase()` | Azure SQL Server + Database | `Microsoft.Sql/servers`, `Microsoft.Sql/servers/databases` |
| Redis | `AddRedis()` | Azure Cache for Redis | `Microsoft.Cache/redis` |
| API project | `AddProject<Api>()` | Container App (or App Service) | `Microsoft.App/containerApps` or `Microsoft.Web/sites` |
| Gateway project | `AddProject<Gateway>()` | Container App (or App Service) | `Microsoft.App/containerApps` or `Microsoft.Web/sites` |
| Scheduler project | `AddProject<Scheduler>()` | Container App (single replica) | `Microsoft.App/containerApps` (maxReplicas: 1) |
| Function App | Azure Functions project | Azure Functions | `Microsoft.Web/sites` (kind: functionapp) |
| Notification project | `AddProject<Notification>()` | Container App (or App Service) | `Microsoft.App/containerApps` or `Microsoft.Web/sites` |
| Dev Tunnel | `AddDevTunnel()` | *N/A — dev only* | Not deployed |
| Service Discovery | Aspire built-in | Container Apps internal DNS / App Service VNet | Built into Container Apps environment |
| OpenTelemetry | ServiceDefaults | Application Insights + Log Analytics | `Microsoft.Insights/components`, `Microsoft.OperationalInsights/workspaces` |
| Health Checks | ServiceDefaults | Container Apps health probes / App Service health check | Built into container app or app service config |

### Connection String Consistency

Aspire injects connection strings via `WithReference(db, connectionName: "...")`. The Bicep templates must set the **same connection string names** as environment variables or app settings:

```
ConnectionStrings__{Project}DbContextTrxn = "Server=...; Database=...;"
ConnectionStrings__{Project}DbContextQuery = "Server=...; Database=...;"  (can point to read replica)
ConnectionStrings__SchedulerDbContext = "Server=...; Database=...;"       (TickerQ persistence)
ConnectionStrings__Redis1 = "your-redis.redis.cache.windows.net:6380,..."
```

> **Critical:** If the Aspire AppHost uses `connectionName: "Foo"`, the Bicep template must set `ConnectionStrings__Foo`. Mismatches cause runtime failures.

---

## Bicep File Structure

```
infra/
├── main.bicep                    # Orchestrator — calls all modules
├── main.bicepparam               # Parameter file (per environment)
├── modules/
│   ├── container-apps-env.bicep  # Container Apps Environment + Log Analytics
│   ├── container-app.bicep       # Reusable module for each container app
│   ├── sql-server.bicep          # Azure SQL Server + Database(s)
│   ├── redis.bicep               # Azure Cache for Redis
│   ├── key-vault.bicep           # Key Vault for secrets
│   ├── identity.bicep            # User-assigned managed identity
│   ├── app-insights.bicep        # Application Insights + Log Analytics workspace
│   ├── container-registry.bicep  # Azure Container Registry
│   ├── entra-app.bicep           # Entra ID app registrations (if applicable)
│   └── dns-zone.bicep            # Custom domain / DNS (optional)
├── environments/
│   ├── dev.bicepparam
│   ├── staging.bicepparam
│   └── prod.bicepparam
└── scripts/
    └── deploy.ps1                # Deployment script (az deployment sub create)
```

---

## Key Bicep Patterns

### 1. main.bicep — Orchestrator

```bicep
targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
param envName string

@description('Primary Azure region')
param location string = 'eastus2'

@description('Project name — used for resource naming')
param projectName string

// Resource Group
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${projectName}-${envName}'
  location: location
}

// Modules
module identity 'modules/identity.bicep' = {
  scope: rg
  name: 'identity'
  params: { projectName: projectName, envName: envName, location: location }
}

module appInsights 'modules/app-insights.bicep' = {
  scope: rg
  name: 'appInsights'
  params: { projectName: projectName, envName: envName, location: location }
}

module sql 'modules/sql-server.bicep' = {
  scope: rg
  name: 'sql'
  params: {
    projectName: projectName
    envName: envName
    location: location
    adminIdentityId: identity.outputs.principalId
  }
}

module redis 'modules/redis.bicep' = {
  scope: rg
  name: 'redis'
  params: { projectName: projectName, envName: envName, location: location }
}

module keyVault 'modules/key-vault.bicep' = {
  scope: rg
  name: 'keyVault'
  params: {
    projectName: projectName
    envName: envName
    location: location
    identityPrincipalId: identity.outputs.principalId
  }
}

module acr 'modules/container-registry.bicep' = {
  scope: rg
  name: 'acr'
  params: { projectName: projectName, envName: envName, location: location }
}

module containerEnv 'modules/container-apps-env.bicep' = {
  scope: rg
  name: 'containerEnv'
  params: {
    projectName: projectName
    envName: envName
    location: location
    logAnalyticsWorkspaceId: appInsights.outputs.logAnalyticsWorkspaceId
  }
}

module apiApp 'modules/container-app.bicep' = {
  scope: rg
  name: 'apiApp'
  params: {
    appName: '${projectName}-api'
    envName: envName
    location: location
    containerAppsEnvId: containerEnv.outputs.envId
    registryServer: acr.outputs.loginServer
    identityId: identity.outputs.identityId
    imageName: '${projectName}-api'
    env: [
      { name: 'ConnectionStrings__${projectName}DbContextTrxn', secretRef: 'sql-trxn-connstr' }
      { name: 'ConnectionStrings__${projectName}DbContextQuery', secretRef: 'sql-query-connstr' }
      { name: 'ConnectionStrings__Redis1', secretRef: 'redis-connstr' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
}

module gatewayApp 'modules/container-app.bicep' = {
  scope: rg
  name: 'gatewayApp'
  params: {
    appName: '${projectName}-gateway'
    envName: envName
    location: location
    containerAppsEnvId: containerEnv.outputs.envId
    registryServer: acr.outputs.loginServer
    identityId: identity.outputs.identityId
    imageName: '${projectName}-gateway'
    isExternalIngress: true  // Gateway is the public entry point
    env: [
      { name: 'ReverseProxy__Clusters__api-cluster__Destinations__api__Address', value: 'https://${apiApp.outputs.fqdn}' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
}

module schedulerApp 'modules/container-app.bicep' = {
  scope: rg
  name: 'schedulerApp'
  params: {
    appName: '${projectName}-scheduler'
    envName: envName
    location: location
    containerAppsEnvId: containerEnv.outputs.envId
    registryServer: acr.outputs.loginServer
    identityId: identity.outputs.identityId
    imageName: '${projectName}-scheduler'
    minReplicas: 1
    maxReplicas: 1
    env: [
      { name: 'ConnectionStrings__${projectName}DbContextTrxn', secretRef: 'sql-trxn-connstr' }
      { name: 'ConnectionStrings__${projectName}DbContextQuery', secretRef: 'sql-query-connstr' }
      { name: 'ConnectionStrings__SchedulerDbContext', secretRef: 'scheduler-sql-connstr' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
}
```

### 2. Reusable Container App Module

```bicep
// modules/container-app.bicep
param appName string
param envName string
param location string
param containerAppsEnvId string
param registryServer string
param identityId string
param imageName string
param isExternalIngress bool = false
param minReplicas int = 1
param maxReplicas int = 3
param cpu string = '0.5'
param memory string = '1Gi'
param env array = []

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-${envName}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvId
    configuration: {
      ingress: {
        external: isExternalIngress
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        { server: registryServer, identity: identityId }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: '${registryServer}/${imageName}:latest'
          resources: { cpu: json(cpu), memory: memory }
          env: env
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
output name string = app.name
```

### 3. SQL Server Module

```bicep
// modules/sql-server.bicep
param projectName string
param envName string
param location string
param adminIdentityId string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${projectName}-${envName}'
  location: location
  properties: {
    administrators: {
      azureADOnlyAuthentication: true    // Entra-only, no SQL auth
      principalType: 'Application'
      login: '${projectName}-identity'
      sid: adminIdentityId
    }
    minimalTlsVersion: '1.2'
  }
}

resource db 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: '${projectName}db-${envName}'
  location: location
  sku: {
    name: envName == 'prod' ? 'S1' : 'Basic'  // Scale by environment
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// Optional: read replica for Query DbContext in prod
resource dbReplica 'Microsoft.Sql/servers/databases@2023-08-01-preview' = if (envName == 'prod') {
  parent: sqlServer
  name: '${projectName}db-${envName}-replica'
  location: location
  sku: { name: 'S1' }
  properties: {
    createMode: 'Secondary'
    sourceDatabaseId: db.id
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output dbName string = db.name
```

---

## Aspire ↔ Bicep Consistency Checklist

When building or updating the Bicep templates, verify against the Aspire AppHost:

- [ ] **Every `AddSqlServer().AddDatabase()` call** has a matching `sql-server.bicep` module invocation
- [ ] **Every `AddRedis()` call** has a matching `redis.bicep` module invocation
- [ ] **Every `AddProject<T>()` call** has a matching `container-app.bicep` module invocation
- [ ] **Connection string names match exactly** — `WithReference(db, connectionName: "X")` → `ConnectionStrings__X`
- [ ] **Scheduler replica count = 1** — matching `.WithReplicas(1)` in Aspire
- [ ] **Scheduler connection name is present** — `ConnectionStrings__SchedulerDbContext` is configured in scheduler host
- [ ] **TickerQ schema deployment is explicit** — `TickerQ_Deployment.sql` is applied before scheduler rollout
- [ ] **Gateway is the only external ingress** — API and Scheduler are internal only
- [ ] **ServiceDefaults telemetry** maps to Application Insights + Log Analytics
- [ ] **Health check paths** are consistent between Aspire `AddDefaultHealthChecks()` and Container Apps probes
- [ ] **Environment variables** in Bicep match `appsettings.json` keys (use `__` for `:` nesting)
- [ ] **YARP cluster destinations** use Container Apps internal FQDN (not Aspire service names)

## Scheduler Schema Deployment (TickerQ)

TickerQ schema is not managed by normal EF migrations. Include a schema deployment step in your release process:

1. Generate/update `TickerQ_Deployment.sql` from Scheduler host in a controlled environment.
2. Publish the script as a deployment artifact.
3. Execute script against target SQL database before deploying/upgrading Scheduler containers.
4. Deploy Scheduler host after schema step succeeds.

This prevents startup failures like missing `[Scheduler].[TimeTickers]` in new environments.

---

## Security Best Practices

- **Managed Identity everywhere** — use user-assigned managed identity for ACR pull, SQL access, Key Vault access. No connection string passwords.
- **Entra-only SQL auth** — `azureADOnlyAuthentication: true`. No SQL login/password.
- **Key Vault for secrets** — connection strings with passwords (Redis) go in Key Vault. Apps access via managed identity + Key Vault reference.
- **Private endpoints** (prod) — SQL, Redis, Key Vault behind private endpoints in a VNet.
- **Minimal TLS 1.2** on all resources.
- **RBAC over access policies** — use Azure RBAC for Key Vault, not legacy access policies.

---

## Deployment

### Via Azure CLI

```powershell
# Dev environment
az deployment sub create `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/environments/dev.bicepparam

# Prod environment
az deployment sub create `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/environments/prod.bicepparam
```

### Via GitHub Actions

```yaml
- uses: azure/arm-deploy@v2
  with:
    scope: subscription
    subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    region: eastus2
    template: infra/main.bicep
    parameters: infra/environments/${{ inputs.environment }}.bicepparam
```

### Via Azure Developer CLI (azd)

If using `azd`, add an `azure.yaml` at the repo root that references the Bicep templates:

```yaml
name: {project-name}
infra:
  provider: bicep
  path: infra
services:
  api:
    project: src/{Host}/{Host}.Api
    host: containerapp
  gateway:
    project: src/{Gateway}/{Gateway}.Gateway
    host: containerapp
```

Then deploy with:

```bash
azd up
```

---

## App Service Alternative

If `deployTarget: AppService` (instead of Container Apps), replace the container app module with:

```bicep
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${projectName}-${envName}'
  location: location
  sku: { name: envName == 'prod' ? 'P1v3' : 'B1', tier: envName == 'prod' ? 'PremiumV3' : 'Basic' }
  kind: 'linux'
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${appName}-${envName}'
  location: location
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identityId}': {} } }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: env
      healthCheckPath: '/health'
    }
  }
}
```

---

## What to Generate

When the AI receives this skill, it should:

1. Read the project's Aspire `AppHost.cs` (if it exists) to discover all resources
2. Generate Bicep modules matching each Aspire resource (or each enabled project input)
3. Verify connection string names are identical between Aspire and Bicep
4. Generate `main.bicep` orchestrating all modules
5. Generate per-environment `.bicepparam` files (dev, staging, prod)
6. Generate a `deploy.ps1` deployment script
7. Optionally generate GitHub Actions workflow for CI/CD deployment

> **Always validate:** After generating Bicep, run `az bicep build --file infra/main.bicep` to check for compilation errors.

---

## Dockerfiles

Each deployable project requires a Dockerfile for containerized deployment. Dockerfiles are **not needed for local development** — Aspire runs projects directly via `dotnet run`. They are only required when deploying to Container Apps, AKS, or any container-based hosting.

Place Dockerfiles in each project directory and use `src/` as the Docker build context so all project references resolve:

```dockerfile
# File: src/{Host}/{Host}.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files
COPY Directory.Packages.props .
COPY nuget.config .
COPY global.json .
COPY *.slnx .

# Copy all .csproj files for restore (cache-efficient layer)
COPY Domain/Domain.Model/*.csproj Domain/Domain.Model/
COPY Domain/Domain.Shared/*.csproj Domain/Domain.Shared/
COPY Application/Application.Contracts/*.csproj Application/Application.Contracts/
COPY Application/Application.Models/*.csproj Application/Application.Models/
COPY Application/Application.Services/*.csproj Application/Application.Services/
COPY Application/Application.MessageHandlers/*.csproj Application/Application.MessageHandlers/
COPY Infrastructure/Infrastructure.Data/*.csproj Infrastructure/Infrastructure.Data/
COPY Infrastructure/Infrastructure.Repositories/*.csproj Infrastructure/Infrastructure.Repositories/
COPY Infrastructure/Infrastructure.Utility/*.csproj Infrastructure/Infrastructure.Utility/
COPY {Host}/{Host}.Bootstrapper/*.csproj {Host}/{Host}.Bootstrapper/
COPY {Host}/{Host}.Api/*.csproj {Host}/{Host}.Api/

RUN dotnet restore {Host}/{Host}.Api/{Host}.Api.csproj

# Copy everything and build
COPY . .
RUN dotnet publish {Host}/{Host}.Api/{Host}.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "{Host}.Api.dll"]
```

Each deployable host (API, Gateway, Scheduler, Notification, Functions) follows the same multi-stage pattern. Adapt the `COPY` layer and `ENTRYPOINT` for the target project.

> **Build context:** Always `docker build -f src/{Host}/{Host}.Api/Dockerfile src/` — the context is `src/`, not the project directory.

---

## Lite Mode Considerations

In **Lite mode** (`scaffoldMode: lite`), the IaC is simplified:

- No Gateway container app (API is the only deployable)
- No Redis module (caching disabled)
- Single DbContext — only one connection string (`ConnectionStrings__{Project}DbContext`)
- No read replica
- Simpler `main.bicep` with fewer module invocations
- App Service is often a better fit than Container Apps for Lite mode projects

---

## Verification

After generating the IaC templates, verify:

- [ ] Every `AddProject<T>()` in Aspire AppHost has a matching `container-app.bicep` module invocation in `main.bicep`
- [ ] Connection string names in Bicep env vars match exactly with Aspire `connectionName` values
- [ ] Scheduler is configured with `minReplicas: 1`, `maxReplicas: 1`
- [ ] Gateway is the only container app with `isExternalIngress: true`
- [ ] `az bicep build --file infra/main.bicep` compiles without errors
- [ ] Per-environment `.bicepparam` files exist for each target environment (dev, staging, prod)
- [ ] Managed identity is used for ACR pull, SQL access, and Key Vault — no passwords in Bicep
- [ ] Dockerfiles build successfully: `docker build -t test -f src/{Host}/{Host}.Api/Dockerfile src/`
- [ ] If Notification is a standalone microservice, it has its own container app module and Dockerfile
