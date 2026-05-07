# Vertical Slice — Generated Files Checklist

Use this checklist after generating a new entity slice to ensure no required files or wiring steps were missed.

Placeholder tokens: [../ai/placeholder-tokens.md](../ai/placeholder-tokens.md).

---

## Add Entity to Existing Project — Fast Path

Use this when adding a new entity to an **already-scaffolded** solution. Skip full phase loading — load only what the slice needs.

### Pre-Flight

- [ ] Solution builds clean: `dotnet build`
- [ ] Identify existing: `RegisterServices.cs`, `{App}DbContextTrxn`, `{App}DbContextQuery`, `WebApplicationBuilderExtensions.cs`
- [ ] Confirm `scaffoldMode` and `testingProfile` from `resource-implementation.yaml`
- [ ] If this slice introduces a new domain term, role, event, custom action, or design decision, append it to `UBIQUITOUS-LANGUAGE.md` / `DESIGN-DECISIONS.md` and update `domain-specification.yaml` **before** generating code (see [../README.md](../README.md) § Phase-1 Artifact Lifecycle)

### Load Set for Slice

1. `ai/SKILL.md` (base reference)
2. `ai/placeholder-tokens.md`
3. Backend templates: `entity-template.md`, `ef-configuration-template.md`, `repository-template.md`, `data-mapping-template.md`, `service-template.md`, `endpoint-template.md`, `structure-validator-template.md`
4. If domain rules needed: `domain-rules-template.md`
5. If child collections: `updater-template.md`
6. If Uno UI enabled: `uno-mvux-model-template.md`, `uno-xaml-page-template.md`, `uno-ui-client-layer.md`
7. If Blazor UI enabled: `skills/ui-blazor.md` — add a Refit method group, entity list page, and entity new/edit page

### Slice Execution Order

1. Create entity + enum/flags in `Domain.Model`
2. Create EF configuration in `Infrastructure.Data`
3. Add `DbSet<{Entity}>` to both DbContexts
4. Create repository interface + implementations (Trxn + Query)
5. Create DTO + SearchFilter in `Application.Models`
6. Create mapper in `Application.Mappers`
7. Create StructureValidator in `Application.Services/Rules`
8. Create service + interface
9. Create endpoint
10. Wire DI in `RegisterServices.cs` (repos + service)
11. Map endpoints in `WebApplicationBuilderExtensions.cs`
12. Run migration: `dotnet ef migrations add Add{Entity} ...`

### Wiring Checklist

- [ ] `DbSet<{Entity}>` added to `{App}DbContextTrxn` and `{App}DbContextQuery`
- [ ] Repos + service registered in `RegisterServices.cs`
- [ ] `Map{Entity}Endpoints()` called in `WebApplicationBuilderExtensions.cs`
- [ ] **[Multi-tenant only]** `ITenantBoundaryValidator` registered (once, not per entity)
- [ ] Aspire AppHost updated (only if new project added to solution)

### Validation

Gate commands: [execution-gates.md](execution-gates.md) § Core Loop. Scope test filter to the new entity (`FullyQualifiedName~{Entity}`). For recurring test failures, see [troubleshooting.md](troubleshooting.md).

### Prompt Pattern

```
Add a new {Entity} vertical slice to the existing {Project} solution.
Follow vertical-slice-checklist.md fast-path.
```

---

## Slice Modes

- **Single-entity slice**: one primary entity with local relationships.
- **Composite slice**: one feature spanning multiple coupled entities/stores.

Use composite mode when business behavior cannot be completed safely as one isolated entity (for example order + reservation + refund, entitlement + purchase + content visibility).

---

## Backend Slice (Required)

For entity `{Entity}`:

