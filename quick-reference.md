# Quick Reference — Cheat Sheet

A single-page summary of naming conventions, project names, key patterns, and DI registration for fast lookup during scaffolding.

---

## Project Names & Namespaces

| Project | Namespace | Location |
|---------|-----------|----------|
| `{Host}.Api` | `{Host}.Api` | `src/{Host}/{Host}.Api/` |
| `{Host}.Gateway` | `{Host}.Gateway` | `src/{Gateway}/{Gateway}.Gateway/` |
| `{Host}.Scheduler` | `{Host}.Scheduler` | `src/{Host}/{Host}.Scheduler/` |
| `{Host}.Bootstrapper` | `{Host}.Bootstrapper` | `src/{Host}/{Host}.Bootstrapper/` |
| `{Host}.BackgroundServices` | `{Host}.BackgroundServices` | `src/{Host}/{Host}.BackgroundServices/` |
| `{Host}.UI` | `{Host}.UI` | `src/{Host}/{Host}.UI/` |
| `{App}.Domain.Model` | `Domain.Model` | `src/Domain/{App}.Domain.Model/` |
| `{App}.Domain.Shared` | `Domain.Shared` | `src/Domain/{App}.Domain.Shared/` |
| `{App}.Application.Contracts` | `Application.Contracts` | `src/Application/{App}.Application.Contracts/` |
| `{App}.Application.Models` | `Application.Models` | `src/Application/{App}.Application.Models/` |
| `{App}.Application.Services` | `Application.Services` | `src/Application/{App}.Application.Services/` |
| `{App}.Application.MessageHandlers` | `Application.MessageHandlers` | `src/Application/{App}.Application.MessageHandlers/` |
| `{App}.Infrastructure` | `Infrastructure` | `src/Infrastructure/{App}.Infrastructure/` |
| `{App}.Infrastructure.Repositories` | `Infrastructure.Repositories` | `src/Infrastructure/{App}.Infrastructure.Repositories/` |
| `{App}.Infrastructure.Notification` | `Infrastructure.Notification` | `src/Infrastructure/{App}.Infrastructure.Notification/` |
| `{App}.FunctionApp` | `{App}.FunctionApp` | `src/Functions/{App}.FunctionApp/` |
| `Aspire.AppHost` | `Aspire.AppHost` | `src/Aspire/AppHost/` |
| `Aspire.ServiceDefaults` | `Aspire.ServiceDefaults` | `src/Aspire/ServiceDefaults/` |
| `Test.Unit` | `Test.Unit` | `src/Test/Test.Unit/` |
| `Test.Integration` | `Test.Integration` | `src/Test/Test.Integration/` |
| `Test.Architecture` | `Test.Architecture` | `src/Test/Test.Architecture/` |
| `Test.PlaywrightUI` | `Test.PlaywrightUI` | `src/Test/Test.PlaywrightUI/` |
| `Test.Load` | `Test.Load` | `src/Test/Test.Load/` |
| `Test.Benchmarks` | `Test.Benchmarks` | `src/Test/Test.Benchmarks/` |
| `Test.Support` | `Test.Support` | `src/Test/Test.Support/` |

## File Naming Conventions

