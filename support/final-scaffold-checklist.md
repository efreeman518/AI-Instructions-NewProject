# Final Scaffold Checklist

Load this file after the final enabled Phase 5 sub-phase. It converts "the scaffold seems done" into objective checks.

This is not production-readiness. It verifies that the generated app is a consistent, runnable scaffold.

---

## Required Commands

Run from the generated app root:

```powershell
python .instructions/scripts/run-final-scaffold-check.py --root . --require-auth-env
```

Equivalent expanded commands:

```powershell
dotnet restore
dotnet build
dotnet test
python .instructions/scripts/validate-ef-packages-feed.py --root . --require-auth-env
python .instructions/scripts/validate-scaffold-output.py --root . --phase final
python .instructions/scripts/validate-handoff.py --root .
python .instructions/scripts/validate-implementation-plan.py --root .
```

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
- [ ] `validate-ef-packages-feed.py` passes.
- [ ] `validate-scaffold-output.py --phase final` passes.
- [ ] `validate-handoff.py` passes.
- [ ] `validate-implementation-plan.py` passes.
- [ ] Every enabled host has a recorded status in `HANDOFF.md`: validated, partially validated, or blocked with reason.
- [ ] At least one entity CRUD/search smoke cycle succeeds.
- [ ] Health endpoint returns 200.
- [ ] OpenAPI/Scalar loads.
- [ ] Aspire dashboard resources healthy when Aspire is enabled.
- [ ] No generated source file contains `throw new NotImplementedException`.
- [ ] No scaffold placeholders remain in source/config.
- [ ] No EF.Packages shared type is reimplemented locally.
- [ ] Deployment-only dependencies are recorded as non-blocking residuals.

---

## If A Check Fails

- Build/test failure: one focused fix pass, rerun the exact failing command.
- Feed failure: fix `nuget.config`, `Directory.Packages.props`, or project package references before changing code.
- Structure failure: generate the missing scaffold artifact instead of loosening the validator.
- Host/runtime failure: record blocker in `HANDOFF.md`, continue only if the failed host is optional or dependency-only.
- Instruction gap: append to root `INSTRUCTION-GAPS.md` in the generated app.
