# CI/CD — GitHub Actions

## Prerequisites

- [solution-structure.md](solution-structure.md) — project layout and build conventions
- [iac.md](iac.md) — Azure Bicep templates (if deploying infrastructure)
- [testing.md](testing.md) — test project layout and categories

## Overview

CI/CD uses **GitHub Actions** to build, test, containerize, and deploy the solution. Pipelines are structured as reusable workflows: a **CI workflow** (build + test on every PR) and a **CD workflow** (build → push container images → deploy per environment).

---

## Workflow Structure

```
.github/
├── workflows/
│   ├── ci.yml                    # PR validation — build + test
│   ├── cd.yml                    # Deploy — build → push ACR → deploy to Container Apps
│   └── infra.yml                 # (Optional) IaC deployment — Bicep
├── actions/
│   └── dotnet-build/
│       └── action.yml            # Reusable composite action for build + test
└── CODEOWNERS                    # (Optional) PR review assignments
```

---

## CI Workflow (Pull Request Validation)

```yaml
# .github/workflows/ci.yml
name: CI

on:
  pull_request:
    branches: [main, develop]
    paths-ignore:
      - '**.md'
      - 'infra/**'
  workflow_dispatch:

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  SOLUTION_PATH: src/{SolutionName}.slnx

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    timeout-minutes: 30

    services:
      sql:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          MSSQL_SA_PASSWORD: ${{ secrets.CI_SQL_PASSWORD }}
        ports:
          - 1433:1433
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $MSSQL_SA_PASSWORD -Q 'SELECT 1' -C"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: src/global.json

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

      - name: Unit Tests
        run: >
          dotnet test ${{ env.SOLUTION_PATH }}
          --no-build --configuration Release
          --filter "TestCategory=Unit"
          --logger "trx;LogFileName=unit-results.trx"
          --collect:"XPlat Code Coverage"
          --results-directory ./TestResults

      - name: Endpoint Tests
        run: >
          dotnet test ${{ env.SOLUTION_PATH }}
          --no-build --configuration Release
          --filter "TestCategory=Endpoint"
          --logger "trx;LogFileName=endpoint-results.trx"
          --results-directory ./TestResults
        env:
          ConnectionStrings__{Project}DbContextTrxn: "Server=localhost;Database={Project}Db_CI;User Id=sa;Password=${{ secrets.CI_SQL_PASSWORD }};TrustServerCertificate=true"

      - name: Architecture Tests
        run: >
          dotnet test ${{ env.SOLUTION_PATH }}
          --no-build --configuration Release
          --filter "TestCategory=Architecture"
          --logger "trx;LogFileName=arch-results.trx"
          --results-directory ./TestResults

      - name: Publish Test Results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Test Results
          path: TestResults/*.trx
          reporter: dotnet-trx

      - name: Upload Coverage
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: coverage
          path: TestResults/**/coverage.cobertura.xml
```

### Test Filtering by Category

| Category | `--filter` | When to Run |
|----------|-----------|-------------|
| `Unit` | `TestCategory=Unit` | Every PR (fast, no dependencies) |
| `Endpoint` | `TestCategory=Endpoint` | Every PR (needs SQL service container) |
| `Integration` | `TestCategory=Integration` | Every PR (with Testcontainers) |
| `Architecture` | `TestCategory=Architecture` | Every PR (fast, in-process) |
| `E2E` | `TestCategory=E2E` | Pre-deploy (Playwright, needs running host) |
| `Load` | `TestCategory=Load` | On-demand / release gate |
| `Benchmark` | `TestCategory=Benchmark` | On-demand (BenchmarkDotNet) |

---

## CD Workflow (Build → Push → Deploy)