| Artifact | Pattern | Example |
|----------|---------|---------|
| Entity | `{Entity}.cs` | `Client.cs` |
| Flags enum | `{Entity}Flags.cs` (or `{Entity}Status.cs`) | `ClientFlags.cs` |
| EF Configuration | `{Entity}Configuration.cs` | `ClientConfiguration.cs` |
| Repository (write) | `{Entity}RepositoryTrxn.cs` | `ClientRepositoryTrxn.cs` |
| Repository (read) | `{Entity}RepositoryQuery.cs` | `ClientRepositoryQuery.cs` |
| Repository interface | `I{Entity}RepositoryTrxn.cs` | `IClientRepositoryTrxn.cs` |
| Updater | `{Entity}Updater.cs` | `ClientUpdater.cs` |
| DTO | `{Entity}Dto.cs` | `ClientDto.cs` |
| Search filter | `{Entity}SearchFilter.cs` | `ClientSearchFilter.cs` |
| Mapper | `{Entity}Mapper.cs` | `ClientMapper.cs` |
| Service | `{Entity}Service.cs` | `ClientService.cs` |
| Service interface | `I{Entity}Service.cs` | `IClientService.cs` |
| Endpoint | `{Entity}Endpoints.cs` | `ClientEndpoints.cs` |
| Unit test | `{Entity}ServiceTests.cs` | `ClientServiceTests.cs` |
| Message handler | `{Event}Handler.cs` | `UserCreatedEventHandler.cs` |
| Validation helper | `ValidationHelper.cs` | `ValidationHelper.cs` |
| Structure validator | `StructureValidators.cs` | `StructureValidators.cs` |
| Service error messages | `ServiceErrorMessages.cs` | `ServiceErrorMessages.cs` |
| Health check | `{Target}HealthCheck.cs` | `RedisHealthCheck.cs` |
| Per-service settings | `{Entity}ServiceSettings.cs` | `TodoItemServiceSettings.cs` |
| Application constants | `AppConstants.cs` | `AppConstants.cs` |
| Error constants | `ErrorConstants.cs` | `ErrorConstants.cs` |
| Claims transformer | `GatewayClaimsTransformer.cs` | `GatewayClaimsTransformer.cs` |
| Startup task | `WarmupDependencies.cs` | `WarmupDependencies.cs` |
| Dockerfile | `Dockerfile` | `Dockerfile` |
| MVUX list model | `{Entity}ListModel.cs` | `TodoItemListModel.cs` |
| MVUX detail model | `{Entity}DetailModel.cs` | `TodoItemDetailModel.cs` |
| MVUX create model | `Create{Entity}Model.cs` | `CreateTodoItemModel.cs` |
| UI service | `{Entity}Service.cs` (UI) | `TodoItemService.cs` |
| UI service interface | `I{Entity}Service.cs` (UI) | `ITodoItemService.cs` |
| UI client model | `{Entity}.cs` (UI) | `TodoItem.cs` |
| XAML page | `{Entity}ListPage.xaml` | `TodoItemListPage.xaml` |
| Entity message | `EntityMessage.cs` | `EntityMessage.cs` |

## Private NuGet Packages

| Package | Used By | Purpose |
|---------|---------|---------|
| `EF.Domain` | Domain.Model | `EntityBase`, `DomainResult<T>`, `ITenantEntity<T>` |
| `EF.Domain.Contracts` | Application.Contracts | `IEntityBaseDto`, `DomainError`, `DomainResult<T>` |
| `EF.Data` | Infrastructure | `RepositoryBase<TDbContext,TAuditIdType,TTenantIdType>`, `EntityBaseConfiguration<T>` |
| `EF.Common` | Application.Services | `Result`, `PredicateBuilder`, `ResultExtensions` |
| `EF.Common.Contracts` | Application.Contracts | `IRequestContext`, `PagedResponse<T>`, `SearchRequest<TFilter>`, `Sort` |
| `EF.BackgroundServices` | Application.Services, Bootstrapper | `IInternalMessageBus`, `IMessageHandler<T>`, `IBackgroundTaskQueue` |
| `EF.Host` | API, Gateway, Scheduler | `IStartupTask`, host extensions |

## Key Base Classes & Interfaces

| Type | From | Purpose |
|------|------|---------|
| `EntityBase` | EF.Domain | Base entity with `Guid Id` (`Guid.CreateVersion7()`), `byte[] RowVersion` |
| `ITenantEntity<TTenantIdType>` | EF.Domain | Interface adding `TenantId` with `where TTenantIdType : struct` |
| `DomainResult<T>` | EF.Domain.Contracts | Result monad — `Success(value)` / `Failure(errors)` with `DomainError` |
| `EntityBaseConfiguration<T>` | EF.Data | EF base config — Guid PK (`ValueGeneratedNever`), RowVersion |
| `RepositoryBase<TDbContext, TAuditIdType, TTenantIdType>` | EF.Data | CRUD repository base with `UpsertAsync`, `DeleteAsync` |
| `IEntityBaseDto` | EF.Domain.Contracts | Base DTO interface with `Guid? Id` |
| `IRequestContext<TAuditIdType, TTenantIdType>` | EF.Common.Contracts | Scoped request context (correlationId, auditId, tenantId, roles) |
| `IInternalMessageBus` | EF.BackgroundServices (`EF.BackgroundServices.InternalMessageBus`) | Publish/subscribe for internal events |
| `IMessageHandler<T>` | EF.BackgroundServices (`EF.BackgroundServices.InternalMessageBus`) | Handler interface for internal events |
| `IFusionCacheProvider` | FusionCache | Named cache factory |

