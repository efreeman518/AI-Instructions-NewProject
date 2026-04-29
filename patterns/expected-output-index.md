# Expected Output File Index

Load on-demand as a reference during Phase 5a–5e to verify scaffolded file layout.

Expected file layout when scaffolding is complete. All paths relative to project root `src/`.

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
| Unit (domain) | `Test/Test.Unit/Domain/TodoItemTests.cs` |
| Unit (mapper) | `Test/Test.Unit/Application/TodoItemMapperTests.cs` |
| Service-level integration | `Test/Test.Integration/Repositories/CategoryRepositoryIntegrationTests.cs` |
| Architecture | `Test/Test.Architecture/LayerDependencyTests.cs` |
| Test support | `Test/Test.Support/UnitTestBase.cs`, `InMemoryDbBuilder.cs`, `DbSupport.cs` |
| Endpoint contract tests | `Test/Test.Endpoints/Endpoints/CategoryEndpointsTests.cs` |
| Workflow E2E tests | `Test/Test.E2E/Workflows/CategoryWorkflowTests.cs` |
| Custom factory | `Test/Test.Endpoints/CustomApiFactory.cs` (or shared via `Test/Test.Support/`) |
| Playwright UI | `Test/Test.PlaywrightUI/Pages/CategoryCrudTests.cs` (browser; runs against hosted stack) |

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
