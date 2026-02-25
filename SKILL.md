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

- `sampleapp/` is **read-only reference**; never edit/build/delete files there.
- Generate code only in the user’s new project directory.
- Use `.slnx` (not legacy `.sln`).
- Use central package management (`Directory.Packages.props`).
- After adding packages, update to latest stable and verify restore/build.
- Record instruction gaps in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md) (do not hot-edit baseline instructions mid-scaffold).

## Context Budget Rules (Mandatory)

1. Load at most **4 skills + 4 templates** per turn.
2. Keep instruction context around **≤30K tokens per phase**.
3. Use [ai-build-optimization.md](ai-build-optimization.md) Phase Loading Manifest.
4. Unload prior phase docs when transitioning.
5. Use [sampleapp-patterns.md](sampleapp-patterns.md) before opening any raw sampleapp source.
6. Large files must be loaded selectively:
   - [domain-inputs.schema.md](domain-inputs.schema.md): only active sections
   - `templates/test-template-*.md`: only needed test type
   - [skills/uno-ui.md](skills/uno-ui.md): dedicated session preferred
7. When context is high and work is stable, create/update `HANDOFF.md`.

## Scaffolding Modes

### `full` (default)
Production-grade architecture with optional workloads and broader quality gates.

### `lite`
Minimal clean architecture for internal tools/PoCs/services. Excludes by default: Gateway, multi-tenant, caching, Uno UI, scheduler/background, Function App, Aspire-heavy orchestration.

Set mode in [domain-inputs.schema.md](domain-inputs.schema.md) (`scaffoldMode`).

## Workflow

1. **Domain discovery conversation** (before YAML)
2. Produce structured inputs via [domain-inputs.schema.md](domain-inputs.schema.md)
3. Choose mode (`full`/`lite`) and profiles (`testingProfile`, `functionProfile`, `unoProfile`)
4. Execute skills phase-by-phase (below)
5. Validate (`dotnet build`, then targeted tests)
6. Capture blockers/next actions in `HANDOFF.md`

## Domain Discovery Protocol (Condensed)

Before code generation, collaborate on:
- Business context and core workflows
- Entities and lifecycle states
- Relationships and boundaries
- Rules/invariants
- Data-store choices (SQL/Cosmos/Table/Blob)
- Tenancy/access model
- Events/integration points
- AI/vector/agent opportunities (if relevant)

Transition only after model summary is confirmed.

## Skills (Recommended Order)

1. [skills/solution-structure.md](skills/solution-structure.md)
2. [skills/domain-model.md](skills/domain-model.md)
3. [skills/data-access.md](skills/data-access.md)
4. [skills/application-layer.md](skills/application-layer.md)
5. [skills/bootstrapper.md](skills/bootstrapper.md)
6. [skills/api.md](skills/api.md)
7. [skills/gateway.md](skills/gateway.md) *(if enabled)*
8. [skills/multi-tenant.md](skills/multi-tenant.md) *(if enabled)*
9. [skills/caching.md](skills/caching.md) *(if enabled)*
10. [skills/aspire.md](skills/aspire.md) *(if enabled)*
11. [skills/background-services.md](skills/background-services.md) *(if scheduler enabled)*
12. [skills/function-app.md](skills/function-app.md) *(if enabled)*
13. [skills/uno-ui.md](skills/uno-ui.md) *(if enabled)*
14. [skills/notifications.md](skills/notifications.md) *(if enabled)*
15. [skills/configuration.md](skills/configuration.md)
16. [skills/identity-management.md](skills/identity-management.md)
17. Optional infra/data integrations as needed:
    - [skills/cosmosdb-data.md](skills/cosmosdb-data.md)
    - [skills/table-storage.md](skills/table-storage.md)
    - [skills/blob-storage.md](skills/blob-storage.md)
    - [skills/messaging.md](skills/messaging.md)
    - [skills/keyvault.md](skills/keyvault.md)
    - [skills/grpc.md](skills/grpc.md)
    - [skills/external-api.md](skills/external-api.md)
18. Delivery:
    - [skills/testing.md](skills/testing.md)
    - [skills/iac.md](skills/iac.md)
    - [skills/cicd.md](skills/cicd.md)
    - [skills/package-dependencies.md](skills/package-dependencies.md)

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
- Stub external dependencies for local compile/run
- Keep Aspire config and IaC names aligned
- Start with minimal viable profiles, promote later

## Reference Files

- [placeholder-tokens.md](placeholder-tokens.md)
- [domain-inputs.schema.md](domain-inputs.schema.md)
- [ai-build-optimization.md](ai-build-optimization.md)
- [sampleapp-patterns.md](sampleapp-patterns.md)
- [quick-reference.md](quick-reference.md)
- [engineer-checklist.md](engineer-checklist.md)
- [troubleshooting.md](troubleshooting.md)
- [GET-STARTED-human.md](GET-STARTED-human.md)

## Tooling Notes

- Prefer latest stable .NET and package releases.
- Configure MCP servers before scaffolding; canonical MCP guidance is in [GET-STARTED-human.md](GET-STARTED-human.md).