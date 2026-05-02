# Bootstrapper

> **When to read:** Phase 5b/5c, when wiring shared DI for domain/application/infrastructure services that multiple hosts (API, Function App, Scheduler, integration tests) consume.
> **Skip if:** Single-host scaffold where registration lives directly in the host; pure domain or pure data-access work that does not touch DI.

## Overview

The **Bootstrapper project** is the centralized DI registration hub. It wires up all non-host-specific services (domain, application, infrastructure) so they can be shared across multiple deployable hosts — API, Function App, Scheduler, and integration tests — without duplicating registration code.

## Project Structure

> Reference patterns: [../patterns/api-host-wiring.md](../patterns/api-host-wiring.md) (API Startup), [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md) (DB Wiring).
> Base types (`IStartupTask`, `RunStartupTasks()`): [../support/ef-packages-reference.md](../support/ef-packages-reference.md).
> `StaticLogging`: from `EF.Common` package (used in Program.cs for early logger).

```
Host/{Host}.Bootstrapper/
├── RegisterServices.cs                       # Partial class - public orchestration methods
├── Registration/
│   ├── RegisterServices.Application.cs        # App services, message handlers
│   ├── RegisterServices.Caching.cs            # FusionCache + Redis backplane
│   ├── RegisterServices.Database.cs           # DbContext pooling, repositories
│   ├── RegisterServices.Infrastructure.cs     # Blob storage, service bus, health checks
│   └── RegisterServices.RequestContext.cs     # Scoped IRequestContext factory
├── IStartupTask.cs                           # Startup task interface
├── IHostExtensions.cs                        # Host extension for running startup tasks
└── StartupTasks/
    ├── ApplyEFMigrationsStartup.cs
    └── WarmupDependencies.cs
```

## Registration Pattern

`RegisterServices.cs` is a **partial static class** with extension methods on `IServiceCollection`, organized by layer. Private helper methods live in partial files under `Registration/`:

> See [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md) for full registration pattern.

```csharp
// Compact pattern — see reference app (TaskFlow) for full implementation
public static partial class RegisterServices
{
    public static IServiceCollection RegisterDomainServices(this IServiceCollection services, IConfiguration config)
    {
        // Domain services (if any)
        return services;
    }

    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        AddMessageHandlers(services);
        AddApplicationServices(services, config);
        return services;
    }

    public static IServiceCollection RegisterInfrastructureServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        AddRequestContextServices(services);
        AddCachingServices(services, config);
        AddDatabaseServices(services, config);  // Pooled factories + scoped wrappers
        AddAzureClientServices(services, config);
        AddStartupTasks(services);
        return services;
    }

    public static IServiceCollection RegisterBackgroundServices(this IServiceCollection services, IConfiguration config)
    {
        // Host-specific listeners / cron services go here.
        // The shared channel queue + internal message bus live in AddSupportServices().
        return services;
    }
}
```

### Support Services (Background Task Queue + Internal Message Bus)

`AuditInterceptor` from the EF packages depends on `IInternalMessageBus`, which in turn depends on `IBackgroundTaskQueue`. These must be registered early, before database services:

```csharp
private static IServiceCollection AddSupportServices(this IServiceCollection services)
{
    services.AddChannelBackgroundTaskQueueWithShutdownHandling();
    services.AddSingleton<IInternalMessageBus, InternalMessageBus>();
    return services;
}
```

Call `AddSupportServices()` as the **first** registration in the main chain (before `AddDatabaseServices`). Without it, any host that registers `AuditInterceptor` will fail at startup with a DI resolution error.

`InternalMessageBus` does **not** execute handlers inline. It enqueues work onto `IBackgroundTaskQueue`, which is drained by the channel hosted service. If you skip `AddChannelBackgroundTaskQueueWithShutdownHandling()`, audited saves can succeed while message-handler side effects never execute.

Required usings:
```csharp
using EF.BackgroundServices;
using EF.BackgroundServices.InternalMessageBus;
using EF.BackgroundServices.Work;
```

> **Note:** `AddBackgroundTaskQueue()` and `AddChannelBackgroundTaskQueue()` register different concrete types. If `AuditInterceptor` expects `ChannelBackgroundTaskQueue`, use `AddChannelBackgroundTaskQueueWithShutdownHandling()` — it registers both the queue and the hosted service that drains it on shutdown.
```

## Startup Task Pattern

> See [../patterns/data-layer-wiring.md](../patterns/data-layer-wiring.md).

### Interface

```csharp
public interface IStartupTask
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
```

### Host Extension

```csharp
public static void AutoRegisterMessageHandlers(this IHost host)
{
    var msgBus = host.Services.GetRequiredService<IInternalMessageBus>();
    msgBus.AutoRegisterHandlers(host.Services, typeof(SomeMessageHandler).Assembly);
}

