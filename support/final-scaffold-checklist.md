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

- [ ] `dotnet restore`, `dotnet build`, and `dotnet test` pass.
- [ ] All `EF.*` packages resolve from the configured private feed (`dotnet restore` succeeded with `NUGET_AUTH_TOKEN` set).
- [ ] `UBIQUITOUS-LANGUAGE.md` and `DESIGN-DECISIONS.md` still match the generated entity, service, and endpoint names.
- [ ] Generated solution shape matches `skills/solution-structure.md` (no missing project, no orphan no-op stub).
- [ ] `HANDOFF.md` resume state is current: `currentPhase`, `currentSubPhase`, gate result, blockers, next load set.
- [ ] `implementation-plan.md` open questions resolved or explicitly deferred with TODO.
- [ ] Every enabled host has a recorded status in `HANDOFF.md`: `validated`, `partially-validated`, or `blocked` with reason.
- [ ] At least one entity CRUD/search smoke cycle succeeds.
- [ ] Health endpoint returns 200.
- [ ] OpenAPI/Scalar loads.
- [ ] Aspire dashboard resources healthy when Aspire is enabled.
- [ ] No generated source file contains `throw new NotImplementedException`.
- [ ] No scaffold placeholders remain in source/config.
- [ ] No `<packagePrefix>.*` shared base type is reimplemented in application/domain/host layers — they live in feed packages or `src/Packages/<packagePrefix>.*` projects only, per `packageStrategy`.
- [ ] Deployment-only dependencies are recorded as non-blocking residuals.

---

## If A Check Fails

- Build/test failure: one focused fix pass, rerun the exact failing command.
- Feed failure: fix `nuget.config`, `Directory.Packages.props`, or project package references before changing code.
- Structure failure: generate the missing scaffold artifact instead of loosening the validator.
- Language failure: update `UBIQUITOUS-LANGUAGE.md` or `DESIGN-DECISIONS.md` to match the accepted domain model before changing code names.
- Host/runtime failure: record blocker in `HANDOFF.md`, continue only if the failed host is optional or dependency-only.
- Instruction gap: append to root `INSTRUCTION-GAPS.md` in the generated app.