| Layer | File Path | Template | Required |
|---|---|---|---|
| Domain | `src/Domain/{Project}.Domain.Model/Entities/{Entity}.cs` | [entity-template.md](../templates/entity-template.md) | yes |
| Domain (optional) | `src/Domain/{Project}.Domain.Model/Enums/{Entity}Status.cs` | — | if flags/status enum used |
| Domain (optional) | `src/Domain/{Project}.Domain.Model/Rules/{Entity}Rules.cs` | [domain-rules-template.md](../templates/domain-rules-template.md) | if rules/state machine/policy matrix used |
| Domain (optional) | `src/Domain/{Project}.Domain.Model/Rules/{Entity}*TransitionRule.cs` | [domain-rules-template.md](../templates/domain-rules-template.md) | if state transitions are constrained |
| Data | `src/Infrastructure/{Project}.Infrastructure.Data/EntityConfigurations/{Entity}Configuration.cs` | [ef-configuration-template.md](../templates/ef-configuration-template.md) | yes |
| Data | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryTrxn.cs` | [repository-template.md](../templates/repository-template.md) | yes |
| Data | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryQuery.cs` | [repository-template.md](../templates/repository-template.md) | yes |
| Data (optional) | `src/Infrastructure/{Project}.Infrastructure.Repositories/Updaters/{Entity}Updater.cs` | [updater-template.md](../templates/updater-template.md) | if child collections |
| App | `src/Application/{Project}.Application.Models/{Entity}/{Entity}Dto.cs` | [data-mapping-template.md](../templates/data-mapping-template.md) | yes |
| App | `src/Application/{Project}.Application.Models/{Entity}/{Entity}SearchFilter.cs` | [data-mapping-template.md](../templates/data-mapping-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Services/I{Entity}Service.cs` | [service-template.md](../templates/service-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Repositories/I{Entity}RepositoryTrxn.cs` | [repository-template.md](../templates/repository-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Repositories/I{Entity}RepositoryQuery.cs` | [repository-template.md](../templates/repository-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Mappers/{Entity}Mapper.cs` | [data-mapping-template.md](../templates/data-mapping-template.md) | yes |
| App | `src/Application/{Project}.Application.Services/Services/{Entity}Service.cs` | [service-template.md](../templates/service-template.md) | yes |
| App (optional) | `src/Application/{Project}.Application.Services/Validators/{Entity}Validator.cs` | — | if custom validator used |
| API | `src/Host/{Host}.Api/Endpoints/{Entity}Endpoints.cs` | [endpoint-template.md](../templates/endpoint-template.md) | yes |

---

## Required Wiring Updates

| File | Required Update |
|---|---|
| `src/Host/{Host}.Bootstrapper/RegisterServices.cs` | register repos + service (+ validators if used) |
| `src/Host/{Host}.Api/WebApplicationBuilderExtensions.cs` | add `Map{Entity}Endpoints()` |
| `src/Infrastructure/{Project}.Infrastructure.Data/{App}DbContextTrxn.cs` | add `DbSet<{Entity}>` |
| `src/Infrastructure/{Project}.Infrastructure.Data/{App}DbContextQuery.cs` | add `DbSet<{Entity}>` |

Migration command:

```bash
dotnet ef migrations add Add{Entity} --project src/Infrastructure/{Project}.Infrastructure.Data --startup-project src/Host/{Host}.Api
```

---

## Test Slice (Recommended)

| File Path | Template |
|---|---|
| `src/Test/Test.Unit/Domain/{Entity}Tests.cs` | [test-templates-domain.md](../templates/test-templates-domain.md) |
| `src/Test/Test.Unit/Services/{Entity}ServiceTests.cs` | [test-templates-service.md](../templates/test-templates-service.md) |
| `src/Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs` | [test-templates-repository.md](../templates/test-templates-repository.md) |
| `src/Test/Test.Unit/Repositories/{Entity}RepositoryQueryTests.cs` | [test-templates-repository.md](../templates/test-templates-repository.md) |
| `src/Test/Test.Unit/Mappers/{Entity}MapperTests.cs` | [test-templates-service.md](../templates/test-templates-service.md) |
| `src/Test/Test.Endpoints/Endpoints/{Entity}EndpointsTests.cs` | [test-templates-endpoint.md](../templates/test-templates-endpoint.md) |
| `src/Test/Test.Architecture/` updates | [test-templates-quality.md](../templates/test-templates-quality.md) |

### Required Test Gate by Profile

- `minimal`: Unit + Endpoint
- `balanced`: Unit + Endpoint + Integration + Architecture
- `comprehensive`: Balanced + E2E + Load + Benchmark (where enabled)

For composite slices, include at least one integration scenario that traverses all participating entities.

---

## Uno UI Slice (Only if `includeUnoUI: true`)

Required UI artifacts:

- `Business/Models/{Entity}.cs`
- `Business/Services/{Feature}/I{Entity}Service.cs`
- `Business/Services/{Feature}/{Entity}Service.cs`
- `Presentation/{Entity}ListModel.cs`
- `Presentation/{Entity}DetailModel.cs`
- `Presentation/Create{Entity}Model.cs`
- `Views/{Entity}ListPage.xaml` + `.xaml.cs`
- `Views/{Entity}DetailPage.xaml` + `.xaml.cs`
- `Views/Create{Entity}Page.xaml` + `.xaml.cs`

Also update `App.xaml.host.cs`:

- register UI services,
- register navigation routes.

Templates: [uno-ui-client-layer.md](../templates/uno-ui-client-layer.md), [uno-mvux-model-template.md](../templates/uno-mvux-model-template.md), [uno-xaml-page-template.md](../templates/uno-xaml-page-template.md).

---

## Post-Generation Verification

### DI and Routing

- [ ] `I{Entity}RepositoryTrxn` -> `{Entity}RepositoryTrxn` registered
- [ ] `I{Entity}RepositoryQuery` -> `{Entity}RepositoryQuery` registered
- [ ] `I{Entity}Service` -> `{Entity}Service` registered
- [ ] `Map{Entity}Endpoints()` wired in API builder extensions
- [ ] **[Multi-tenant only]** `ITenantBoundaryValidator` -> `TenantBoundaryValidator` registered (once for all entities)

### Data Access

- [ ] `DbSet<{Entity}>` in both Trxn and Query contexts
- [ ] `{Entity}Configuration` applies expected relationships/indexes
- [ ] migration generated and applied (or startup migration path validated)

### Build and Tests

- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] endpoint slice reachable in OpenAPI/Scalar when enabled

### Domain Rules / Policy

- [ ] Domain rule artifacts exist when Phase 1 rules/state machine/policy matrix are defined
- [ ] Transition/guard rules are wired into service or domain operations

### Mixed-Store / Reconciliation (if applicable)

For slices spanning SQL + Cosmos/Table/Blob + messaging, see [OPERATIONS.md](OPERATIONS.md) § Mixed-Store Slice Gate. Required:

- [ ] Authoritative store vs projection store boundary is explicit (writes always go to authoritative first; projections sync via domain events).
- [ ] Reconciliation job exists for drift detection and replay-safe correction.
- [ ] Replay window/late-arrival handling validated in tests.
- [ ] Projection reads never serve stale data for consistency-critical operations.

For implementation patterns (SQL + Cosmos composite, reconciliation job shape), see [data-persistence-advanced.md](data-persistence-advanced.md).

### Timeline / Support Trace (if applicable)

- [ ] Immutable timeline/audit projection is emitted for support/dispute critical workflows
- [ ] Timeline query/read endpoint (or equivalent query path) is available

### Content Lifecycle (if applicable)

- [ ] Draft and published snapshot semantics are modeled
- [ ] Scheduled publish is idempotent
- [ ] Rollback target/version policy is defined

### UI (if enabled)

- [ ] UI service DI and routes added
- [ ] list/detail/create pages render

---

DI registration shape and migration command live in the Fast Path section above; gate commands are canonical in [execution-gates.md](execution-gates.md) § Core Loop.
