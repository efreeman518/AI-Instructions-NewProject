# Quick Reference — Cheat Sheet

High-signal lookup for naming, structure, dependencies, DI patterns, and common routes/config keys during scaffolding.

---

## Core Project Map

| Project | Namespace | Location |
|---|---|---|
| `{Host}.Api` | `{Host}.Api` | `src/{Host}/{Host}.Api/` |
| `{Gateway}.Gateway` | `{Gateway}.Gateway` | `src/{Gateway}/{Gateway}.Gateway/` |
| `{Host}.Scheduler` | `{Host}.Scheduler` | `src/{Host}/{Host}.Scheduler/` |
| `{Host}.Bootstrapper` | `{Host}.Bootstrapper` | `src/{Host}/{Host}.Bootstrapper/` |
| `{Host}.BackgroundServices` | `{Host}.BackgroundServices` | `src/{Host}/{Host}.BackgroundServices/` |
| `{Host}.UI` | `{Host}.UI` | `src/{Host}/{Host}.UI/` |
| `{App}.FunctionApp` | `{App}.FunctionApp` | `src/Functions/{App}.FunctionApp/` |
| `Domain.*` | `Domain.*` | `src/Domain/` |
| `Application.*` | `Application.*` | `src/Application/` |
| `Infrastructure.*` | `Infrastructure.*` | `src/Infrastructure/` |
| `Aspire.*` | `Aspire.*` | `src/Aspire/` |
| `Test.*` | `Test.*` | `src/Test/` |

---

## File Naming Conventions

| Artifact | Pattern |
|---|---|
| Entity | `{Entity}.cs` |
| EF config | `{Entity}Configuration.cs` |
| Write repo | `{Entity}RepositoryTrxn.cs` |
| Read repo | `{Entity}RepositoryQuery.cs` |
| Repo interface | `I{Entity}RepositoryTrxn.cs` / `I{Entity}RepositoryQuery.cs` |
| Updater | `{Entity}Updater.cs` |
| DTO | `{Entity}Dto.cs` |
| Search filter | `{Entity}SearchFilter.cs` |
| Mapper | `{Entity}Mapper.cs` |
| Service | `{Entity}Service.cs` |
| Service interface | `I{Entity}Service.cs` |
| Endpoint | `{Entity}Endpoints.cs` |
| Message handler | `{Event}Handler.cs` |
| Health check | `{Target}HealthCheck.cs` |
| Settings POCO | `{Entity}ServiceSettings.cs` |
| Dockerfile | `Dockerfile` |

---

## Core Package Roles

| Package | Primary Purpose |
|---|---|
| `EF.Domain` | entities, tenant contracts, domain result primitives |
| `EF.Domain.Contracts` | DTO/domain result contracts |
| `EF.Data` | EF repo/config base abstractions |
| `EF.Common` | `Result`, helpers, predicates |
| `EF.Common.Contracts` | request context, paging/search contracts |
| `EF.BackgroundServices` | internal message bus, handlers, background queue |
| `EF.Host` | host startup/task abstractions |

---

## Base Type Lookup

| Type | Purpose |
|---|---|
| `EntityBase` | common Id + rowversion base entity |
| `ITenantEntity<TTenantId>` | tenant ownership contract |
| `DomainResult<T>` | domain-level success/failure monad |
| `EntityBaseConfiguration<T>` | base EF configuration rules |
| `RepositoryBase<TContext, TAuditId, TTenantId>` | common repository operations |
| `IRequestContext<TAuditId, TTenantId>` | scoped audit/tenant/role context |
| `IInternalMessageBus` | internal publish/subscribe pipeline |
| `IMessageHandler<T>` | event handler contract |

---

## DI Registration Pattern

Keep registrations in `RegisterServices.cs` under Bootstrapper:

```csharp
public static IServiceCollection AddBootstrapper(this IServiceCollection services, IConfiguration config)
{
    services.AddDbContextPool<{App}DbContextTrxn>(/* ... */);
    services.AddDbContextPool<{App}DbContextQuery>(/* ... */);

    services.AddScoped<I{Entity}RepositoryTrxn, {Entity}RepositoryTrxn>();
    services.AddScoped<I{Entity}RepositoryQuery, {Entity}RepositoryQuery>();
    services.AddScoped<I{Entity}Service, {Entity}Service>();

    services.AddSingleton<IInternalMessageBus>(sp =>
    {
        var bus = new InternalMessageBus(sp);
        bus.AutoRegisterHandlers(typeof({Entity}Service).Assembly);
        return bus;
    });

    return services;
}
```

---

## Endpoint Pattern

```csharp
private static void SetupApiVersionedEndpoints(WebApplication app)
{
    var group = app.MapGroup("api/v1").RequireAuthorization();
    group.Map{Entity}Endpoints(problemDetailsIncludeStackTrace);
}
```

Common routes:

| Operation | Method | Route |
|---|---|---|
| Search | `POST` | `/{entities}/search` |
| Get | `GET` | `/{entities}/{id}` |
| Create | `POST` | `/{entities}` |
| Update | `PUT` | `/{entities}` |
| Delete | `DELETE` | `/{entities}/{id}` |

---

## DbContext Reminder

Both contexts define the same `DbSet<{Entity}>`; query context is configured for no-tracking.

---

## Aspire AppHost Pattern

```csharp
var sql = builder.AddSqlServer("sql").AddDatabase("{project}db").WithDataVolume();
var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.{Host}_Api>("{host}api").WithReference(sql).WithReference(redis);
var gateway = builder.AddProject<Projects.{Host}_Gateway>("gateway").WithReference(api).WaitFor(api);
var scheduler = builder.AddProject<Projects.{Host}_Scheduler>("scheduler").WithReference(sql).WithReference(redis).WithReplicas(1);
```

Use underscores in `Projects.{Host}_Api` style identifiers.

---

## Common Config Keys

| Key | Purpose |
|---|---|
| `ConnectionStrings:{App}DbContextTrxn` | SQL write connection |
| `ConnectionStrings:{App}DbContextQuery` | SQL read/query connection |
| `ConnectionStrings:Redis1` | cache/backplane connection |
| `Gateway_EntraExt` | gateway auth config |
| `Api_EntraID` | API auth config |
| `ServiceAuth:{clusterId}` | service-to-service token config |
| `CacheDurations` | cache TTL settings |
| `OpenApiSettings:Enable` | OpenAPI docs toggle |

---

## Canonical References

- Architecture flow and skill order: [SKILL.md](SKILL.md)
- Slice-level generation checklist: [vertical-slice-checklist.md](vertical-slice-checklist.md)
- Prompt/context strategy: [ai-build-optimization.md](ai-build-optimization.md)
- Placeholder semantics: [placeholder-tokens.md](placeholder-tokens.md)