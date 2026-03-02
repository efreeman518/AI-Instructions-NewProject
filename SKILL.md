# New Business Project — C#/.NET Scaffolding Skill

## Purpose

Use this skill set to scaffold new C#/.NET business applications with clean architecture, optional Gateway/Functions/Scheduler/Uno UI, and Azure-ready deployment patterns.

## When to Use

Use for:
- New solution scaffolding
- Adding full vertical slices (entity → data → app → API → tests)
- Adding optional workloads (Gateway, Functions, Scheduler, Uno UI)
- Preparing config/auth/infra/IaC/CI-CD foundations

## Non-Negotiables

- `sample-app/` is **read-only reference**; never edit/build/delete files there.
- Load [sampleapp-patterns.md](sampleapp-patterns.md) only when selecting cross-project patterns or before opening raw `sample-app/src/` files. Do not preload it for routine scaffolding.
- Generate code only in the user's new project directory.
- Use `.slnx` (not legacy `.sln`).
- Use central package management (`Directory.Packages.props`).
- After adding packages, update to latest stable and verify restore/build.
- Record instruction gaps in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md) (do not hot-edit baseline instructions mid-scaffold).
- Prefer latest stable .NET SDK and package releases. MCP server setup: see [START-HUMAN.md](START-HUMAN.md).
- All mode/profile/flag defaults come from [resource-implementation-schema.md](resource-implementation-schema.md) (**Canonical Defaults**).

## Context Budget Rules (Mandatory)

1. Load at most **4 skills + 5 templates** per turn.
2. Keep instruction context around **≤30K tokens per phase** (see `_manifest.json` `contextBudget` for model-specific overrides).
3. Use the **Phase Loading Manifest** (below) for per-phase file lists.
4. Unload prior phase docs when transitioning.
5. Do not preload [quick-reference.md](quick-reference.md) or [sampleapp-patterns.md](sampleapp-patterns.md); load only when needed.
6. Use [sampleapp-patterns.md](sampleapp-patterns.md) before opening any raw sampleapp source.
7. Large files must be loaded selectively:
   - `templates/test-template-*.md`: only needed test type
   - [skills/uno-ui.md](skills/uno-ui.md): dedicated session preferred
8. When context is high during execution, create/update `HANDOFF.md` with resume instructions for next session.

## Session Start (Every AI Turn)

Before any scaffolding work in a new AI session:
1. Load [START-AI.md](START-AI.md) only.
2. Check target project root for an existing `HANDOFF.md` — if found: read **Current Phase** to determine where to resume; read **Next Load Set** for files to load; read **Blockers** to decide whether to continue or route to engineer first
3. Determine current phase:
   - Phases 1-3 (discovery/resources/planning): load corresponding schema files
   - Phase 4 (implementation): load `SKILL.md` + `placeholder-tokens.md`, then check `resource-implementation-schema.md` for `scaffoldMode`, `testingProfile`, host profiles, and enabled flags before loading sub-phase files
4. If required inputs are missing or ambiguous, apply the **Missing-Inputs Protocol** (below) before proceeding

---

## Scaffolding Modes

### `full` (default)
Production-grade architecture with optional workloads and broader quality gates.

### `lite`
Minimal clean architecture for internal tools/PoCs/services.

| Excluded in `lite` | Skill / Phase |
|---|---|
| API Gateway (YARP) | `skills/gateway.md` |
| Multi-tenancy | `skills/multi-tenant.md` |
| Distributed caching | `skills/caching.md` |
| Aspire orchestration | `skills/aspire.md` |
| Scheduler/background services | `skills/background-services.md` |
| Function App | `skills/function-app.md` |
| Uno UI | `skills/uno-ui.md` |
| Notifications | `skills/notifications.md` |
| IaC + CI/CD pipeline | `skills/iac.md`, `skills/cicd.md` |

In `lite` mode, load only: Foundation + App Core + Configuration + Identity + Testing. Add optional hosts only after core stabilizes.

Set mode in [resource-implementation-schema.md](resource-implementation-schema.md) (`scaffoldMode`).

