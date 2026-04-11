# Execution Gates & Validation

Single source of truth for compile/test/run checkpoints across implementation phases.

Use this file for:
- operator setup and preflight verification,
- phase-by-phase validation commands,
- exit criteria,
- pre-merge quality gates.

If another file disagrees, this file wins.

---

## Operator Setup (Pre-Scaffold)

Run once per machine/repo before beginning any scaffolding phase.

### Scope Selection (Pick One)

- [ ] **API-only baseline**: Foundation + App Core + API verification
- [ ] **API + services**: API baseline + Gateway/Aspire/Scheduler as enabled
- [ ] **Full app**: API + services + Function App + Uno UI + IaC as enabled

### Development Tools

- [ ] Git repo initialized with `.gitignore` for .NET
- [ ] `.NET SDK` installed (`dotnet --version`)
- [ ] Docker running (if Aspire uses SQL/Redis containers)
- [ ] `nuget.config` includes `nuget.org` + all custom/private feeds (see Private Feed Auth below)
- [ ] EF tools installed (`dotnet tool install -g dotnet-ef`)
- [ ] Functions Core Tools installed (`func --version`) *(if using Functions)*
- [ ] Uno templates installed (`dotnet new install Uno.Templates`) *(if using Uno UI)*
- [ ] Uno.Check installed (`dotnet tool install -g uno.check`) *(if using Uno UI)*
- [ ] Kiota CLI installed (`dotnet tool install -g Microsoft.OpenApi.Kiota`) *(if using Uno UI)*

### Private NuGet Feed Auth (Phase 3 Pre-Flight)

If the project uses private NuGet packages (e.g., EF.Packages), configure feed authentication before Phase 4.

**Step 1:** Ask the user for:
- Feed URL (e.g., `https://nuget.pkg.github.com/{owner}/index.json`)
- Auth method: environment variable (recommended) or credential provider

**Step 2:** Generate `nuget.config` with `packageSourceMapping` and `packageSourceCredentials`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="privatefeed" value="https://nuget.pkg.github.com/{owner}/index.json" />
  </packageSources>

  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="privatefeed">
      <package pattern="EF.*" />
    </packageSource>
  </packageSourceMapping>

  <packageSourceCredentials>
    <privatefeed>
      <add key="Username" value="{username}" />
      <add key="ClearTextPassword" value="%NUGET_AUTH_TOKEN%" />
    </privatefeed>
  </packageSourceCredentials>
</configuration>
```

**Step 3:** Set the auth token via environment variable:

```powershell
# PowerShell — session-scoped (recommended for local dev)
$env:NUGET_AUTH_TOKEN = "ghp_xxxxxxxxxxxxxxxxxxxx"

# Or persist in user profile (PowerShell $PROFILE)
[Environment]::SetEnvironmentVariable("NUGET_AUTH_TOKEN", "ghp_xxxx", "User")
```

> **Security:** Never commit PATs to source control. Use `%VARIABLE%` syntax in `nuget.config` (Windows) or `$VARIABLE` (Linux/Mac) for credential interpolation. Add `.env` and `nuget.config.local` to `.gitignore`.

**Step 4:** Verify:

```powershell
dotnet restore
```

Gate: exit code 0, all EF.Packages resolve successfully.

### AI Assistant — MCP Servers

Configure these in your AI client (VS Code `settings.json` or Claude Desktop config) so the AI can look up current docs and interact with tools during scaffolding.

- [ ] MCP servers configured per [../README.md](../README.md) (Essential + phase-relevant)

---

## Core Loop (Run After Each Scaffolding Sub-Phase)

Run from solution root:

```powershell
dotnet restore
dotnet build
dotnet test --filter "TestCategory=Unit"
```

Gate passes when all three commands succeed.

**TDD note:** For Phase 5a/5b, the TDD protocol expects tests to fail (red) before implementation and pass (green) after. The core loop verifies the green state. See [../ai/tdd-protocol.md](../ai/tdd-protocol.md).

---

## Phase Gates

## 4 — Contract Scaffolding

Required:
- solution structure compiles (`.slnx`, all project files, `Directory.Packages.props`),
- all interfaces, DTOs, entity shells, and no-op stubs compile,
- test projects compile (Test.Support, Test.Unit, Test.Integration, profile-specific projects).

Exit criteria:
- [ ] Solution structure matches `skills/solution-structure.md`
- [ ] Every entity from `resource-implementation.yaml` has: interface, DTO, entity shell, builders
- [ ] All no-op stubs satisfy their interfaces
- [ ] `RegisterServices.cs` wires all no-op stubs
- [ ] Test.Support contains `UnitTestBase`, `InMemoryDbBuilder`, `DbSupport`, `Utility`, `TestConstants`
- [ ] `{Entity}DtoBuilder` returns valid DTOs
- [ ] No domain logic in entity shells (only `throw new NotImplementedException`)

Commands:

```powershell
dotnet build
```

No `dotnet test` required — test projects are empty or contain no test methods.

---

## 5a — Foundation (TDD)

Required:
- domain + data-access projects build,
- DbContext + repository wiring is present,
- all domain entity tests, domain rule tests, and repository tests pass.

Exit criteria:
- [ ] Domain entities exist with real logic (shells replaced)
- [ ] Domain rule tests pass
- [ ] Repository tests pass with `InMemoryDbBuilder`
- [ ] `{Entity}Builder.Build()` activated (returns valid entities)
- [ ] No-op repository stubs replaced with real implementations in `RegisterServices.cs`
- [ ] DbContext files compile with EF configurations

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Unit"
```

