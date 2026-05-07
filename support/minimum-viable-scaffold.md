# Minimum Viable Scaffold (MVS)

The shortest path from "empty repo" to "passing API with one entity" using this instruction set. Use this when you want to ship something working before deciding on optional hosts, UI, gateway, AI, or messaging.

## What MVS produces

- A single .NET API host with one entity (CRUD + search), one DbContext pair (Trxn + Query), repositories, mapper, validator, service, endpoints, and unit + endpoint tests.
- `scaffoldMode: api-only`. No Gateway, no Uno/Blazor UI, no Function App, no Scheduler, no AI services, no messaging.
- Every external dependency declared `lazy-optional` so the app boots locally without cloud setup.
- Auth runs in scaffold mode (config-driven principal). Live identity provider is deferred.

You should reach a green `dotnet build` + `dotnet test` and a working `GET /health` in under a day of focused work. Everything beyond that is incremental.

## When to use MVS vs the full workflow

| Situation | Use |
|---|---|
| First time using this scaffold; want to feel the loop end-to-end | MVS |
| Internal tool, prototype, or proof-of-concept | MVS |
| Production app with known scope (gateway, UI, scheduler, etc.) | Full [phase router](../START-AI.md) |
| Adding to an already-scaffolded app | [vertical-slice-checklist.md](vertical-slice-checklist.md) |

MVS is a profile, not a separate path. After MVS finishes you can promote to a richer profile by editing `resource-implementation.yaml` and running additional Phase 5 sub-phases.

## Prerequisites

