# CI/CD — GitHub Actions

## Prerequisites

- [solution-structure.md](solution-structure.md)
- [iac.md](iac.md)
- [testing.md](testing.md)

## Purpose

Use GitHub Actions for:

1. CI on pull requests (restore/build/test).
2. CD on protected branches (build image, push, deploy by environment).
3. Optional IaC deployment (`infra.yml`) when Bicep is enabled.

## Non-Negotiables

1. Use OIDC (`id-token: write`) for Azure auth; do not store cloud credentials as static secrets.
2. Promotion path is explicit (`dev -> staging -> prod`) with environment protections.
3. Deploy artifacts are immutable by commit SHA tag.
4. Scheduler schema/dependency steps run before scheduler rollout.
5. PR CI excludes heavy suites (E2E/load/benchmarks unless explicitly gated).

---

## Recommended Workflow Layout

```
.github/
├── workflows/
│   ├── ci.yml
│   ├── cd.yml
│   └── infra.yml        # optional
└── actions/
    └── dotnet-build/    # optional composite action
```

---

## `ci.yml` (PR Validation)

Run on PRs to `main`/`develop`:

- checkout
- setup .NET from `src/global.json`
- `dotnet restore`
- `dotnet build --no-restore`
- targeted test runs by category
- publish TRX and coverage artifacts

Minimal shape:

```yaml
name: CI

on:
  pull_request:
    branches: [main, develop]
    paths-ignore: ['**.md', 'infra/**']
  workflow_dispatch:

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: src/global.json
      - run: dotnet restore src/{SolutionName}.slnx
      - run: dotnet build src/{SolutionName}.slnx --no-restore --configuration Release
      - run: dotnet test src/{SolutionName}.slnx --no-build --filter "TestCategory=Unit"
      - run: dotnet test src/{SolutionName}.slnx --no-build --filter "TestCategory=Endpoint"
      - run: dotnet test src/{SolutionName}.slnx --no-build --filter "TestCategory=Architecture"
```

### Test Category Policy

| Category | Default in PR CI |
|---|---|
| `Unit` | required |
| `Endpoint` | required (if SQL-dependent endpoints exist) |
| `Architecture` | required |
| `Integration` | optional by repo policy |
| `E2E`, `Load`, `Benchmark` | release/on-demand only |

---

## `cd.yml` (Build, Push, Deploy)

Run on `main` and manual dispatch with `environment` input.

### Job A: Build and Push

- login via `azure/login@v2` + OIDC
- ACR login
- build each deployable image
- push both `:${{ github.sha }}` and optional `:latest`

### Job B: Deploy

- protected environment (`dev`, `staging`, `prod`)
- update host images using SHA tag
- keep scheduler replicas pinned when no coordination layer exists

Minimal shape:

```yaml
name: CD

on:
  push:
    branches: [main]
  workflow_dispatch:
    inputs:
      environment:
        type: choice
        options: [dev, staging, prod]

permissions:
  id-token: write
  contents: read

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        project:
          - name: api
            dockerfile: src/{Host}/{Host}.Api/Dockerfile
          - name: gateway
            dockerfile: src/{Gateway}/{Gateway}.Gateway/Dockerfile
          - name: scheduler
            dockerfile: src/{Host}/{Host}.Scheduler/Dockerfile
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - run: az acr login --name ${{ vars.ACR_NAME }}
      - run: docker build -t ${{ vars.ACR_LOGIN_SERVER }}/${{ matrix.project.name }}:${{ github.sha }} -f ${{ matrix.project.dockerfile }} src
      - run: docker push ${{ vars.ACR_LOGIN_SERVER }}/${{ matrix.project.name }}:${{ github.sha }}
```

---

## `infra.yml` (Optional Bicep)

Use manual dispatch per environment:

1. OIDC login
2. `az bicep build --file infra/main.bicep`
3. deploy with `azure/arm-deploy@v2`

Run infra separately from app rollout unless the team explicitly couples both.

---

## Dockerfile Contract

Each deployable host has a Dockerfile in its project folder and uses `src/` as build context.

Required pattern:

1. Multi-stage (`restore -> publish -> runtime`).
2. Copy solution/package files before source for restore-layer caching.
3. Runtime image is minimal and only contains published outputs.

---

## Repository Configuration

### Required Secrets

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `CI_SQL_PASSWORD` (if SQL service container used in CI)

### Required Variables

- `ACR_LOGIN_SERVER`
- `ACR_NAME`
- `PROJECT_NAME`
- `AZURE_REGION`
- `INCLUDE_SCHEDULER`

### Environments

Create `dev`, `staging`, `prod` with protection rules on `staging` and `prod`.

---

## Lite Mode

For `scaffoldMode: lite`:

- CI: unit tests only.
- CD: API image only.
- Infra workflow optional/manual.
- simplified Dockerfile/project matrix.

---

## Deployment Guardrails

1. OIDC only for Azure auth in workflows.
2. Build context remains `src/` for correct project-reference resolution.
3. Deploy by SHA tag; avoid mutable-only deploy references.
4. Schema prerequisites (such as scheduler tables) are applied before rolling dependent services.
5. Keep environment promotions explicit and approval-gated.
6. Validate Bicep before deploy.
7. Never run E2E/load/benchmark in default PR path.
8. Keep token placeholders aligned with [placeholder-tokens.md](../placeholder-tokens.md).

---

## Verification

- [ ] `ci.yml` runs restore/build and required PR test categories
- [ ] `cd.yml` logs in with OIDC and pushes SHA-tagged images
- [ ] deployment step updates correct environment resources
- [ ] scheduler deployment order includes prerequisite schema step
- [ ] `infra.yml` validates and deploys Bicep when enabled
- [ ] repo secrets/variables/environments are configured and protected