Scaffold migration (remove old, create fresh baseline — see [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md)):

```powershell
# Remove any existing migrations first
dotnet ef migrations remove --force `
  --project src/Infrastructure/{Project}.Infrastructure.Data `
  --startup-project src/{Host}/{Host}.Api

# Create a clean baseline
dotnet ef migrations add InitialCreate `
  --project src/Infrastructure/{Project}.Infrastructure.Data `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn
```

> **Scaffold rule:** During scaffolding, always start fresh. Do not accumulate incremental migrations until the baseline is established and the project is in production.

## 5b — App Core (TDD)

Required:
- DTOs/mappers/services compile,
- API endpoint mappings compile,
- DI registrations resolve,
- all service unit tests and endpoint integration tests pass.

Exit criteria:
- [ ] Service unit tests pass (mock-based, via Moq)
- [ ] Endpoint integration tests pass (via `CustomApiFactory` + `WebApplicationFactory`)
- [ ] No-op service stubs replaced with real implementations in `RegisterServices.cs`
- [ ] API endpoint mappings added in `WebApplicationBuilderExtensions.cs`
- [ ] `DbSet<{Entity}>` exists in Trxn + Query DbContexts
- [ ] API host builds cleanly

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"
```

## 5c — Runtime / Edge (Tests-After)

Required:
- host startup path is healthy for enabled runtime concerns,
- Aspire wiring works when enabled,
- infrastructure tests written and passing (health checks, configuration loading, caching).

Runtime/Host checks (enabled features only):

### Aspire AppHost

Preflight (run before first launch — see [../skills/aspire.md](../skills/aspire.md) § Preflight):
- [ ] Docker running (`docker info` succeeds)
- [ ] No stale containers holding required ports (`docker ps`)
- [ ] `dotnet restore` on AppHost succeeds

Gate:
- [ ] `src/Aspire/AppHost/AppHost.csproj` uses `Aspire.AppHost.Sdk` MSBuild SDK
- [ ] Required Aspire CLI env vars are set before terminal `dotnet run`
- [ ] `dotnet build src/Aspire/AppHost` succeeds
- [ ] `dotnet run --project src/Aspire/AppHost` starts resources
- [ ] Dashboard reachable (URL from console output — do not reuse prior session URLs)
- [ ] All registered resources show healthy in dashboard before testing endpoints
- [ ] Data-plane spot check: at least one backing store (SQL tables exist, Redis reachable, seed rows present) verified directly — not just via dashboard liveness

### Gateway

- [ ] Gateway build succeeds
- [ ] Gateway can route to API via configured cluster/service discovery

Commands:

```powershell
dotnet build
dotnet test
dotnet run --project src/Aspire/AppHost
```

After Aspire verification, write infrastructure tests (health checks, config loading, caching) and re-run `dotnet test` to confirm.

## 5d — Optional Hosts (Tests-After)

Run only for enabled hosts.

Required:
- enabled optional hosts compile and start cleanly,
- host-specific integration steps complete,
- optional host dependencies are reachable.

> **Scaffold vs Complete:** Do NOT mark Phase 5d complete unless each enabled optional host has both a validated build AND its host-specific gate result recorded below. If a host only scaffolds successfully (e.g., solution builds but the host has not been started or its target-specific checks have not passed), record the status as "scaffolded" or "partially validated" — not "complete". The handoff must reflect per-host gate status, not just solution-level build success.

Function App:

- [ ] `local.settings.json` contains required runtime keys and trigger bindings
- [ ] Azurite/dev-tunnel/ngrok started if required by triggers

```powershell
func host start --verbose
```

Uno UI:

- [ ] `uno-check` validates workloads
- [ ] Gateway/OpenAPI endpoint reachable for client generation
- [ ] Kiota client generation completes (if used)
- [ ] UI runs on selected target (`net10.0-browserwasm` or `net10.0-desktop`)

```powershell
uno-check
dotnet build --project src/{Project}.UI/{Project}.UI.csproj -f net10.0-browserwasm
```

If targeting desktop instead of WASM, build the selected desktop target instead of `net10.0-browserwasm`.

If targeting Android (`net10.0-android`):
- [ ] Android SDK path resolved (see `skills/uno-ui.md` § Android SDK Discovery)
- [ ] `<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>` set if manual ADB sideloading is used
- [ ] Emulator host networking uses `10.0.2.2` for local backend calls (see `skills/uno-ui.md` § Emulator Host Networking)

> **Starter-library escape hatch:** If the repo currently contains only a `net10.0` starter library or shell-contract scaffold instead of a real Uno multi-target app, Phase 5d for Uno must be recorded as **incomplete**. `NETSDK1139` on `net10.0-browserwasm` is expected in that scenario and is evidence that Uno scaffolding is still missing — not an environment glitch. Do not debug/workaround it; record the status as "scaffolded — Uno multi-target not yet created" and move on.

Also verify:
- Gateway/OpenAPI endpoint is reachable for client generation
- Kiota client generation completes (if used)
- the selected Uno target runs successfully

Scheduler:

- [ ] Scheduler connection string configured
- [ ] Scheduler operational tables exist (verify schema ownership — see [troubleshooting.md](troubleshooting.md) § Third-Party Operational Store Schema Triage)

```powershell
dotnet run --project src/{Host}/{Host}.Scheduler
```

> **AppHost/config dependency:** When the scheduler depends on AppHost-provided resources (e.g., connection strings via service discovery), either run it through AppHost or provide equivalent local connection strings (e.g., `ConnectionStrings:LuminaDb`) before using direct `dotnet run`. Record which path was validated in the handoff.

## 5e — Quality Gates + Delivery

Unit, service, endpoint, and integration tests already exist from Phases 5a/5b/5c/5d. Phase 5e adds quality gate tests and runs a full regression.

**New tests in this phase:**
- Architecture tests (NetArchTest layering rules)
- Load tests (NBomber, if comprehensive profile)
- Benchmarks (BenchmarkDotNet, if comprehensive profile)
- E2E Playwright tests (if comprehensive profile + UI enabled)

**Also in this phase:**
- IaC (Bicep), CI/CD pipeline YAML, Dockerfile, coverage settings

Required profile gate (full regression):
- `minimal`: Unit + Endpoint
- `balanced`: Unit + Endpoint + Integration + Architecture
- `comprehensive`: Balanced + E2E/Load/Benchmark (when enabled)

Commands:

```powershell
dotnet test
```

IaC (if enabled):

```powershell
az bicep build --file infra/main.bicep
```

Delivery checks:
- [ ] Full test suite passes (regression — not first-time creation for unit/endpoint/integration)
- [ ] Architecture tests enforce layering rules
- [ ] `az bicep build --file infra/main.bicep` succeeds *(if IaC enabled)*
- [ ] Aspire <-> IaC names/connection strings are aligned

## 5f — Authentication Finalization

**Scaffold mode is the default.** Phase 5f is complete when the app builds, tests pass, and auth works with the config-driven scaffold principal. Live identity provider setup is supplemental hardening — it does **not** block scaffold completion.

Required (scaffold mode):
- `AuthMode` toggle present in config (`Scaffold` vs provider name)
- App boots and all endpoints are reachable with scaffold principal
- Auth stubs/no-op passthrough removed or gated behind `AuthMode` check
- Endpoint tests pass against the scaffold auth path

Required (live provider — only when intentionally provisioned):
- Auth provider configured with real tenant values
- Authenticated endpoint behavior verified against live tokens
- Scaffold stub gated by config so it does not activate in production

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Endpoint"
```