Same as the [README Prerequisites](../README.md#prerequisites), with these MVS-specific simplifications:

- **Skip:** Uno templates, `uno-check`, Kiota, Functions Core Tools.
- **Required:** `.NET SDK`, `git`, Python (for installer), Docker (only if you keep Aspire enabled — MVS keeps it off by default), GitHub Packages read for EF.Packages feed.

## Install

```powershell
# from a clone of this instruction repo
py -3 scripts/install-to-project.py --target C:\path\to\your-app --verify
```

The `--verify` flag confirms all entrypoints landed correctly. See [install-to-project.py](../scripts/install-to-project.py) for the full smoke-check list.

## The MVS prompts

Six AI sessions: one per phase for Phases 1–4, plus separate sessions for Phase 5a and Phase 5b (Phase 5 always runs one sub-phase per session). MVS skips 5c, 5d, and 5e — see § Why this works.

Paste each prompt verbatim, fill in the `{...}` placeholders, let the session run to its gate, then close the session. The next session starts fresh from `START-AI.md` + `HANDOFF.md`.

### Phase 1 — Domain Discovery (one entity)

```text
Load .instructions/START-AI.md. This is a new project — no HANDOFF.md yet.
Mode: minimum-viable-scaffold (API-only, one entity, no optional hosts).
Generate domain artifacts for {ProjectName}.
Business: {one-sentence business description}.
Single entity for now: {EntityName} with fields: {field1: type, field2: type, ...}.
Run shared-understanding-interview.md but skip branches that don't apply to a single-entity API
(messaging, scheduling, multi-host, multi-tenant unless explicitly required).
Produce domain-specification.yaml, UBIQUITOUS-LANGUAGE.md, DESIGN-DECISIONS.md.
Write HANDOFF.md and close.
```

**Done when:** `domain-specification.yaml` defines exactly one entity. `DESIGN-DECISIONS.md` records that this is an MVS profile.

### Phase 2 — Resource Definition (api-only)

```text
Load .instructions/START-AI.md and HANDOFF.md.
Generate resource-implementation.yaml per ai/resource-implementation-schema.md.
Set scaffoldMode: api-only. Set testingProfile: minimal.
Disable: gateway, uno-ui, blazor-ui, function-app, scheduler, aspire, multi-tenant, ai-services, messaging.
Set every external dependency mode to lazy-optional.
Update HANDOFF.md and close.
```

**Done when:** `resource-implementation.yaml` has `scaffoldMode: api-only` and every optional flag is `false`. No external dep is in `deployment-only` or `emulator` mode.

### Phase 3 — Implementation Plan

```text
Load .instructions/START-AI.md and HANDOFF.md.
Generate implementation-plan.md per ai/implementation-plan.md.
Pre-flight: configure EF.Packages feed via scripts/configure-ef-packages-feed.py (if private packages are used)
and confirm `dotnet restore` exits 0. Confirm `dotnet ef` is installed.
Tooling section: list only CLIs needed for an api-only scaffold (dotnet, dotnet-ef). Skip MCP discovery beyond Microsoft Docs + Context7.
Update HANDOFF.md and close.
```

**Done when:** `dotnet restore` exits 0. The plan's Tooling section has no unresolved items.

### Phase 4 — Contract Scaffolding

```text
Load .instructions/START-AI.md and HANDOFF.md.
Load the Phase 4 file set from START-AI.md § Phase Router.
Generate the api-only contract scaffold per ai/contract-scaffolding.md:
solution structure (.slnx + Directory.Packages.props), API host project, Application/Domain/Infrastructure projects,
test projects (Test.Support, Test.Unit, Test.Endpoints), interfaces, DTOs, entity shells, no-op DI stubs.
Skip projects for Gateway, Aspire AppHost, Function App, Uno/Blazor UI, Scheduler.
Gate: `dotnet build` succeeds on the full solution including test projects.
Set contractsScaffolded: true in HANDOFF.md and close.
```

**Done when:** `dotnet build` is green and the solution contains exactly: API host, Application/Domain/Infrastructure projects, and the three test projects. No optional hosts.

### Phase 5 — Implementation (5a + 5b only for MVS)

MVS skips 5c (no optional hosts), 5d (no architecture/load/benchmark gates beyond `minimal` profile), and 5e (auth stays in scaffold mode; no AI services).

#### 5a — Foundation (TDD)

```text
Load .instructions/START-AI.md and HANDOFF.md.
Read the Phase 5 file table in ai/SKILL.md (5a row only) and load those files.
Follow ai/tdd-protocol.md: write domain/rule/repository tests first (red), implement to green.
Activate {Entity}Builder.Build() after entity logic is implemented.
Replace no-op repository stubs with real implementations in RegisterServices.cs.
Gate: see support/execution-gates.md § 5a.
Update HANDOFF.md (currentSubPhase: 5b) and close.
```

#### 5b — App Core + API (TDD)

```text
Load .instructions/START-AI.md and HANDOFF.md.
Load the 5b row from the Phase 5 file table — but only the api-only required entries.
Skip runtime concerns: gateway, multi-tenant, caching, aspire, observability, security extensions.
Follow ai/tdd-protocol.md: service tests → implement service, endpoint tests → implement endpoints.
Replace no-op DI stubs with real implementations.
Gate: see support/execution-gates.md § 5b (skip the Aspire portion — Aspire is disabled in MVS).
Update HANDOFF.md and close.
```

**Done when:** `dotnet build` green, `dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"` green, the API host starts and `GET /health` returns 200.

## "You are done" check

Run from the solution root:

```powershell
dotnet build
dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"
dotnet run --project src\Host\{Host}.Api -- --urls "http://localhost:5100"
# in another shell, or via your HTTP client:
# GET http://localhost:5100/health → 200 OK
# POST http://localhost:5100/v1/{entity-route} → 201 + Location header
```

If all three pass, MVS is complete.

## Promoting beyond MVS

When you're ready to add scope:

- **New entity:** [vertical-slice-checklist.md](vertical-slice-checklist.md) fast-path.
- **Add a runtime concern** (caching, gateway, multi-tenant, observability, security): edit `resource-implementation.yaml` to enable the flag, run a 5b session loading only the new skill file.
- **Add an optional host** (Function App, Scheduler, Uno UI, Blazor UI): enable the flag in `resource-implementation.yaml`, run a dedicated 5c session per host.
- **Live auth or AI services:** enable in `resource-implementation.yaml`, run a 5e session.
- **Quality gates** (architecture tests, load, benchmarks): bump `testingProfile` and run a 5d session.

## Why this works

MVS is the same instruction set, scoped down. Every gate, every artifact, every conflict-precedence rule still applies — the only difference is that `resource-implementation.yaml` flags most optional surfaces off, so the per-sub-phase load sets shrink, and 5c/5d/5e can be deferred until the API is real.

If you find yourself wanting Gateway or UI or messaging mid-MVS, stop the MVS path and switch to the full [phase router](../START-AI.md). Mixing them produces incomplete artifacts.
