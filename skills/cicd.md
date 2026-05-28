# CI/CD - GitHub Actions

See dockerfile-template.md for container patterns.

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
6. **Private NuGet feed auth:** If the solution references packages from authenticated feeds (e.g., GitHub Packages), the workflow must authenticate before `dotnet restore`. Store a PAT as a repo secret (e.g., `NUGET_PAT`) and add an auth step. Without this, restore fails with `NU1301 / 401 Unauthorized`. See the NuGet auth step below.

---

## Recommended Workflow Layout

```
.github/
|-- workflows/
|   |-- ci.yml
|   |-- cd.yml
|   `-- infra.yml        # optional
`-- actions/
    `-- dotnet-build/    # optional composite action
```

---

## `ci.yml` (PR Validation)

Run on PRs to `main`/`develop`:

- checkout
- setup .NET from `src/global.json`
- `dotnet restore`
- `dotnet build --no-restore`
- targeted test runs by category (Endpoint path by default, broader Integration path optionally gated)
- publish TRX and coverage artifacts

Minimal shape:

```yaml
name: CI

on:
  pull_request:
    branches: [main, develop]
    paths-ignore: ['**.md', 'infra/**']
  workflow_dispatch:
    inputs:
      includeIntegration:
        type: boolean
        default: false
        description: "Run non-endpoint integration tests"

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: src/global.json

      # Install extra workloads if solution includes WASM/Uno projects
      # - run: dotnet workload install wasm-tools

      # Private NuGet feed auth (if nuget.config references authenticated feeds)
      - name: Authenticate private NuGet feed
        env:
          NUGET_PAT: ${{ secrets.NUGET_PAT }}
        if: env.NUGET_PAT != ''
        run: >
          dotnet nuget update source "{FeedName}"
          --username "ci" --password "$NUGET_PAT"
          --store-password-in-clear-text
          --configfile src/nuget.config

      - run: dotnet restore src/{SolutionName}.slnx

      # Vulnerability audit per support/execution-gates.md section Vulnerability Audit
      # High severity must be fixed or recorded in .scaffold/INSTRUCTION-GAPS.md
      - name: Vulnerability audit
        run: |
          dotnet list src/{SolutionName}.slnx package --vulnerable --include-transitive 2>&1 | tee vuln.log
          if grep -E '\bHigh\b' vuln.log; then
            echo "::warning::High-severity vulnerable packages detected. Verify each is documented in .scaffold/INSTRUCTION-GAPS.md."
          fi

      - run: dotnet build src/{SolutionName}.slnx --no-restore --configuration Release

      # Target specific test projects to avoid "No test matches" noise from unrelated projects
      - run: dotnet test src/Test/Test.Unit/Test.Unit.csproj --no-build --configuration Release
      - run: dotnet test src/Test/Test.Endpoints/Test.Endpoints.csproj --no-build --configuration Release
      - run: dotnet test src/Test/Test.Architecture/Test.Architecture.csproj --no-build --configuration Release
      - if: ${{ github.event_name == 'workflow_dispatch' && inputs.includeIntegration == true }}
        run: dotnet test src/Test/Test.Integration/Test.Integration.csproj --no-build --configuration Release
      - if: ${{ github.event_name == 'workflow_dispatch' && inputs.includeE2E == true }}
        run: dotnet test src/Test/Test.E2E/Test.E2E.csproj --no-build --configuration Release
      # Test.PlaywrightUI requires a hosted stack - see "Hosted-stack orchestration" section below.
```

### Test Category Policy

    | Category | PR Default | Optional / Manual |
    |---|---|---|
    | `Unit` | required | - |
    | `Endpoint` | required (WebApplicationFactory contract coverage) | - |
    | `Architecture` | required | - |
    | `Integration` | - | service-level vs real external services (workflow dispatch; Testcontainers required) |
    | `E2E` | - | multi-endpoint workflow chains (workflow dispatch) |
    | `PlaywrightUI` | - | browser UI; requires hosted stack (release/nightly) |
    | `Load`, `Benchmark`, `Mutation` | - | release/on-demand only |

### Hosted-Stack Orchestration (`Test.PlaywrightUI`)

Playwright tests cannot use `WebApplicationFactory` - they drive a real browser and need real Kestrel + UI host. The CI job must launch the stack, wait for health, run the tests, then tear down. For React/Vite, pass the actual UI resource URL from the hosted stack into the test project (for example `{APP}_REACT_BASE_URL`); do not assume the local Vite default port.

```yaml
playwright:
  runs-on: ubuntu-latest
  needs: [build]
  if: github.event_name == 'workflow_dispatch' && inputs.includePlaywright == true
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
    - run: dotnet build src/{SolutionName}.slnx --configuration Release
    - name: Start Aspire AppHost
      run: |
        dotnet run --project src/Host/Aspire/AppHost --configuration Release &
        echo "APPHOST_PID=$!" >> $GITHUB_ENV
    - name: Wait for stack health
      run: |
        for i in {1..30}; do
          if curl -sf http://localhost:5100/health; then exit 0; fi
          sleep 2
        done
        exit 1
    - name: Install Playwright browsers
      run: pwsh src/Test/Test.PlaywrightUI/bin/Release/$(TargetFramework)/playwright.ps1 install --with-deps
    - name: Run Playwright tests
      env:
        PLAYWRIGHT_BASE_URL: http://localhost:5100
      run: dotnet test src/Test/Test.PlaywrightUI/Test.PlaywrightUI.csproj --no-build --configuration Release
    - name: Stop AppHost
      if: always()
      run: kill $APPHOST_PID || true
```

For PR-time runs, gate `Test.PlaywrightUI` to nightly to keep PR loops fast.

If `Test.PlaywrightUI` is a Node Playwright suite for React/Vite, replace the browser install/run steps with `npm ci`, `npx playwright install --with-deps`, and `node node_modules/@playwright/test/cli.js test --project react`, while keeping the same hosted-stack startup/teardown contract.

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
            dockerfile: src/Host/{Host}.Api/Dockerfile
          - name: gateway
            dockerfile: src/Host/{Gateway}.Gateway/Dockerfile
          - name: scheduler
            dockerfile: src/Host/{Host}.Scheduler/Dockerfile
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
8. Keep token placeholders aligned with [placeholder-tokens.md](../ai/placeholder-tokens.md).

---

## Verification

- [ ] `ci.yml` runs restore/build and required PR test categories
- [ ] `cd.yml` logs in with OIDC and pushes SHA-tagged images
- [ ] deployment step updates correct environment resources
- [ ] scheduler deployment order includes prerequisite schema step
- [ ] `infra.yml` validates and deploys Bicep when enabled
- [ ] repo secrets/variables/environments are configured and protected
