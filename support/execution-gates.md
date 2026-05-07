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

The scaffold uses private EF.Packages NuGet packages. Every local developer environment needs package read access before Phase 4 restore/build can pass.

**Step 1:** Ask the user to set or confirm:
- Feed URL (e.g., `https://nuget.pkg.github.com/{owner}/index.json`)
- Auth method: `NUGET_AUTH_TOKEN` environment variable (recommended) or credential provider

**Step 2:** Generate `nuget.config` with the feed helper:

```powershell
python .instructions/scripts/configure-ef-packages-feed.py --root . --feed-url https://nuget.pkg.github.com/{owner}/index.json --username {username}
```

The helper writes `%NUGET_AUTH_TOKEN%` only; it must never write the PAT value.

Manual equivalent:

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
$env:NUGET_AUTH_TOKEN = "<package-read-token>"

# Or persist in user profile (PowerShell $PROFILE)
[Environment]::SetEnvironmentVariable("NUGET_AUTH_TOKEN", "<package-read-token>", "User")
```

> **Security:** Never commit PATs to source control or ask the user to paste one into chat. Use `%VARIABLE%` syntax in `nuget.config` (Windows) or `$VARIABLE` (Linux/Mac) for credential interpolation. Add `.env` and `nuget.config.local` to `.gitignore`.

**Step 4:** Verify:

```powershell
dotnet restore
```

Gate: `dotnet restore` exits 0. All EF.Packages resolve successfully, `EF.*` maps to the private feed, and `dotnet-ef` maps to `nuget.org` when package source mapping is enabled.

### AI Assistant — MCP Servers

Configure these in your AI client (VS Code `settings.json` or Claude Desktop config) so the AI can look up current docs and interact with tools during scaffolding.

- [ ] MCP servers configured per [../README.md](../README.md) (Essential + phase-relevant)

### Tooling Verification (Phase 3 Gate)

Phase 3 must populate the **Tooling & Environment Readiness** section of `implementation-plan.md`. Before closing Phase 3:

- [ ] All CLIs required by resource YAML technology choices are identified with install commands
- [ ] MCP server discovery completed (npm search, MCP registry) for project-specific libraries
- [ ] CLI preference applied: CLIs chosen over MCP servers where both exist (lower token cost)
- [ ] Each CLI entry has a verified checkbox or an install command the operator can run before Phase 4
- [ ] `dotnet restore` exits 0 with `NUGET_AUTH_TOKEN` set
- [ ] Developer reviews `UBIQUITOUS-LANGUAGE.md` and `DESIGN-DECISIONS.md` for completeness against `domain-specification.yaml`
- [ ] Developer reviews `implementation-plan.md` against `ai/implementation-plan.md` schema

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
- test projects compile (Test.Support, Test.Unit, Test.Integration, Test.Endpoints, Test.E2E, profile-specific projects: Test.Architecture, Test.PlaywrightUI, Test.Load, Test.Benchmarks).

Exit criteria:
- [ ] Solution structure matches `skills/solution-structure.md`
- [ ] Every entity from `resource-implementation.yaml` has: interface, DTO, entity shell, builders
- [ ] All no-op stubs satisfy their interfaces
- [ ] `RegisterServices.cs` wires all no-op stubs
- [ ] Test.Support contains `UnitTestBase`, `InMemoryDbBuilder`, `DbSupport`, `Utility`, `TestConstants`
- [ ] `{Entity}DtoBuilder` returns valid DTOs
- [ ] No domain logic in entity shells (only `throw new NotImplementedException`)
- [ ] EF.Packages shared types are consumed from packages, not reimplemented locally
- [ ] Phase 4 scaffold structure validator passes

Commands:

```powershell
dotnet restore
dotnet build
```

No `dotnet test` required — test projects are empty or contain no test methods. Developer confirms the solution structure, no-op stubs, and EF.Packages references match the Phase 4 exit criteria above.

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
  --startup-project src/Host/{Host}.Api

# Create a clean baseline
dotnet ef migrations add InitialCreate `
  --project src/Infrastructure/{Project}.Infrastructure.Data `
  --startup-project src/Host/{Host}.Api `
  --context {App}DbContextTrxn
