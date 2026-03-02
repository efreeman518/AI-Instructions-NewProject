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
2. Keep instruction context around **≤30K tokens per phase**.
3. Use [ai-build-optimization.md](ai-build-optimization.md) Phase Loading Manifest.
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
    - Phase 4 (implementation): load `SKILL.md` + `placeholder-tokens.md` + `ai-build-optimization.md`, then check `resource-implementation-schema.md` for `scaffoldMode`, `testingProfile`, host profiles, and enabled flags before loading sub-phase files
4. If required inputs are missing or ambiguous, apply the **Missing-Inputs Protocol** in [ai-build-optimization.md](ai-build-optimization.md) before proceeding

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

## Workflow — Four Phases

### Phase 1 — Domain Discovery
Define entities, relationships, events, workflows, rules in business language. No implementation details.
- Guide: [domain-design-guide.md](domain-design-guide.md)
- Output: YAML per [domain-specification-schema.md](domain-specification-schema.md)

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

## Phase File Router

Per-phase file load lists are in the **Phase Loading Manifest** in [ai-build-optimization.md](ai-build-optimization.md). Load the minimum set for the current phase only.

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

### 4d — Optional Hosts
13. [skills/background-services.md](skills/background-services.md) *(if scheduler enabled)*
14. [skills/function-app.md](skills/function-app.md) *(if enabled)*
15. [skills/uno-ui.md](skills/uno-ui.md) *(if enabled)*
16. [skills/notifications.md](skills/notifications.md) *(if enabled)*
### 4e — Quality + Delivery
17. Testing + delivery:
    - [skills/testing.md](skills/testing.md)
    - [skills/iac.md](skills/iac.md)
    - [skills/cicd.md](skills/cicd.md)
18. Optional infra/data integrations as needed:
    - [skills/cosmosdb-data.md](skills/cosmosdb-data.md)
    - [skills/table-storage.md](skills/table-storage.md)
    - [skills/blob-storage.md](skills/blob-storage.md)
    - [skills/messaging.md](skills/messaging.md)
    - [skills/keyvault.md](skills/keyvault.md)
    - [skills/grpc.md](skills/grpc.md)
    - [skills/external-api.md](skills/external-api.md)

### 4f — Authentication (Final)
19. [skills/identity-management.md](skills/identity-management.md) *(defer to end; use stubs in earlier phases)*

## Template Usage

Use templates for generated artifacts and keep naming aligned with [placeholder-tokens.md](placeholder-tokens.md).

- Backend templates: entity/config/repository/dto/mapper/service/endpoint/rules/message-handler
- UI templates: MVUX model/XAML page/UI model/UI service
- Tests: load only needed file from `templates/test-template-*.md`

## Vertical Slice Shortcut

For an existing solution, use:
- [vertical-slice-checklist.md](vertical-slice-checklist.md)
- Relevant `templates/`
- [ai-build-optimization.md](ai-build-optimization.md)

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

