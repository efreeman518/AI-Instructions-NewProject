# Vertical Slice — Generated Files Checklist

Use this checklist after generating a new entity slice to ensure no required files or wiring steps were missed.

Placeholder tokens: [placeholder-tokens.md](placeholder-tokens.md).

---

## Add Entity to Existing Project — Fast Path

Use this when adding a new entity to an **already-scaffolded** solution. Skip full phase loading — load only what the slice needs.

### Pre-Flight

- [ ] Solution builds clean: `dotnet build`
- [ ] Identify existing: `RegisterServices.cs`, `{App}DbContextTrxn`, `{App}DbContextQuery`, `WebApplicationBuilderExtensions.cs`
- [ ] Confirm `scaffoldMode` and `testingProfile` from `resource-implementation.yaml`

### Load Set for Slice

1. `SKILL.md` (base reference)
2. `placeholder-tokens.md`
3. Backend templates: `entity-template.md`, `ef-configuration-template.md`, `repository-template.md`, `dto-template.md`, `mapper-template.md`, `service-template.md`, `endpoint-template.md`, `structure-validator-template.md`
4. If domain rules needed: `domain-rules-template.md`
5. If child collections: `updater-template.md`
6. If UI enabled: `mvux-model-template.md`, `xaml-page-template.md`, `ui-model-template.md`, `ui-service-template.md`

### Slice Execution Order

1. Create entity + enum/flags in `Domain.Model`
2. Create EF configuration in `Infrastructure.Repositories`
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
- [ ] Aspire AppHost updated (only if new project added to solution)

### Validation

```powershell
dotnet build
dotnet test --filter "TestCategory={Entity}"
```

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
| Domain | `src/Domain/{Project}.Domain.Model/Entities/{Entity}.cs` | [entity-template.md](templates/entity-template.md) | yes |
| Domain (optional) | `src/Domain/{Project}.Domain.Model/Enums/{Entity}Status.cs` | — | if flags/status enum used |
| Domain (optional) | `src/Domain/{Project}.Domain.Rules/{Entity}Rules.cs` | [domain-rules-template.md](templates/domain-rules-template.md) | if rules/state machine/policy matrix used |
| Domain (optional) | `src/Domain/{Project}.Domain.Rules/{Entity}*TransitionRule.cs` | [domain-rules-template.md](templates/domain-rules-template.md) | if state transitions are constrained |
| Data | `src/Infrastructure/{Project}.Infrastructure.Data/EntityConfigurations/{Entity}Configuration.cs` | [ef-configuration-template.md](templates/ef-configuration-template.md) | yes |
| Data | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryTrxn.cs` | [repository-template.md](templates/repository-template.md) | yes |
| Data | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryQuery.cs` | [repository-template.md](templates/repository-template.md) | yes |
| Data (optional) | `src/Infrastructure/{Project}.Infrastructure.Repositories/Updaters/{Entity}Updater.cs` | [updater-template.md](templates/updater-template.md) | if child collections |
| App | `src/Application/{Project}.Application.Models/{Entity}/{Entity}Dto.cs` | [dto-template.md](templates/dto-template.md) | yes |
| App | `src/Application/{Project}.Application.Models/{Entity}/{Entity}SearchFilter.cs` | [dto-template.md](templates/dto-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Services/I{Entity}Service.cs` | [service-template.md](templates/service-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Repositories/I{Entity}RepositoryTrxn.cs` | [repository-template.md](templates/repository-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Repositories/I{Entity}RepositoryQuery.cs` | [repository-template.md](templates/repository-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Mappers/{Entity}Mapper.cs` | [mapper-template.md](templates/mapper-template.md) | yes |
| App | `src/Application/{Project}.Application.Services/Services/{Entity}Service.cs` | [service-template.md](templates/service-template.md) | yes |
| App (optional) | `src/Application/{Project}.Application.Services/Validators/{Entity}Validator.cs` | — | if custom validator used |
| API | `src/{Host}/{Host}.Api/Endpoints/{Entity}Endpoints.cs` | [endpoint-template.md](templates/endpoint-template.md) | yes |

---

## Required Wiring Updates

| File | Required Update |
|---|---|
| `src/{Host}/{Host}.Bootstrapper/RegisterServices.cs` | register repos + service (+ validators if used) |
| `src/{Host}/{Host}.Api/WebApplicationBuilderExtensions.cs` | add `Map{Entity}Endpoints()` |
| `src/Infrastructure/{Project}.Infrastructure.Data/{App}DbContextTrxn.cs` | add `DbSet<{Entity}>` |
| `src/Infrastructure/{Project}.Infrastructure.Data/{App}DbContextQuery.cs` | add `DbSet<{Entity}>` |

Migration command:

```bash
dotnet ef migrations add Add{Entity} --project src/Infrastructure/{Project}.Infrastructure.Data --startup-project src/{Host}/{Host}.Api
```

---

## Test Slice (Recommended)

| File Path | Template |
|---|---|
| `src/Test/Test.Unit/Domain/{Entity}Tests.cs` | [test-template-unit.md](templates/test-template-unit.md) |
| `src/Test/Test.Unit/Services/{Entity}ServiceTests.cs` | [test-template-unit.md](templates/test-template-unit.md) |
| `src/Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs` | [test-template-unit.md](templates/test-template-unit.md) |
| `src/Test/Test.Unit/Repositories/{Entity}RepositoryQueryTests.cs` | [test-template-unit.md](templates/test-template-unit.md) |
| `src/Test/Test.Unit/Mappers/{Entity}MapperTests.cs` | [test-template-unit.md](templates/test-template-unit.md) |
| `src/Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs` | [test-template-integration.md](templates/test-template-integration.md) |
| `src/Test/Test.Architecture/` updates | [test-template-quality.md](templates/test-template-quality.md) |

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

Templates: [ui-model-template.md](templates/ui-model-template.md), [ui-service-template.md](templates/ui-service-template.md), [mvux-model-template.md](templates/mvux-model-template.md), [xaml-page-template.md](templates/xaml-page-template.md).

---

## Post-Generation Verification

### DI and Routing

- [ ] `I{Entity}RepositoryTrxn` -> `{Entity}RepositoryTrxn` registered
- [ ] `I{Entity}RepositoryQuery` -> `{Entity}RepositoryQuery` registered
- [ ] `I{Entity}Service` -> `{Entity}Service` registered
- [ ] `Map{Entity}Endpoints()` wired in API builder extensions

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

- [ ] Authoritative store vs projection store boundary is explicit
- [ ] Reconciliation handler/job exists for drift detection and replay-safe correction
- [ ] Replay window/late-arrival handling is validated in tests

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

## Bootstrapper Snippet

```csharp
services.AddScoped<I{Entity}RepositoryTrxn, {Entity}RepositoryTrxn>();
services.AddScoped<I{Entity}RepositoryQuery, {Entity}RepositoryQuery>();
services.AddScoped<I{Entity}Service, {Entity}Service>();
```

---

## Quick Runbook

1. `dotnet build`
2. `dotnet ef migrations add Add{Entity}`
3. `dotnet test --filter "TestCategory=Unit"`
4. `dotnet test --filter "TestCategory=Endpoint"`