```yaml
# .github/workflows/cd.yml
name: CD

on:
  push:
    branches: [main]
  workflow_dispatch:
    inputs:
      environment:
        description: 'Target environment'
        required: true
        default: 'dev'
        type: choice
        options: [dev, staging, prod]

permissions:
  id-token: write   # OIDC for Azure login
  contents: read

env:
  REGISTRY: ${{ vars.ACR_LOGIN_SERVER }}

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    strategy:
      matrix:
        project:
          - name: api
            context: src
            dockerfile: src/{Host}/{Host}.Api/Dockerfile
          - name: gateway
            context: src
            dockerfile: src/{Gateway}/{Gateway}.Gateway/Dockerfile
          - name: scheduler
            context: src
            dockerfile: src/{Host}/{Host}.Scheduler/Dockerfile
          # Add more deployable projects as needed

    steps:
      - uses: actions/checkout@v4

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: ACR Login
        run: az acr login --name ${{ vars.ACR_NAME }}

      - name: Build & Push Image
        run: |
          IMAGE=${{ env.REGISTRY }}/${{ matrix.project.name }}:${{ github.sha }}
          IMAGE_LATEST=${{ env.REGISTRY }}/${{ matrix.project.name }}:latest
          docker build -t $IMAGE -t $IMAGE_LATEST -f ${{ matrix.project.dockerfile }} ${{ matrix.project.context }}
          docker push $IMAGE
          docker push $IMAGE_LATEST

  deploy:
    needs: build-and-push
    runs-on: ubuntu-latest
    environment: ${{ inputs.environment || 'dev' }}
    timeout-minutes: 15

    steps:
      - uses: actions/checkout@v4

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy Container Apps
        run: |
          ENV=${{ inputs.environment || 'dev' }}
          TAG=${{ github.sha }}
          
          # Update each container app with the new image
          az containerapp update \
            --name ${{ vars.PROJECT_NAME }}-api-${ENV} \
            --resource-group rg-${{ vars.PROJECT_NAME }}-${ENV} \
            --image ${{ env.REGISTRY }}/api:${TAG}
          
          az containerapp update \
            --name ${{ vars.PROJECT_NAME }}-gateway-${ENV} \
            --resource-group rg-${{ vars.PROJECT_NAME }}-${ENV} \
            --image ${{ env.REGISTRY }}/gateway:${TAG}
          
          az containerapp update \
            --name ${{ vars.PROJECT_NAME }}-scheduler-${ENV} \
            --resource-group rg-${{ vars.PROJECT_NAME }}-${ENV} \
            --image ${{ env.REGISTRY }}/scheduler:${TAG} \
            --min-replicas 1 --max-replicas 1

      - name: Deploy TickerQ Schema (if Scheduler)
        if: ${{ vars.INCLUDE_SCHEDULER == 'true' }}
        run: |
          # Apply TickerQ schema before restarting scheduler
          sqlcmd -S ${{ secrets.SQL_SERVER_FQDN }} -d ${{ vars.PROJECT_NAME }}db-${ENV} \
            --authentication-method=ActiveDirectoryDefault \
            -i infra/scripts/TickerQ_Deployment.sql
```

---

## Infrastructure Workflow (Bicep Deployment)

```yaml
# .github/workflows/infra.yml
name: Infrastructure

on:
  workflow_dispatch:
    inputs:
      environment:
        description: 'Target environment'
        required: true
        type: choice
        options: [dev, staging, prod]

permissions:
  id-token: write
  contents: read

jobs:
  deploy-infra:
    runs-on: ubuntu-latest
    environment: ${{ inputs.environment }}
    timeout-minutes: 30

    steps:
      - uses: actions/checkout@v4

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Validate Bicep
        run: az bicep build --file infra/main.bicep

      - name: Deploy Infrastructure
        uses: azure/arm-deploy@v2
        with:
          scope: subscription
          subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          region: ${{ vars.AZURE_REGION }}
          template: infra/main.bicep
          parameters: infra/environments/${{ inputs.environment }}.bicepparam
          failOnStdErr: false
```

---

## Dockerfile Pattern

Each deployable project needs a Dockerfile. Place Dockerfiles in the project directory and use the `src/` folder as the build context so all project references resolve correctly.

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

# Restore
RUN dotnet restore {Host}/{Host}.Api/{Host}.Api.csproj