### `api-only`

Single API host, no gateway/UI/scheduler/functions. Aspire optional (for local SQL/Redis).

| Excluded in `api-only` | Skill / Phase |
|---|---|
| API Gateway (YARP) | `skills/gateway.md` |
| Multi-tenancy | `skills/multi-tenant.md` |
| Distributed caching | `skills/caching.md` |
| Scheduler/background services | `skills/background-services.md` |
| Function App | `skills/function-app.md` |
| Uno UI | `skills/uno-ui.md` |
| Notifications | `skills/notifications.md` |
| IaC + CI/CD pipeline | `skills/iac.md`, `skills/cicd.md` |

In `api-only` mode, load only: Foundation + App Core + Configuration + Identity + Testing. Aspire is optional (include when local dev needs SQL/Redis orchestration). Follows the same manifest as `lite` but keeps Aspire as optional.

### Lite Mode — What to Reference from Sample App

The sample app (`sample-app/`) is a `full` mode implementation. When scaffolding in `lite` mode, reference only these portions:

| Layer | Sample App Files to Reference | Skip |
|---|---|---|
| Domain Model | `Domain.Model/` entities, rules, value objects | — |
| Data Access | `Infrastructure.Repositories/` (Trxn + Query repos, EF configs) | CosmosDb/Table/Blob repos |
| Application | `Application.Services/`, `Application.Mappers/`, `Application.Models/` | Message handlers (unless internal events needed) |
| Bootstrapper | `TaskFlow.Bootstrapper/RegisterServices.cs` — use as DI pattern | Skip scheduler/gateway/notification registrations |
| API | `TaskFlow.Api/` — endpoints, startup, middleware | YARP, auth relay |
| Testing | `Test/Test.Unit/`, `Test.Support/` | E2E, Load, Benchmark, Architecture |

**Lite mode entity count guidance:** 1-5 entities. Beyond 5, evaluate whether `full` mode features (gateway, caching, background jobs) would add value.

**Lite mode Aspire:** Optional. If local dev needs SQL + Redis, include Aspire AppHost with just those resources. Skip multi-host orchestration.

**Lite mode identity:** Same stub-first approach. Often `api-only` + lite is the right combo for internal tools — auth may be a simple API key or shared managed identity rather than full Entra flows.

## Workflow — Four Phases

### Phase 1 — Domain Discovery
Define entities, relationships, events, workflows, rules in business language. No implementation details.
- Output: YAML per [domain-specification-schema.md](domain-specification-schema.md) (includes design guidance)

### Phase 2 — Resource Definition
Map domain constructs to Aspire/Azure resources: data stores, datatypes/precision, messaging, hosting.
- Output: YAML per [resource-implementation-schema.md](resource-implementation-schema.md)
- Choose `scaffoldMode` (`full`/`lite`) and profiles here

### Phase 3 — Implementation Plan
Layout ordered steps, resolve open questions, confirm approach before coding.
- Output: `implementation-plan.md` in target project root (template: [implementation-plan.md](implementation-plan.md))

### Phase 4 — Implementation
Code, compile, test. Execute skills in sub-phases below. Ask questions as needed.
- Validate after each sub-phase (`dotnet build`, then targeted tests)

## Phase Loading Manifest

Load the minimum set for the current phase only.

### Session bootstrap
- [START-AI.md](START-AI.md)

### Phase 1 — Domain Discovery
- [domain-specification-schema.md](domain-specification-schema.md)

### Phase 2 — Resource Definition
- [resource-implementation-schema.md](resource-implementation-schema.md)
- [domain-specification-schema.md](domain-specification-schema.md) *(read-only reference)*

### Phase 3 — Implementation Plan
- [implementation-plan.md](implementation-plan.md)
- [domain-specification-schema.md](domain-specification-schema.md) *(read-only reference)*
- [resource-implementation-schema.md](resource-implementation-schema.md) *(read-only reference)*

### Phase 4 base set
- `SKILL.md` (this file)
- [placeholder-tokens.md](placeholder-tokens.md)