If live Entra setup is not yet performed, log it in `HANDOFF.md` as a deployment-only dependency and continue.

## 5g — AI Integration

**Scaffold mode is the default.** Phase 5g is complete when AI-backed interfaces compile, resolve from DI, and tests pass with stubs or no-op implementations. Live Foundry/AI Search endpoints are deployment-only dependencies and do not block scaffold completion.

Required (scaffold mode):
- AI service interfaces compile and resolve from DI
- Config sections absent → services register as no-op stubs (not throws/missing-registration)
- AI DI/configuration compiles

Required (live endpoints — only when provisioned):
- Search service responds
- Agent endpoint responds
- Integration tests pass against live resources

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Unit"
```

If live AI endpoints are not yet provisioned, log them in `HANDOFF.md` as deployment-only dependencies.

---

## Pre-Merge Gate

Must pass before merge:

```powershell
dotnet restore
dotnet build
dotnet test
```

If IaC is part of scope:

```powershell
az bicep build --file infra/main.bicep
```

---

## Failure Handling

- Code-generation failures: one focused AI fix pass, then re-run failing gate.
- Infra/environment failures: log in `HANDOFF.md`, classify blocker, continue non-blocked scope.
- Instruction gaps: append to `UPDATE-INSTRUCTIONS.md`.
- If a step fails, log the blocker in `HANDOFF.md` (see [HANDOFF.md template](HANDOFF.md)) and continue with non-blocked work.
- Pattern reference: [../support/sampleapp-patterns.md](../support/sampleapp-patterns.md) (pattern index → `patterns/` folder) for composition wiring.

---

## Mid-Session Rollback Protocol

When the AI discovers a fundamental assumption error (wrong entity design, incorrect relationship, misunderstood domain rule) after multiple files have been generated:

1. **Stop generating.** Do not continue building on a flawed assumption.
2. **Assess scope:** Identify all files generated since the last git checkpoint that are affected by the error.
3. **Rollback to checkpoint:**
   ```powershell
   git stash          # save current work
   git log --oneline -5  # find last clean checkpoint
   ```
4. **Decide recovery path:**
   - **Isolated error** (affects 1-2 files): `git stash pop`, fix the affected files, rebuild and re-test.
   - **Structural error** (wrong entity shape, missing relationship, bad inheritance): `git checkout <last-checkpoint>` to discard, then correct the domain/resource YAML before re-scaffolding.
   - **Domain misunderstanding** (entity purpose is wrong): go back to Phase 1 output, clarify with the user, update `domain-specification.md`, and re-scaffold the slice from scratch.
5. **Document in HANDOFF.md:** Record what was rolled back and why, so the next session doesn't repeat the mistake.
6. **Re-enter at the corrected sub-phase** using the standard phase loading manifest.

> **Rule:** Never patch-fix a structural error across 3+ files. It is faster and safer to rollback and re-scaffold than to chase cascading fixes.

---

## Post-Scaffold Smoke Test

Run after all Phase 5 sub-phases complete (before the Pre-Merge Gate) to validate the scaffold works end-to-end:

### 1. Build & Test
```powershell
dotnet restore
dotnet build
dotnet test
```

### 2. Host Startup
```powershell
# API host (required)
dotnet run --project src/{Host}/{Host}.Api -- --urls "http://localhost:5100"
# Verify: GET http://localhost:5100/health → 200 OK (Ctrl+C after)