# Copy everything and build
COPY . .
RUN dotnet publish {Host}/{Host}.Api/{Host}.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "{Host}.Api.dll"]
```

### Dockerfile for Gateway

Same structure as API, replacing project paths:

```dockerfile
# File: src/{Gateway}/{Gateway}.Gateway/Dockerfile
# ... same multi-stage pattern, targeting {Gateway}.Gateway.csproj
```

### Dockerfile for Scheduler

Same pattern, but include TickerQ-specific NuGet packages:

```dockerfile
# File: src/{Host}/{Host}.Scheduler/Dockerfile
# ... same multi-stage pattern, targeting {Host}.Scheduler.csproj
```

> **Note:** For local development, Dockerfiles are **not needed** — Aspire runs projects directly via `dotnet run`. Dockerfiles are only required for cloud deployment to Container Apps or AKS.

---

## GitHub Repository Settings

### Required Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_CLIENT_ID` | Service principal or workload identity client ID (for OIDC) |
| `AZURE_TENANT_ID` | Azure Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target Azure subscription |
| `CI_SQL_PASSWORD` | SA password for CI SQL Server service container |

### Required Variables

| Variable | Description |
|----------|-------------|
| `ACR_LOGIN_SERVER` | ACR login server (e.g., `myacr.azurecr.io`) |
| `ACR_NAME` | ACR name (e.g., `myacr`) |
| `PROJECT_NAME` | Project name (lowercase, used in resource naming) |
| `AZURE_REGION` | Primary Azure region (e.g., `eastus2`) |
| `INCLUDE_SCHEDULER` | `true` or `false` — whether to deploy scheduler schema |

### Environments

Create GitHub environments (`dev`, `staging`, `prod`) with:
- **Environment protection rules** for `staging` and `prod` (required reviewers)
- **Environment-specific secrets/variables** if connection strings or regions differ

---

## Lite Mode Considerations

In **Lite mode** (`scaffoldMode: lite`):

- CI workflow only runs Unit tests (no endpoint tests, no SQL service container)
- CD workflow deploys a single container (API only — no gateway, no scheduler)
- No infrastructure workflow (deploy manually or via `az webapp deploy`)
- Dockerfile is simpler (fewer project layers)

---

## Rules

1. **OIDC over secrets** — use workload identity federation (`id-token: write`) instead of storing Azure credentials as secrets.
2. **Build context = `src/`** — Dockerfiles reference project paths relative to the `src/` folder so all `<ProjectReference>` items resolve.
3. **Multi-stage builds** — always use multi-stage Dockerfiles (restore → build → publish → runtime) for minimal image size.
4. **Tag with SHA** — tag container images with `${{ github.sha }}` for traceability. Also push `:latest` for convenience.
5. **Test filtering** — use `--filter "TestCategory=..."` to run specific test categories. Never run E2E or load tests on every PR.
6. **Schema before deploy** — TickerQ schema SQL must run before the Scheduler container is deployed to avoid missing table errors.
7. **Validate Bicep in CI** — run `az bicep build` as a validation step before deployment.
8. **Environment protection** — require manual approval for `staging` and `prod` deployments.
9. **Placeholder tokens** — see [placeholder-tokens.md](../placeholder-tokens.md) for all token definitions.

---

## Verification

After creating the CI/CD workflows, verify:

- [ ] `ci.yml` triggers on PRs to `main`/`develop` and runs Unit + Endpoint + Architecture tests
- [ ] `cd.yml` builds Docker images for each deployable project and pushes to ACR
- [ ] `cd.yml` deploys container apps with the correct image tag
- [ ] `infra.yml` validates and deploys Bicep templates
- [ ] Dockerfiles build successfully: `docker build -t test -f src/{Host}/{Host}.Api/Dockerfile src/`
- [ ] GitHub repository has required secrets and variables configured
- [ ] Environment protection rules exist for `staging` and `prod`
- [ ] Test categories in CI match the `[TestCategory]` attributes in test projects
- [ ] Scheduler deployment includes TickerQ schema step before container update