### Phase 4a — Foundation
- `skills/solution-structure.md`, `skills/domain-model.md`, `skills/data-access.md`, `skills/package-dependencies.md`
- `templates/entity-template.md`, `templates/ef-configuration-template.md`, `templates/repository-template.md`, `templates/appsettings-template.md`
- `skills/cosmosdb-data.md` / `skills/table-storage.md` / `skills/blob-storage.md` *(if non-SQL entities)*

### Phase 4b — App Core
- `skills/application-layer.md`, `skills/bootstrapper.md`, `skills/api.md`
- `templates/dto-template.md`, `templates/mapper-template.md`, `templates/service-template.md`, `templates/endpoint-template.md`
- `templates/message-handler-template.md` *(if events/handlers)*
- `templates/structure-validator-template.md`, `templates/exception-handler-template.md`

### Phase 4c — Runtime/Edge
Load only enabled concerns: `skills/gateway.md`, `skills/aspire.md`, `skills/configuration.md`, `skills/multi-tenant.md`, `skills/caching.md`, `skills/observability.md`, `skills/security.md`

### Phase 4d — Optional Hosts
- `skills/background-services.md` (scheduler), `skills/function-app.md`, `skills/uno-ui.md` (dedicated session preferred), `skills/notifications.md`
- UI templates when `includeUnoUI: true`

### Phase 4e — Quality + Delivery
- `skills/testing.md` + relevant `templates/test-template-*.md`
- `skills/iac.md`, `skills/cicd.md`

### Phase 4f — Authentication (Final)
- `skills/identity-management.md`

### On-Demand (Load When Debugging)
- `skills/error-handling.md` — cross-cutting error pipeline reference (load when debugging error flows)
- `skills/migrations.md` — EF migration strategy (load when adding/running migrations)

## Phase 4 Skills (Recommended Order)

### 4a — Foundation
1. [skills/solution-structure.md](skills/solution-structure.md)
2. [skills/domain-model.md](skills/domain-model.md)
3. [skills/data-access.md](skills/data-access.md) *(+ cosmosdb/table/blob skills if non-SQL entities)*
4. [skills/package-dependencies.md](skills/package-dependencies.md) *(load with Foundation; re-reference any time packages change)*

### 4b — App Core
5. [skills/application-layer.md](skills/application-layer.md)
6. [skills/bootstrapper.md](skills/bootstrapper.md)
7. [skills/api.md](skills/api.md)

### 4c — Runtime/Edge
8. [skills/gateway.md](skills/gateway.md) *(if enabled)*
9. [skills/multi-tenant.md](skills/multi-tenant.md) *(if enabled)*
10. [skills/caching.md](skills/caching.md) *(if enabled)*
11. [skills/aspire.md](skills/aspire.md) *(if enabled)*
12. [skills/configuration.md](skills/configuration.md)
13. [skills/observability.md](skills/observability.md)
14. [skills/security.md](skills/security.md)

### 4d — Optional Hosts
15. [skills/background-services.md](skills/background-services.md) *(if scheduler enabled)*
16. [skills/function-app.md](skills/function-app.md) *(if enabled)*
17. [skills/uno-ui.md](skills/uno-ui.md) *(if enabled)*
18. [skills/notifications.md](skills/notifications.md) *(if enabled)*
### 4e — Quality + Delivery
19. Testing + delivery:
    - [skills/testing.md](skills/testing.md)
    - [skills/iac.md](skills/iac.md)
    - [skills/cicd.md](skills/cicd.md)
20. Optional infra/data integrations as needed:
    - [skills/cosmosdb-data.md](skills/cosmosdb-data.md)
    - [skills/table-storage.md](skills/table-storage.md)
    - [skills/blob-storage.md](skills/blob-storage.md)
    - [skills/messaging.md](skills/messaging.md)
    - [skills/keyvault.md](skills/keyvault.md)
    - [skills/grpc.md](skills/grpc.md)
    - [skills/external-api.md](skills/external-api.md)

### 4f — Authentication (Final)
21. [skills/identity-management.md](skills/identity-management.md) *(defer to end; use stubs in earlier phases)*

