# Expected Output File Index

Load on-demand as a reference during Phase 5a–5e to verify scaffolded file layout.

Expected file layout when scaffolding is complete. All paths relative to project root `src/`.

> **Scope:** Backend layers below are always emitted. Optional Phase 5c hosts (Blazor, Uno) extend this index — those sections only apply when the corresponding `enabledFeatures` flag is set in `HANDOFF.md` (`includeBlazorUI`, `includeUnoUI`). For host-internal layout details, see [../skills/ui-blazor.md](../skills/ui-blazor.md) and [../skills/ui-uno.md](../skills/ui-uno.md).

## Domain Layer
| Artifact | Path |
|---|---|
| Entity (root) | `Domain/Domain.Model/TodoItem.cs` |
| Entity (child) | `Domain/Domain.Model/Comment.cs` |
| Value object | `Domain/Domain.Model/DateRange.cs` |

## Data Access
| Artifact | Path |
|---|---|
| EF config (entity) | `Infrastructure/Infrastructure.Data/Configurations/TodoItemConfiguration.cs` |
| Write repository | `Infrastructure/Infrastructure.Repositories/TodoItemRepositoryTrxn.cs` |
| Read repository | `Infrastructure/Infrastructure.Repositories/TodoItemRepositoryQuery.cs` |
| Trxn DbContext | `Infrastructure/Infrastructure.Data/{App}DbContextTrxn.cs` |
| Query DbContext | `Infrastructure/Infrastructure.Data/{App}DbContextQuery.cs` |
| Updater | `Infrastructure/Infrastructure.Repositories/TodoItemUpdater.cs` |

## Application Layer
| Artifact | Path |
|---|---|
| Service | `Application/Application.Services/TodoItemService.cs` |
| DTO | `Application/Application.Models/TodoItemDto.cs` |
| Search filter | `Application/Application.Models/TodoItemSearchFilter.cs` |
| Mapper | `Application/Application.Mappers/TodoItemMapper.cs` |
| Contracts | `Application/Application.Contracts/` |
| Error constants | `Application/Application.Contracts/ErrorConstants.cs` |
| DefaultRequest | `Application/Application.Models/DefaultRequest.cs` (record) |
| DefaultResponse | `Application/Application.Models/DefaultResponse.cs` (record) |
| Structure validator | `Application/Application.Services/Rules/{Entity}StructureValidator.cs` |
| Service error messages | `Application/Application.Services/Rules/ServiceErrorMessages.cs` |
| Tenant info DTO | `Application/Application.Models/TenantInfoDto.cs` *(multi-tenant only)* |
| Tenant boundary validator | `Application/Application.Services/TenantBoundaryValidator.cs` *(multi-tenant only)* |
| Tenant boundary interface | `Application/Application.Contracts/ITenantBoundaryValidator.cs` *(multi-tenant only)* |
| Validation helper | `Application/Application.Services/Rules/ValidationHelper.cs` *(multi-tenant only)* |
| Tenant logging extensions | `Application/Application.Services/Rules/TenantBoundaryLoggingExtensions.cs` *(multi-tenant only)* |
| Tenant rules | `Application/Application.Services/Rules/TenantRules.cs` *(multi-tenant only)* |
| Message handler | `Application/Application.MessageHandlers/TodoItemCreatedEventHandler.cs` |

## API Host
| Artifact | Path |
|---|---|
| Program.cs | `Host/{Host}.Api/Program.cs` |
| Endpoints | `Host/{Host}.Api/Endpoints/TodoItemEndpoints.cs` |
| RegisterApiServices | `Host/{Host}.Api/RegisterApiServices.cs` |
| Bootstrapper | `Host/{Host}.Bootstrapper/RegisterServices.cs` |

## Testing
| Artifact | Path |
|---|---|
| Test support — shared WAF base | `Test/Test.Support/WebApplicationFactoryBase.cs` (with `TestDbContextFactory<T>` + `WebApplicationFactoryHelpers`) |
| Test support — JSON options | `Test/Test.Support/JsonTestOptions.cs` |
| Test support — shared constants | `Test/Test.Support/TestConstants.cs`, `LocalSqlSettings.cs` |
| Test support — utilities | `Test/Test.Support/UnitTestBase.cs`, `InMemoryDbBuilder.cs`, `DbSupport.cs` |
| Test support — builders | `Test/Test.Support/Builders/{Entity}Builder.cs`, `{Entity}DtoBuilder.cs` (one of each per entity) |
| Unit (domain) | `Test/Test.Unit/Domain/{Entity}Tests.cs`, `{Entity}RulesTests.cs` |
| Unit (mapper, per entity) | `Test/Test.Unit/Mappers/{Entity}MapperTests.cs` |
| Unit (mapper parity, consolidated) | `Test/Test.Unit/Mappers/MapperProjectionParityTests.cs` |
| Unit (services) | `Test/Test.Unit/Services/{Entity}ServiceTests.cs` |
| Unit (repositories) | `Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs`, `{Entity}RepositoryQueryTests.cs` |
| Endpoint contract tests | `Test/Test.Endpoints/Endpoints/{Entity}EndpointsTests.cs` |
| Endpoint factory | `Test/Test.Endpoints/CustomApiFactory.cs` (derives from `Test.Support/WebApplicationFactoryBase`) |
| E2E factory | `Test/Test.E2E/SqlApiFactory.cs` (Testcontainers SQL, static lifecycle) |
| E2E workflow tests | `Test/Test.E2E/{Entity}WorkflowTests.cs` |
| Integration — Aspire fixture | `Test/Test.Integration/AspireTestHost.cs` (full distributed app + lifecycle) |
| Integration — DB context factory | `Test/Test.Integration/DbContextFactory.cs` (piggyback on `AspireTestHost.ConnectionString`) |
| Integration — repo integration | `Test/Test.Integration/{Entity}RepositoryIntegrationTests.cs` (migrations + CRUD + tenant filter + M:N) |
| Integration — audit pipeline (Azurite) | `Test/Test.Integration/AuditLogRepositoryAzuriteTests.cs` |
| Integration — API audit pipeline | `Test/Test.Integration/ApiAuditPipelineTests.cs` |
| Integration — projection pipeline | `Test/Test.Integration/DomainEventPipelineTests.cs` |
| Architecture | `Test/Test.Architecture/*DependencyTests.cs` |
| Playwright UI | `Test/Test.PlaywrightUI/Pages/{Entity}CrudTests.cs` (browser; runs against hosted stack) |
| Load | `Test/Test.Load/{Entity}LoadTests.cs` |
| Benchmark | `Test/Test.Benchmarks/{Entity}Benchmarks.cs` |