```

> **Scaffold rule:** During scaffolding, always start fresh. Do not accumulate incremental migrations until the baseline is established and the project is in production.

## 5b — App Core + Runtime/Edge (TDD for app/API, tests-after for runtime)

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

### Runtime / Edge concerns (within 5b, tests-after)

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
- [ ] `src/Host/Aspire/AppHost/AppHost.csproj` uses `Aspire.AppHost.Sdk` MSBuild SDK
- [ ] Required Aspire CLI env vars are set before terminal `dotnet run`
- [ ] `dotnet build src/Host/Aspire/AppHost` succeeds
- [ ] `dotnet run --project src/Host/Aspire/AppHost` starts resources
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
dotnet run --project src/Host/Aspire/AppHost
```

After Aspire verification, write infrastructure tests (health checks, config loading, caching) and re-run `dotnet test` to confirm.

## 5c — Optional Hosts (Tests-After)

Run only for enabled hosts.

Required:
- enabled optional hosts compile and start cleanly,
- host-specific integration steps complete,
- optional host dependencies are reachable.

> **Scaffold vs Complete:** Do NOT mark Phase 5c complete unless each enabled optional host has both a validated build AND its host-specific gate result recorded below. If a host only scaffolds successfully (e.g., solution builds but the host has not been started or its target-specific checks have not passed), record the status as `scaffolded` or `partially-validated`, not `validated`. The handoff must reflect per-host gate status, not just solution-level build success.

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
dotnet build --project src/UI/{Project}.Uno/{Project}.Uno.csproj -f net10.0-browserwasm
```

If targeting desktop instead of WASM, build the selected desktop target instead of `net10.0-browserwasm`.

If targeting Android (`net10.0-android`):
- [ ] Android SDK path resolved (see `skills/ui-uno.md` § Android SDK Discovery)
- [ ] `<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>` set if manual ADB sideloading is used
- [ ] Emulator host networking uses `10.0.2.2` for local backend calls (see `skills/ui-uno.md` § Emulator Host Networking)

> **Starter-library escape hatch:** If the repo currently contains only a `net10.0` starter library or shell-contract scaffold instead of a real Uno multi-target app, Phase 5c for Uno must be recorded as **blocked**. `NETSDK1139` on `net10.0-browserwasm` is expected in that scenario and is evidence that Uno scaffolding is still missing — not an environment glitch. Do not debug/workaround it; record the status as `blocked — Uno multi-target not yet created` and move on.

Also verify:
- Gateway/OpenAPI endpoint is reachable for client generation
- Kiota client generation completes (if used)
- the selected Uno target runs successfully

Scheduler:

- [ ] Scheduler connection string configured
- [ ] Scheduler operational tables exist (verify schema ownership — see [troubleshooting.md](troubleshooting.md) § Third-Party Operational Store Schema Triage)

```powershell
dotnet run --project src/Host/{Host}.Scheduler
```

> **AppHost/config dependency:** When the scheduler depends on AppHost-provided resources (e.g., connection strings via service discovery), either run it through AppHost or provide equivalent local connection strings (e.g., `ConnectionStrings:LuminaDb`) before using direct `dotnet run`. Record which path was validated in the handoff.

## 5d — Quality Gates + Delivery

Unit, service, endpoint, and integration tests already exist from Phases 5a/5b/5c. Phase 5d adds quality gate tests and runs a full regression.

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

## 5e — Integration (Auth + AI)

### Authentication Finalization (within 5e)

**Scaffold mode is the default.** Authentication finalization is complete when the app builds, tests pass, and auth works with the config-driven scaffold principal. Live identity provider setup is supplemental hardening — it does **not** block scaffold completion.

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

### AI Integration (within 5e, when `includeAiServices: true`)

**Scaffold mode is the default.** AI integration is complete when AI-backed interfaces compile, resolve from DI, and tests pass with stubs or no-op implementations. Live Foundry/AI Search endpoints are deployment-only dependencies and do not block scaffold completion.

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

## Compiler-Warning Policy

`dotnet build` exits 0 is the gate. New compiler/analyzer warnings introduced by generated code are either resolved or recorded in `INSTRUCTION-GAPS.md` with owner and rationale. `TreatWarningsAsErrors` is **off by default**; teams may opt in via `Directory.Build.props` once the codebase is warning-clean. Warnings from referenced packages or generated SDK code (EF migrations, source generators) do not block the gate.

**What blocks the gate:**
- `dotnet build` returns nonzero exit code.
- A warning is suppressed without an entry in `INSTRUCTION-GAPS.md`.

**What does NOT block the gate:**
- Warnings from third-party packages.
- SDK-generated code warnings (EF migrations, source generators).
- Documented warnings with an owner and target resolution date.

---

## Vulnerability Audit

Run after `dotnet restore`:

```powershell
dotnet list package --vulnerable --include-transitive
```

Severity policy:
- **High:** must be fixed (upgrade direct dependency or pin transitive) **or** recorded in `INSTRUCTION-GAPS.md` as a blocked deployment dependency with owner and target resolution date.
- **Moderate:** logged in `INSTRUCTION-GAPS.md` only; tracked but does not block.
- **Low:** team discretion.

The audit is mandatory before pre-merge gate and as part of the Phase 5d quality regression. CI workflows must include the audit step (see [../skills/cicd.md](../skills/cicd.md)).

---

## Pre-Merge Gate

Must pass before merge:

```powershell
dotnet restore
dotnet build
dotnet test
```

Plus a manual walk-through of [final-scaffold-checklist.md](final-scaffold-checklist.md) covering: solution structure shape, no-op stub coverage, host startup smoke checks, and HANDOFF.md completeness.

If IaC is part of scope:

```powershell
az bicep build --file infra/main.bicep
```

---

## Failure Handling

- Code-generation failures: one focused AI fix pass, then re-run failing gate.
- Infra/environment failures: log in `HANDOFF.md`, classify blocker, continue non-blocked scope.
- Instruction gaps: in a consumer app, append to root `INSTRUCTION-GAPS.md`; in this instruction repository, append to `support/UPDATE-INSTRUCTIONS.md`.
- If a step fails, log the blocker in `HANDOFF.md` (see [HANDOFF.md template](HANDOFF.md)) and continue with non-blocked work.
- Pattern reference: [../support/pattern-dispatcher.md](../support/pattern-dispatcher.md) (pattern index → `patterns/` folder) for composition wiring.

---

## Mid-Session Rollback Protocol

See [OPERATIONS.md](OPERATIONS.md) § Mid-Session Rollback Protocol.

---

## Post-Scaffold Smoke Test

Run after all Phase 5 sub-phases complete (before the Pre-Merge Gate) to validate the scaffold works end-to-end:

Load [final-scaffold-checklist.md](final-scaffold-checklist.md) for the canonical final acceptance checklist.

### 1. Build & Test
```powershell
dotnet restore
dotnet build
dotnet test
```

### 2. Host Startup
```powershell
# API host (required)
dotnet run --project src/Host/{Host}.Api -- --urls "http://localhost:5100"
# Verify: GET http://localhost:5100/health → 200 OK (Ctrl+C after)

# Aspire (if enabled)
dotnet run --project src/Host/Aspire/AppHost
# Verify: Aspire dashboard loads, all resources show healthy

# Scheduler (if enabled)
dotnet run --project src/Host/{Host}.Scheduler

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
- [ ] No unresolved `// TODO: [CONFIGURE]` stubs remain in production paths (stubs in auth/external-API are expected until Phase 5e)
- [ ] Aspire dashboard shows all registered resources (if enabled)
- [ ] Compiler-warning policy applied (see § Compiler-Warning Policy below)
- [ ] Vulnerability audit run (see § Vulnerability Audit below)
