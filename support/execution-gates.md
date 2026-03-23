# Execution Gates (Canonical)

Single source of truth for compile/test/run checkpoints across implementation phases.

Use this file for:
- phase-by-phase validation commands,
- exit criteria,
- pre-merge quality gates.

If another file disagrees, this file wins.

---

## Core Loop (Run After Each Scaffolding Sub-Phase)

Run from solution root:

```powershell
dotnet restore
dotnet build
dotnet test --filter "TestCategory=Unit"
```

Gate passes when all three commands succeed.

---

## Phase Gates

## 4a — Foundation

Required:
- solution structure and references compile,
- domain + data-access projects build,
- DbContext + repository wiring is present.

Commands:

```powershell
dotnet build
```

Optional migration readiness check:

```powershell
dotnet ef migrations add InitialCreate `
  --project src/Infrastructure/{Project}.Infrastructure.Data `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn
```

## 4b — App Core

Required:
- DTOs/mappers/services compile,
- API endpoint mappings compile,
- DI registrations resolve.

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Endpoint"
```

## 4c — Runtime / Edge

Required:
- host startup path is healthy for enabled runtime concerns,
- Aspire wiring works when enabled.

Commands:

```powershell
dotnet build
dotnet run --project src/Aspire/AppHost
```

## 4d — Optional Hosts

Run only for enabled hosts.

Required:
- enabled optional hosts compile and start cleanly,
- host-specific integration steps complete,
- optional host dependencies are reachable.

Function App:

```powershell
func host start --verbose
```

Uno UI:

```powershell
uno-check
dotnet build --project src/{Project}.UI/{Project}.UI.csproj -f net10.0-browserwasm
```

If targeting desktop instead of WASM, build the selected desktop target instead of `net10.0-browserwasm`.

Also verify:
- Gateway/OpenAPI endpoint is reachable for client generation
- Kiota client generation completes (if used)
- the selected Uno target runs successfully

Scheduler:

```powershell
dotnet run --project src/{Host}/{Host}.Scheduler
```

## 4e — Quality + Delivery

Required profile gate:
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

## 4f — Authentication Finalization

**Scaffold mode is the default.** Phase 4f is complete when the app builds, tests pass, and auth works with the config-driven scaffold principal. Live identity provider setup is supplemental hardening — it does **not** block scaffold completion.

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

## 4g — AI Integration

**Scaffold mode is the default.** Phase 4g is complete when AI-backed interfaces compile, resolve from DI, and tests pass with stubs or no-op implementations. Live Foundry/AI Search endpoints are deployment-only dependencies and do not block scaffold completion.

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

Run after all Phase 4 sub-phases complete (before the Pre-Merge Gate) to validate the scaffold works end-to-end:

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
- [ ] No unresolved `// TODO: [CONFIGURE]` stubs remain in production paths (stubs in auth/external-API are expected until Phase 4f)
- [ ] Aspire dashboard shows all registered resources (if enabled)
- [ ] No compiler warnings in generated code (treat as errors)
