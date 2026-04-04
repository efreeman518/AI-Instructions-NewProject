# Expected Output File Index

Load on-demand as a reference during Phase 4a–4e to verify scaffolded file layout.

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
| Message handler | `Application/Application.MessageHandlers/TodoItemCreatedEventHandler.cs` |

## API Host
| Artifact | Path |
|---|---|
| Program.cs | `{Host}/{Host}.Api/Program.cs` |
| Endpoints | `{Host}/{Host}.Api/Endpoints/TodoItemEndpoints.cs` |
| RegisterApiServices | `{Host}/{Host}.Api/RegisterApiServices.cs` |
| Bootstrapper | `{Host}/{Host}.Bootstrapper/RegisterServices.cs` |

## Testing
| Artifact | Path |
|---|---|
| Unit (domain) | `Test/Test.Unit/Domain/TodoItemTests.cs` |
| Unit (mapper) | `Test/Test.Unit/Application/TodoItemMapperTests.cs` |
| Integration | `Test/Test.Integration/EndpointContractTests.cs` |
| Architecture | `Test/Test.Architecture/LayerDependencyTests.cs` |
| Test support | `Test/Test.Support/UnitTestBase.cs`, `InMemoryDbBuilder.cs`, `DbSupport.cs` |
| Endpoint tests | `Test/Test.Endpoints/Endpoints/CategoryEndpointsTests.cs` |
| Custom factory | `Test/Test.Integration/CustomApiFactory.cs` |

## Aspire
| Artifact | Path |
|---|---|
| AppHost | `Aspire/AppHost/AppHost.cs` |
| Service defaults | `Aspire/ServiceDefaults/Extensions.cs` |

## Infrastructure
| Artifact | Path |
|---|---|
| Dockerfile (per host) | `{Host}/{Host}.Api/Dockerfile` |
| Health checks | `{Host}/{Host}.Api/HealthChecks/SqlHealthCheck.cs` |