# Aspire (if enabled)
dotnet run --project src/Aspire/AppHost
# Verify: Aspire dashboard loads, all resources show healthy

# Scheduler (if enabled)
dotnet run --project src/{Host}/{Host}.Scheduler

# Function App (if enabled)
func host start --port 7100
```

### 3. API Endpoint Smoke
For each scaffolded entity, verify the CRUD cycle works:
```
POST /v1/tenant/{tenantId}/{entity-route}       → 201 + Location header
GET  /v1/tenant/{tenantId}/{entity-route}/{id}   → 200 + entity body
POST /v1/tenant/{tenantId}/{entity-route}/search → 200 + paged results
PUT  /v1/tenant/{tenantId}/{entity-route}/{id}   → 200 + updated body
DEL  /v1/tenant/{tenantId}/{entity-route}/{id}   → 204
```
Use `http` (HTTPie), `curl`, or the Scalar UI at `/scalar/v1`.

### 4. Checklist
- [ ] All hosts start without errors
- [ ] Health endpoint returns 200
- [ ] At least one entity CRUD cycle completes successfully
- [ ] OpenAPI/Scalar UI loads at `/scalar/v1`
- [ ] No unresolved `// TODO: [CONFIGURE]` stubs remain in production paths (stubs in auth/external-API are expected until Phase 5f)
- [ ] Aspire dashboard shows all registered resources (if enabled)
- [ ] No compiler warnings in generated code (treat as errors)
