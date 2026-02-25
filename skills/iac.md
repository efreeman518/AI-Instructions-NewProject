# Infrastructure as Code — Azure Bicep

Use this skill to generate `infra/` Bicep templates that mirror the runtime topology.

## Prerequisites

- API/core hosts scaffolded
- Aspire AppHost available (if used)
- Azure CLI + Bicep CLI installed

---

## Aspire ↔ Azure Mapping

| Aspire resource | Azure resource | Bicep type |
|---|---|---|
| `AddSqlServer().AddDatabase()` | Azure SQL server + DB | `Microsoft.Sql/servers`, `Microsoft.Sql/servers/databases` |
| `AddRedis()` | Azure Cache for Redis | `Microsoft.Cache/redis` |
| `AddProject<Api/Gateway/Scheduler/...>()` | Container App or App Service | `Microsoft.App/containerApps` / `Microsoft.Web/sites` |
| ServiceDefaults telemetry | App Insights + Log Analytics | `Microsoft.Insights/components`, `Microsoft.OperationalInsights/workspaces` |

Rule: every `WithReference(resource, connectionName: "X")` in Aspire requires `ConnectionStrings__X` in app env settings.

---

## File Layout

```text
infra/
  main.bicep
  environments/
    dev.bicepparam
    staging.bicepparam
    prod.bicepparam
  modules/
    identity.bicep
    app-insights.bicep
    sql-server.bicep
    redis.bicep
    key-vault.bicep
    container-registry.bicep
    container-apps-env.bicep
    container-app.bicep
  scripts/
    deploy.ps1
```

---

## Core Patterns

### `main.bicep` orchestration

```bicep
targetScope = 'subscription'

param envName string
param location string = 'eastus2'
param projectName string

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${projectName}-${envName}'
  location: location
}

module identity 'modules/identity.bicep' = {
  scope: rg
  name: 'identity'
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
```

### Reusable container app module

```bicep
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
```

### SQL module baseline

```bicep
param projectName string
param envName string
param location string
param adminIdentityId string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-${projectName}-${envName}'
  location: location
  properties: {
    administrators: {
      azureADOnlyAuthentication: true
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
}
```

---

## Required Consistency Rules

1. One AppHost project => one deployable host module.
2. Connection names match exactly (`WithReference` ↔ `ConnectionStrings__*`).
3. Gateway is the only external ingress by default.
4. Scheduler stays single replica (`minReplicas = maxReplicas = 1`) when using TickerQ.
5. ServiceDefaults telemetry maps to App Insights + Log Analytics.
6. Keep Aspire resource names, IaC names, and appsettings keys aligned.

### TickerQ schema deployment

Apply `TickerQ_Deployment.sql` before scheduler rollout; this schema is not a standard app EF migration.

---

## Security Rules

- Use managed identity for ACR pull, SQL, Key Vault, App Config.
- Keep SQL Entra-only (`azureADOnlyAuthentication: true`).
- Put secrets in Key Vault / secret refs, not plain Bicep values.
- Prefer private endpoints in production.

---

## Deployment

### Azure CLI

```powershell
az deployment sub create `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/environments/dev.bicepparam
```

### GitHub Actions

```yaml
- uses: azure/arm-deploy@v2
  with:
    scope: subscription
    template: infra/main.bicep
    parameters: infra/environments/${{ inputs.environment }}.bicepparam
```

### azd

```yaml
name: {project-name}
infra:
  provider: bicep
  path: infra
services:
  api:
    project: src/{Host}/{Host}.Api
    host: containerapp
```

---

## Dockerfile Requirement

Container deployments require per-host Dockerfiles. Use `src/` as build context.

```bash
docker build -f src/{Host}/{Host}.Api/Dockerfile src/
```

---

## Lite Mode

For `scaffoldMode: lite`:
- Usually one API host
- Single DbContext connection key
- No Redis module unless explicitly enabled
- Simplified `main.bicep`

## Verification

- [ ] `az bicep build --file infra/main.bicep` succeeds
- [ ] Env vars match runtime config keys
- [ ] Aspire resources and IaC modules align
- [ ] Scheduler replica and scheduler DB settings are correct
- [ ] Deploy parameters exist per target environment
