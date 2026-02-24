# Vertical Slice — Generated Files Checklist

When adding a new entity to an existing solution, generate all files in this checklist. Use this as a verification list after generating to ensure nothing was missed.

> **Placeholder reference:** See [placeholder-tokens.md](placeholder-tokens.md) for token definitions and casing conventions.

---

## Backend Vertical Slice

For an entity named `{Entity}` (e.g., `TodoItem`):

### Domain Layer

| # | File Path | Template | Notes |
|---|-----------|----------|-------|
| 1 | `src/Domain/{Project}.Domain.Model/Entities/{Entity}.cs` | [entity-template.md](templates/entity-template.md) | Entity with `Create()` factory, private setters |
| 2 | `src/Domain/{Project}.Domain.Model/Enums/{Entity}Status.cs` | — | Only if entity has a flags enum property |

### Data Access Layer

| # | File Path | Template | Notes |
|---|-----------|----------|-------|
| 3 | `src/Infrastructure/{Project}.Infrastructure.Data/EntityConfigurations/{Entity}Configuration.cs` | [ef-configuration-template.md](templates/ef-configuration-template.md) | EF `IEntityTypeConfiguration` |
| 4 | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryTrxn.cs` | [repository-template.md](templates/repository-template.md) | Write repository |
| 5 | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryQuery.cs` | [repository-template.md](templates/repository-template.md) | Read repository |
| 6 | `src/Infrastructure/{Project}.Infrastructure.Repositories/Updaters/{Entity}Updater.cs` | [updater-template.md](templates/updater-template.md) | **Only if entity has child collections** |

### Application Layer

| # | File Path | Template | Notes |
|---|-----------|----------|-------|
| 7 | `src/Application/{Project}.Application.Contracts/DTOs/{Entity}Dto.cs` | [dto-template.md](templates/dto-template.md) | DTO record |
| 8 | `src/Application/{Project}.Application.Contracts/DTOs/{Entity}SearchFilter.cs` | [dto-template.md](templates/dto-template.md) | Search filter class |
| 9 | `src/Application/{Project}.Application.Contracts/Interfaces/I{Entity}Service.cs` | [service-template.md](templates/service-template.md) | Service interface |
| 10 | `src/Application/{Project}.Application.Contracts/Interfaces/I{Entity}RepositoryTrxn.cs` | [repository-template.md](templates/repository-template.md) | Repository interface (write) |
| 11 | `src/Application/{Project}.Application.Contracts/Interfaces/I{Entity}RepositoryQuery.cs` | [repository-template.md](templates/repository-template.md) | Repository interface (read) |
| 12 | `src/Application/{Project}.Application.Services/Mappers/{Entity}Mapper.cs` | [mapper-template.md](templates/mapper-template.md) | Static mapper + projectors |
| 13 | `src/Application/{Project}.Application.Services/Services/{Entity}Service.cs` | [service-template.md](templates/service-template.md) | CRUD service implementation |
| 14 | `src/Application/{Project}.Application.Services/Validators/{Entity}Validator.cs` | — | Validator returning `Result` (see [application-layer.md](skills/application-layer.md)) |

### API Layer

| # | File Path | Template | Notes |
|---|-----------|----------|-------|
| 15 | `src/{Host}/{Host}.Api/Endpoints/{Entity}Endpoints.cs` | [endpoint-template.md](templates/endpoint-template.md) | Minimal API endpoint class |

### Registration

| # | File | Action |
|---|------|--------|
| 16 | `src/{Host}/{Host}.Bootstrapper/RegisterServices.cs` | Add DI registrations for repository, service, and validators |
| 17 | `src/{Host}/{Host}.Api/WebApplicationBuilderExtensions.cs` | Add `Map{Entity}Endpoints()` in `SetupApiVersionedEndpoints` |
| 18 | `src/Infrastructure/{Project}.Infrastructure.Data/{App}DbContextTrxn.cs` | Add `DbSet<{Entity}>` |
| 19 | `src/Infrastructure/{Project}.Infrastructure.Data/{App}DbContextQuery.cs` | Add `DbSet<{Entity}>` |

### Migration

| # | Command | Notes |
|---|---------|-------|
| 20 | `dotnet ef migrations add Add{Entity} --project src/Infrastructure/{Project}.Infrastructure.Data --startup-project src/{Host}/{Host}.Api` | Run after all data access files are created |

---

## Test Vertical Slice

| # | File Path | Template | Notes |
|---|-----------|----------|-------|
| 21 | `src/Test/Test.Unit/Domain/{Entity}Tests.cs` | [test-template.md](templates/test-template.md) | Entity domain tests |
| 22 | `src/Test/Test.Unit/Services/{Entity}ServiceTests.cs` | [test-template.md](templates/test-template.md) | Service unit tests (mock + InMemory) |
| 23 | `src/Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs` | [test-template.md](templates/test-template.md) | Repository CRUD tests |
| 24 | `src/Test/Test.Unit/Repositories/{Entity}RepositoryQueryTests.cs` | [test-template.md](templates/test-template.md) | Query repository tests |
| 25 | `src/Test/Test.Unit/Mappers/{Entity}MapperTests.cs` | [test-template.md](templates/test-template.md) | Mapper tests |
| 26 | `src/Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs` | [test-template.md](templates/test-template.md) | HTTP endpoint integration tests |
| 27 | `src/Test/Test.Architecture/` | [test-template.md](templates/test-template.md) | Update assembly references if this is the first entity |

