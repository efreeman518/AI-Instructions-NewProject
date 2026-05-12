# Final Scaffold Checklist

Load this file after the final enabled Phase 5 sub-phase. It converts "the scaffold seems done" into objective checks.

This is not production-readiness. It verifies that the generated app is a consistent, runnable scaffold.

---

## Required Commands

Run from the generated app root:

```powershell
dotnet restore
dotnet build
dotnet test
```

All three must exit 0. Then walk the **Completion Criteria** below.

If IaC is enabled:

```powershell
az bicep build --file infra/main.bicep
```

If Aspire is enabled:

```powershell
dotnet run --project src/Host/Aspire/AppHost
```

Verify the Aspire dashboard shows all enabled resources healthy before exercising endpoints.

**Live smoke deferral.** The live AppHost boot may be skipped at the final-checklist stage when **all** of the following are recorded in the most recent sub-phase `HANDOFF.md`:

- The sub-phase did not modify `Host/Aspire/AppHost` or the Aspire resource graph (record this fact explicitly, e.g., "AppHost / resource graph not modified this sub-phase").
- A prior sub-phase recorded a green Aspire live boot in `HANDOFF.md` § Validation since the last AppHost change.
- This session's `TestCategory=Unit`, `TestCategory=Endpoint`, and (where applicable) `TestCategory=Integration` runs were all green.

If any condition is missing, run the live boot. When deferred, copy the prior green boot's discovery evidence into this session's `HANDOFF.md` § Validation and mark the row `deferred — see <prior-sub-phase>`.

---

## API Smoke

Run against the API host or gateway, depending on scaffold mode.

For at least one tenant and one entity:

```text
POST   /v1/tenant/{tenantId}/{entity-route}        -> 201 or 200
GET    /v1/tenant/{tenantId}/{entity-route}/{id}   -> 200
POST   /v1/tenant/{tenantId}/{entity-route}/search -> 200
PUT    /v1/tenant/{tenantId}/{entity-route}/{id}   -> 200
DELETE /v1/tenant/{tenantId}/{entity-route}/{id}   -> 204 or 200
GET    /health                                     -> 200
GET    /scalar/v1                                  -> 200
```

Use `curl`, HTTPie, REST Client, or Scalar. Record status codes and endpoint discovery method in `HANDOFF.md`; do not record ephemeral localhost URLs or Aspire-assigned ports.

---

## Completion Criteria

- [ ] `dotnet restore`, `dotnet build`, and `dotnet test` pass. The full `dotnet test` (no filter) is green — every category the scaffold produces is either passing or `Assert.Inconclusive` / `[Ignore]` with a recorded reason. No test assembly aborts in `[AssemblyInitialize]`.
- [ ] Every test that is `[Ignore]`'d or marked `Assert.Inconclusive` for a deferred external dep is named in `HANDOFF.md` § Scaffold Acceptance with the unblocking step.
- [ ] All `EF.*` packages resolve from the configured private feed (`dotnet restore` succeeded with `NUGET_AUTH_TOKEN` set).
- [ ] `UBIQUITOUS-LANGUAGE.md` and `DESIGN-DECISIONS.md` still match the generated entity, service, and endpoint names.
- [ ] Generated solution shape matches `skills/solution-structure.md` (no missing project, no orphan no-op stub).
- [ ] `HANDOFF.md` resume state is current: `currentPhase`, `currentSubPhase`, gate result, blockers, next load set.
- [ ] `implementation-plan.md` open questions resolved or explicitly deferred with TODO.
- [ ] Every enabled host has a recorded status in `HANDOFF.md`: `validated`, `partially-validated`, or `blocked` with reason.
- [ ] At least one entity CRUD/search smoke cycle succeeds.
- [ ] Health endpoint returns 200.
- [ ] OpenAPI/Scalar loads.
- [ ] **Aspire AppHost clean startup:** `dotnet run --project src/Host/Aspire/AppHost` reaches the dashboard with every registered resource in **Running** state, no exceptions in resource logs, and `/healthz` returning 200 on every host. Stub-mode external deps (`emulator`, `lazy-optional`, `no-op stub`, `deployment-only`) count as healthy when their stub/emulator path responds.
- [ ] **Every UI host starts cleanly — Aspire-registered AND standalone:**
  - Blazor (when enabled): standalone `dotnet run` reaches `Application started` + root URL renders; when added to AppHost, the resource reaches Running and a Refit call returns data (or typed empty state).
  - Uno (when enabled): the selected platform target (`<tfm>-browserwasm` / `<tfm>-desktop` / `<tfm>-android`) launches and renders the shell; when added to AppHost, the Uno resource reaches Running.
  - Backend connectivity from UI: at least one entity list page loads against the Gateway/API without console exceptions (empty or seeded data — both valid).
- [ ] No generated source file outside the **scaffold-skipped surface** contains `throw new NotImplementedException`. The skipped surface is limited to: (a) `NoOp*` fallback stubs in `Infrastructure.Stubs/` (or equivalent) registered via `TryAddSingleton`/`TryAddScoped` for entities the scaffold contracts but does not activate, and (b) override methods on `<packagePrefix>.*` repository/storage base types that are only reachable through those `NoOp*` stubs. Per [../ai/contract-scaffolding.md](../ai/contract-scaffolding.md), even these stubs should prefer safe defaults (`Result.Success`, empty collections, completed `Task`) — throwing is permitted only when no safe default exists for the return shape.
- [ ] No scaffold placeholders remain in source/config.
- [ ] No `<packagePrefix>.*` shared base type is reimplemented in application/domain/host layers — they live in feed packages or `src/Packages/<packagePrefix>.*` projects only, per `packageStrategy`.
- [ ] **One public type per file** across all generated `.cs` files in `src/` (including `src/Packages/<Prefix>.*`). File name matches the type. Lumped files (multiple top-level public/internal types) are a failure unless they fall under the exception list in [../skills/solution-structure.md](../skills/solution-structure.md) § Non-Negotiables.
- [ ] Deployment-only dependencies are recorded as non-blocking residuals.

---

## If A Check Fails

- Build/test failure: one focused fix pass, rerun the exact failing command.
- Feed failure: fix `nuget.config`, `Directory.Packages.props`, or project package references before changing code.
- Structure failure: generate the missing scaffold artifact instead of loosening the validator.
- Language failure: update `UBIQUITOUS-LANGUAGE.md` or `DESIGN-DECISIONS.md` to match the accepted domain model before changing code names.
- Host/runtime failure: record blocker in `HANDOFF.md`, continue only if the failed host is optional or dependency-only.
- Instruction gap: append to root `INSTRUCTION-GAPS.md` in the generated app.
