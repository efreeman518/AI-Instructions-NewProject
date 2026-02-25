# Vertical Slice — Generated Files Checklist

Use this checklist after generating a new entity slice to ensure no required files or wiring steps were missed.

Placeholder tokens: [placeholder-tokens.md](placeholder-tokens.md).

---

## Backend Slice (Required)

For entity `{Entity}`:

| Layer | File Path | Template | Required |
|---|---|---|---|
| Domain | `src/Domain/{Project}.Domain.Model/Entities/{Entity}.cs` | [entity-template.md](templates/entity-template.md) | yes |
| Domain (optional) | `src/Domain/{Project}.Domain.Model/Enums/{Entity}Status.cs` | — | if flags/status enum used |
| Data | `src/Infrastructure/{Project}.Infrastructure.Data/EntityConfigurations/{Entity}Configuration.cs` | [ef-configuration-template.md](templates/ef-configuration-template.md) | yes |
| Data | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryTrxn.cs` | [repository-template.md](templates/repository-template.md) | yes |
| Data | `src/Infrastructure/{Project}.Infrastructure.Repositories/{Entity}RepositoryQuery.cs` | [repository-template.md](templates/repository-template.md) | yes |
| Data (optional) | `src/Infrastructure/{Project}.Infrastructure.Repositories/Updaters/{Entity}Updater.cs` | [updater-template.md](templates/updater-template.md) | if child collections |
| App | `src/Application/{Project}.Application.Contracts/DTOs/{Entity}Dto.cs` | [dto-template.md](templates/dto-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/DTOs/{Entity}SearchFilter.cs` | [dto-template.md](templates/dto-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Interfaces/I{Entity}Service.cs` | [service-template.md](templates/service-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Interfaces/I{Entity}RepositoryTrxn.cs` | [repository-template.md](templates/repository-template.md) | yes |
| App | `src/Application/{Project}.Application.Contracts/Interfaces/I{Entity}RepositoryQuery.cs` | [repository-template.md](templates/repository-template.md) | yes |
| App | `src/Application/{Project}.Application.Services/Mappers/{Entity}Mapper.cs` | [mapper-template.md](templates/mapper-template.md) | yes |
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