## Template Usage

Use templates for generated artifacts and keep naming aligned with [placeholder-tokens.md](placeholder-tokens.md).

- Backend templates: entity/config/repository/dto/mapper/service/endpoint/rules/message-handler/structure-validator/exception-handler
- UI templates: MVUX model/XAML page/UI model/UI service
- Tests: load only needed file from `templates/test-template-*.md`

## Vertical Slice Shortcut

For an existing solution, use:
- [vertical-slice-checklist.md](vertical-slice-checklist.md)
- Relevant `templates/`

Generate one complete slice, validate, then move to next slice.

## Key Principles

- Clean architecture boundaries
- Bootstrapper-based shared DI wiring
- Static mappers + EF-safe projectors
- `DomainResult`-style railway flow
- Tenant-safe defaults where enabled
- SQL defaults: `nvarchar(N)`, `decimal(10,4)`, `datetime2`
- Stub external dependencies for local compile/run — generate compilable no-op implementations with `// TODO: [CONFIGURE]` comments at every integration point (stub class, DI registration, appsettings section)
- Keep Aspire config and IaC names aligned
- Start with minimal viable profiles, promote later

---

## Fail-Fast Protocol

After every build:
- **Code-generation issue** (usings/references/DI/wiring/packages): attempt one focused fix pass, rebuild.
- **Missing package in `Directory.Packages.props`**: add at latest stable version, restore, rebuild.
- **Infrastructure issue** (feed auth, env vars, Docker, certs, SQL/cloud access): do not loop fixes. Document blocker in `HANDOFF.md`, point engineer to [engineer-checklist.md](engineer-checklist.md).

## Git Checkpoint Protocol

Commit after each successful sub-phase to enable safe rollback:

1. **After Phase 4a (Foundation):** `git add -A && git commit -m "scaffold: foundation — entities, data access, domain rules"`
2. **After Phase 4b (App Core):** `git add -A && git commit -m "scaffold: app core — services, DTOs, mappers, endpoints"`
3. **After Phase 4c (Runtime/Edge):** `git add -A && git commit -m "scaffold: runtime — gateway, aspire, config, caching"`
4. **After Phase 4d (Optional Hosts):** `git add -A && git commit -m "scaffold: optional hosts — scheduler, functions, UI"`
5. **After Phase 4e (Quality):** `git add -A && git commit -m "scaffold: quality — tests, IaC, CI/CD"`

If a sub-phase fails after the one-pass fix attempt:
- `git stash` the broken changes.
- Log blocker in `HANDOFF.md`.
- Continue with non-blocked sub-phases from the last clean commit.
- Return to stashed changes when the blocker is resolved.

## Missing-Inputs Protocol

When domain inputs are absent or ambiguous:
- **Required** (`ProjectName`, `customNugetFeeds`, at least one entity): ask before proceeding.
- **Defaults** (modes/profiles/flags): use [resource-implementation-schema.md](resource-implementation-schema.md) **Canonical Defaults**; note assumptions inline.
- **Partial entity definitions**: scaffold what is defined; emit `// TODO` stubs for missing properties/rules.

### Phase 3 Pre-Flight: Custom NuGet Feeds

At Phase 3 start: ask for custom/private NuGet feed URLs and auth method. Update `nuget.config`, run `dotnet restore` — must exit 0 before Phase 4.

## Validation Cadence

- Foundation/App Core: `dotnet build`
- Feature slice: build + targeted unit, endpoint, and integration tests
- Pre-merge baseline: full test run
- IaC: run commands from [engineer-checklist.md](engineer-checklist.md)

## Mixed-Store Slice Gate

For slices spanning SQL + Cosmos/Table/Blob + messaging:
- Explicit consistency boundary (authoritative store + projection store)
- Reconciliation handler/job with replay-safe correction logic
- Drift detection check in post-generation verification

## Session State (`HANDOFF.md`)

Create in target project root during Phase 4 when context is high or at session boundaries. Not needed during Phases 1-3. See [HANDOFF.md](HANDOFF.md) for template.