## DI Registration Patterns

All registrations go in `{Host}.Bootstrapper/RegisterServices.cs`:

```csharp
public static IServiceCollection AddBootstrapper(this IServiceCollection services, IConfiguration config)
{
    // Databases
    services.AddDbContextPool<{App}DbContextTrxn>(/* ... */);
    services.AddDbContextPool<{App}DbContextQuery>(/* ... */);

    // Repositories
    services.AddScoped<I{Entity}RepositoryTrxn, {Entity}RepositoryTrxn>();
    services.AddScoped<I{Entity}RepositoryQuery, {Entity}RepositoryQuery>();

    // Services
    services.AddScoped<I{Entity}Service, {Entity}Service>();

    // Message handlers (auto-discover all IMessageHandler<T>)
    services.AddSingleton<IInternalMessageBus>(sp =>
    {
        var bus = new InternalMessageBus(sp);
        bus.AutoRegisterHandlers(typeof({Entity}Service).Assembly);
        return bus;
    });

    // Caching
    services.AddFusionCache("{Entity}Cache")
        .WithDefaultEntryOptions(/* ... */)
        .WithDistributedCache(/* Redis */);

    return services;
}
```

## Endpoint Registration

In `{Host}.Api/WebApplicationBuilderExtensions.cs`:

```csharp
private static void SetupApiVersionedEndpoints(WebApplication app)
{
    var group = app.MapGroup("api/v1").RequireAuthorization();
    group.Map{Entity}Endpoints(problemDetailsIncludeStackTrace);
}
```

## DbContext Registration

Both contexts need `DbSet<{Entity}>`:

```csharp
// {App}DbContextTrxn.cs
public DbSet<{Entity}> {Entities} => Set<{Entity}>();

// {App}DbContextQuery.cs (identical DbSet, but with NoTracking behavior)
public DbSet<{Entity}> {Entities} => Set<{Entity}>();
```

## Aspire AppHost Pattern

```csharp
// In AppHost/AppHost.cs
var sql = builder.AddSqlServer("sql").AddDatabase("{project}db").WithDataVolume();
var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.{Host}_Api>("{host}api")
    .WithReference(sql).WithReference(redis);

var gateway = builder.AddProject<Projects.{Host}_Gateway>("gateway")
    .WithReference(api).WaitFor(api);

var scheduler = builder.AddProject<Projects.{Host}_Scheduler>("scheduler")
    .WithReference(sql).WithReference(redis).WithReplicas(1);
```

> **Note:** `Projects.{Host}_Api` uses underscores because C# identifiers can't contain dots. See [placeholder-tokens.md](placeholder-tokens.md) rule 7.

## Common Route Patterns

| Operation | Method | Route | Returns |
|-----------|--------|-------|---------|
| Search | `POST` | `/{entities}/search` | `PagedResponse<{Entity}Dto>` |
| Get by ID | `GET` | `/{entities}/{id}` | `DefaultResponse<{Entity}Dto>` |
| Create | `POST` | `/{entities}` | `DefaultResponse<{Entity}Dto>` |
| Update | `PUT` | `/{entities}` | `DefaultResponse<{Entity}Dto>` |
| Delete | `DELETE` | `/{entities}/{id}` | `Result` |

## Config Section Names

| Section | Used By | Example Key |
|---------|---------|-------------|
| `ConnectionStrings:{App}DbContextTrxn` | EF Core | SQL connection string |
| `ConnectionStrings:{App}DbContextQuery` | EF Core | SQL read replica connection |
| `ConnectionStrings:Redis1` | FusionCache | Redis connection |
| `Gateway_EntraExt` | Gateway auth | Entra External ID config |
| `Api_EntraID` | API auth | Entra ID (service-to-service) |
| `ServiceAuth:{clusterId}` | TokenService | Client credentials per cluster |
| `CacheDurations` | FusionCache | TTL per cache name |
| `OpenApiSettings:Enable` | API | Toggle Scalar/OpenAPI docs |

## Canonical References

- Skill order and architecture principles: [SKILL.md](SKILL.md)
- Vertical-slice generated file checklist: [vertical-slice-checklist.md](vertical-slice-checklist.md)
- AI prompt/iteration strategy: [ai-build-optimization.md](ai-build-optimization.md)
