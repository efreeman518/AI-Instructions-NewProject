# Quick Reference — Cheat Sheet

High-signal lookup for structure, dependencies, DI patterns, and common routes/config keys during scaffolding.

> **File naming conventions** are in [../ai/placeholder-tokens.md](../ai/placeholder-tokens.md#file-naming-conventions).

---

## Core Project Map

| Project | Namespace | Location |
|---|---|---|
| `{Host}.Api` | `{Host}.Api` | `src/Host/{Host}.Api/` |
| `{Gateway}.Gateway` | `{Gateway}.Gateway` | `src/Host/{Gateway}.Gateway/` |
| `{Host}.Scheduler` | `{Host}.Scheduler` | `src/Host/{Host}.Scheduler/` |
| `{Host}.Bootstrapper` | `{Host}.Bootstrapper` | `src/Host/{Host}.Bootstrapper/` |
| `{Host}.BackgroundServices` | `{Host}.BackgroundServices` | `src/Host/{Host}.BackgroundServices/` |
| `{Host}.UI` | `{Host}.UI` | `src/UI/{Host}.UI/` |
| `{App}.Functions` | `{App}.Functions` | `src/Host/{App}.Functions/` |
| `Domain.*` | `Domain.*` | `src/Domain/` |
| `Application.*` | `Application.*` | `src/Application/` |
| `Infrastructure.*` | `Infrastructure.*` | `src/Infrastructure/` |
| `Aspire.*` | `Aspire.*` | `src/Host/Aspire/` |
| `Test.*` | `Test.*` | `src/Test/` |

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

## Event Taxonomy (Do Not Mix)

| Event Type | Layer | Transport | Naming/Contract |
|---|---|---|---|
| Domain event | `Domain.*` | In-process only | Raised from aggregate invariants |
| Integration event | `Application.Contracts.Events` | Cross-process bus (Service Bus/Event Grid) | Published through `IIntegrationEventPublisher` |

Rules:
- If a message crosses process boundaries, define it in `Application.Contracts.Events`.
- Do not keep bus payload records under `Domain.*.Events`.
- Keep event serializer payloads transport-stable (avoid domain-only navigation/value object coupling).

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

**RequestContext constructor order:** `new RequestContext<string, Guid?>(correlationId, auditId, tenantId, roles)`.

---

## DI + Bus Wiring Pattern

Keep registrations in `RegisterServices.cs` under Bootstrapper:

```csharp
private static IServiceCollection AddSupportServices(this IServiceCollection services)
{
    services.AddChannelBackgroundTaskQueueWithShutdownHandling();
    services.AddSingleton<IInternalMessageBus, InternalMessageBus>();
    return services;
}

private static void AddApplicationServices(IServiceCollection services)
{
    services.AddScoped<I{Entity}Service, {Entity}Service>();
    services.AddScoped<IMessageHandler<{EventName}>, {EventName}Handler>();
    services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
}

public static void AutoRegisterMessageHandlers(this IHost host)
{
    var msgBus = host.Services.GetRequiredService<IInternalMessageBus>();
    msgBus.AutoRegisterHandlers(host.Services, typeof({EventName}Handler).Assembly);
}
```

Call `AutoRegisterMessageHandlers()` after `Build()`. `AuditInterceptor` publishes through `IInternalMessageBus`, and that bus dispatches through the channel background queue rather than inline.
```

---

## Endpoint Pattern

```csharp
private static void SetupApiVersionedEndpoints(WebApplication app)
{
    var group = app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/{entity}")
        .RequireAuthorization("TenantMatch");
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