## Aspire
| Artifact | Path |
|---|---|
| AppHost | `Host/Aspire/AppHost/AppHost.cs` |
| Service defaults | `Host/Aspire/ServiceDefaults/Extensions.cs` |

## Infrastructure
| Artifact | Path |
|---|---|
| Dockerfile (per host) | `Host/{Host}.Api/Dockerfile` |
| Health checks | `Host/{Host}.Api/HealthChecks/SqlHealthCheck.cs` |

## Blazor UI (Phase 5c, optional — `includeBlazorUI: true`)

Source: [../skills/ui-blazor.md](../skills/ui-blazor.md). Project root: `Host/{Project}.Blazor/`.

| Artifact | Path |
|---|---|
| Program.cs | `Host/{Project}.Blazor/Program.cs` |
| App root | `Host/{Project}.Blazor/App.razor` |
| Routes | `Host/{Project}.Blazor/Components/Routes.razor` |
| Imports | `Host/{Project}.Blazor/Components/_Imports.razor` |
| Layout | `Host/{Project}.Blazor/Components/Layout/MainLayout.razor` |
| Page (dashboard) | `Host/{Project}.Blazor/Components/Pages/Dashboard.razor` |
| Page (entity list, per entity) | `Host/{Project}.Blazor/Components/Pages/{Entity}List.razor` |
| Page (entity new/edit, per entity) | `Host/{Project}.Blazor/Components/Pages/{Entity}Page.razor` |
| Page (settings, error) | `Host/{Project}.Blazor/Components/Pages/Settings.razor`, `Error.razor` |
| Refit API client | `Host/{Project}.Blazor/Services/I{Project}ApiClient.cs` |
| Scoped state hub | `Host/{Project}.Blazor/Services/FloatService.cs` |
| Static assets | `Host/{Project}.Blazor/wwwroot/app.css` |
| Runtime config (WASM only) | `Host/{Project}.Blazor/wwwroot/appsettings.json` |

## Uno UI (Phase 5c, optional, dedicated session — `includeUnoUI: true`)

Source: [../skills/ui-uno.md](../skills/ui-uno.md), [../skills/ui-uno-shell.md](../skills/ui-uno-shell.md), [../skills/ui-uno-mvux.md](../skills/ui-uno-mvux.md), [../skills/ui-uno-platforms.md](../skills/ui-uno-platforms.md). Project root: `Host/{Project}.UI/`.

| Artifact | Path |
|---|---|
| App entry | `Host/{Project}.UI/App.xaml`, `App.xaml.cs`, `App.xaml.host.cs` |
| App config | `Host/{Project}.UI/appsettings.json` (+ environment variants) |
| Shell | `Host/{Project}.UI/Shell.xaml`, `Shell.xaml.cs`, `ShellModel.cs` |
| Business model (per entity) | `Host/{Project}.UI/Business/Models/{Entity}.cs` |
| Business service (per feature) | `Host/{Project}.UI/Business/Services/{Feature}/I{Entity}Service.cs`, `{Entity}Service.cs` |
| Kiota client (generated) | `Host/{Project}.UI/Client/` |
| MVUX model — list (per entity) | `Host/{Project}.UI/Presentation/{Entity}ListModel.cs` |
| MVUX model — detail (per entity) | `Host/{Project}.UI/Presentation/{Entity}DetailModel.cs` |
| MVUX model — create (per entity) | `Host/{Project}.UI/Presentation/Create{Entity}Model.cs` |
| Page — list (per entity) | `Host/{Project}.UI/Views/{Entity}ListPage.xaml` + `.xaml.cs` |
| Page — detail (per entity) | `Host/{Project}.UI/Views/{Entity}DetailPage.xaml` + `.xaml.cs` |
| Page — create (per entity) | `Host/{Project}.UI/Views/Create{Entity}Page.xaml` + `.xaml.cs` |
| Styles / strings / converters | `Host/{Project}.UI/Styles/`, `Strings/`, `Converters/` |
| WASM platform glue | `Host/{Project}.UI/Platforms/WebAssembly/WasmScripts/AppManifest.js` |