---

## Uno UI Vertical Slice (when `includeUnoUI: true`)

| # | File Path | Template | Notes |
|---|-----------|----------|-------|
| 28 | `src/{Host}/{Host}.UI/Business/Models/{Entity}.cs` | [ui-model-template.md](templates/ui-model-template.md) | Client-side record model |
| 29 | `src/{Host}/{Host}.UI/Business/Services/{Feature}/I{Entity}Service.cs` | [ui-service-template.md](templates/ui-service-template.md) | UI service interface |
| 30 | `src/{Host}/{Host}.UI/Business/Services/{Feature}/{Entity}Service.cs` | [ui-service-template.md](templates/ui-service-template.md) | UI service implementation |
| 31 | `src/{Host}/{Host}.UI/Presentation/{Entity}ListModel.cs` | [mvux-model-template.md](templates/mvux-model-template.md) | MVUX list model |
| 32 | `src/{Host}/{Host}.UI/Presentation/{Entity}DetailModel.cs` | [mvux-model-template.md](templates/mvux-model-template.md) | MVUX detail model |
| 33 | `src/{Host}/{Host}.UI/Presentation/Create{Entity}Model.cs` | [mvux-model-template.md](templates/mvux-model-template.md) | MVUX create/edit model |
| 34 | `src/{Host}/{Host}.UI/Views/{Entity}ListPage.xaml` | [xaml-page-template.md](templates/xaml-page-template.md) | List page XAML |
| 35 | `src/{Host}/{Host}.UI/Views/{Entity}ListPage.xaml.cs` | [xaml-page-template.md](templates/xaml-page-template.md) | List page code-behind |
| 36 | `src/{Host}/{Host}.UI/Views/{Entity}DetailPage.xaml` | [xaml-page-template.md](templates/xaml-page-template.md) | Detail page XAML |
| 37 | `src/{Host}/{Host}.UI/Views/{Entity}DetailPage.xaml.cs` | [xaml-page-template.md](templates/xaml-page-template.md) | Detail page code-behind |
| 38 | `src/{Host}/{Host}.UI/Views/Create{Entity}Page.xaml` | [xaml-page-template.md](templates/xaml-page-template.md) | Create/edit page XAML |
| 39 | `src/{Host}/{Host}.UI/Views/Create{Entity}Page.xaml.cs` | [xaml-page-template.md](templates/xaml-page-template.md) | Create/edit page code-behind |

### UI Registration

| # | File | Action |
|---|------|--------|
| 40 | `src/{Host}/{Host}.UI/App.xaml.host.cs` | Register `I{Entity}Service` / `{Entity}Service` in `ConfigureServices` |
| 41 | `src/{Host}/{Host}.UI/App.xaml.host.cs` | Register navigation routes for list, detail, and create pages |

---

## Post-Creation Wiring Checklist

After generating all files for a new entity, walk through this checklist to ensure everything is connected:

### DI Registration
- [ ] `I{Entity}RepositoryTrxn` → `{Entity}RepositoryTrxn` registered in `RegisterServices.cs`
- [ ] `I{Entity}RepositoryQuery` → `{Entity}RepositoryQuery` registered in `RegisterServices.cs`
- [ ] `I{Entity}Service` → `{Entity}Service` registered in `RegisterServices.cs`
- [ ] `Map{Entity}Endpoints()` called in `WebApplicationBuilderExtensions.cs`

### Database
- [ ] `DbSet<{Entity}>` added to both `{App}DbContextTrxn` and `{App}DbContextQuery`
- [ ] `{Entity}Configuration` class applies `EntityBaseConfiguration<{Entity}>` and configures all relationships
- [ ] Migration created with `dotnet ef migrations add Add{Entity}`
- [ ] Migration applied locally with `dotnet ef database update` (or Aspire startup task handles it)

### Build & Test
- [ ] Solution builds without errors: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] New entity endpoints are visible in Scalar/OpenAPI (if enabled)
- [ ] CRUD operations work through Gateway → API → Database

### UI (when applicable)
- [ ] UI service registered in `ConfigureServices`
- [ ] Navigation routes registered for list/detail/create pages
- [ ] Pages render with mock data (if `uiUseMocks: true`)

---

## Bootstrapper DI Registration Snippet

Add these lines to `RegisterServices.cs`:

```csharp
// In RegisterDomainServices or RegisterInfrastructureServices:
services.AddScoped<I{Entity}RepositoryTrxn, {Entity}RepositoryTrxn>();
services.AddScoped<I{Entity}RepositoryQuery, {Entity}RepositoryQuery>();

// In RegisterApplicationServices:
services.AddScoped<I{Entity}Service, {Entity}Service>();
```

---

## Quick Verification

After generating all files:

1. `dotnet build` — should compile with zero errors
2. `dotnet ef migrations add Add{Entity}` — should generate migration
3. `dotnet test --filter "TestCategory=Unit"` — unit tests should pass
4. `dotnet test --filter "TestCategory=Endpoint"` — endpoint tests should pass (may require DB)
