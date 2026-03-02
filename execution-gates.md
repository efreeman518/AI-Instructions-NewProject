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

Function App:

```powershell
func host start --verbose
```

Uno UI:

```powershell
uno-check
```

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

Required:
- auth provider configured,
- authenticated endpoint behavior verified,
- auth stubs removed or gated for dev-only.

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Endpoint"
```

## 4g — AI Integration

Required:
- search service responds,
- agent endpoint responds,
- AI DI/configuration compiles.

Commands:

```powershell
dotnet build
dotnet test --filter "TestCategory=Integration"
```

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