public static async Task RunStartupTasks(this IHost host)
{
    host.AutoRegisterMessageHandlers();
    using var scope = host.Services.CreateScope();
    foreach (var task in scope.ServiceProvider.GetServices<IStartupTask>())
        await task.ExecuteAsync();
}
```

`[ScopedMessageHandler]` marks the handler for scoped resolution during dispatch. It does **not** add the handler to DI. Register each `IMessageHandler<T>` implementation explicitly in `RegisterApplicationServices()`.

### Example Startup Tasks

```csharp
// Apply pending EF migrations
public class ApplyEFMigrationsStartup(IDbContextFactory<{Project}DbContextTrxn> factory) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
}
```

## Usage in Host Projects

> See [../patterns/api-host-wiring.md](../patterns/api-host-wiring.md) (API Startup Sequence).

### API Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services
    .RegisterInfrastructureServices(config)
    .RegisterDomainServices(config)
    .RegisterApplicationServices(config)
    .RegisterBackgroundServices(config)
    .AddApiServices(config, startupLogger);  // API-specific

var app = builder.Build().ConfigurePipeline();
await app.RunStartupTasks();
await app.RunAsync();
```

### Function App Program.cs

```csharp
builder.Services
    .RegisterInfrastructureServices(builder.Configuration)
    .RegisterDomainServices(builder.Configuration)
    .RegisterApplicationServices(builder.Configuration)
    .RegisterBackgroundServices(builder.Configuration);
    // No RegisterApiServices — functions don't need endpoints/auth pipeline

var app = builder.Build();
app.AutoRegisterMessageHandlers();
await app.RunAsync();
```

## Key Principles

1. **One Bootstrapper project** — All non-host-specific DI goes here
2. **Host projects only add host-specific concerns** — API adds endpoints/auth, Functions add triggers, etc.
3. **Extension method chaining** — Each `Register*()` returns `IServiceCollection` for fluent chaining
4. **Private helper methods** — Keep the public surface clean; group related registrations
5. **Configuration-driven** — Settings classes loaded from `IConfiguration` sections; bootstrapper reads config, never owns per-host defaults
6. **Startup tasks run after Build()** — Before `RunAsync()`, migrations applied, caches warmed
7. **Every host must supply its config** — Any key read inside `RegisterInfrastructureServices` (or any shared registration method) must exist in the `appsettings*.json` of every host that calls it: API, Scheduler, and Functions all need the same config keys
8. **Build after DI changes** — Small registration changes (factory lambdas, new `using` imports, constructor signature changes) can break compile. Run a focused host build immediately after any registration edit to catch failures early

---

## Verification

After generating the Bootstrapper, confirm:

- [ ] Single `RegisterServices.cs` (partial class) with public `RegisterInfrastructureServices`, `RegisterDomainServices`, `RegisterApplicationServices`, and `RegisterBackgroundServices` extension methods
- [ ] Private helper methods split into `Registration/RegisterServices.*.cs` partial files
- [ ] DbContexts registered via pooled factory with scoped wrappers
- [ ] All `I{Entity}RepositoryTrxn` and `I{Entity}RepositoryQuery` interfaces registered
- [ ] All `I{Entity}Service` implementations registered
- [ ] All `IMessageHandler<T>` implementations are registered in DI (typically scoped)
- [ ] `AutoRegisterMessageHandlers()` called after `Build()` to bind handler assemblies into `IInternalMessageBus`
- [ ] FusionCache registered with Redis backplane (if caching is enabled)
- [ ] Startup tasks registered as `IStartupTask` (migrations, cache warmup)
- [ ] No host-specific concerns (no endpoints, no triggers, no YARP) — those belong in the host project
- [ ] Cross-references: Every service/repository in [solution-structure.md](solution-structure.md) reference map is registered here

---

**TaskFlow proof (local):** `../AI-Instructions-ReferenceApp/src/Host/TaskFlow.Bootstrapper/RegisterServices.cs` + `../AI-Instructions-ReferenceApp/src/Host/TaskFlow.Bootstrapper/Registration/RegisterServices.*.cs` (Application, Infrastructure, Database, Caching, RequestContext partials)
**TaskFlow proof (remote fallback):** <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/TaskFlow.Bootstrapper>
