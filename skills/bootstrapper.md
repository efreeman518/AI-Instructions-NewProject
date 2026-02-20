# Bootstrapper

## Overview

The **Bootstrapper project** is the centralized DI registration hub. It wires up all non-host-specific services (domain, application, infrastructure) so they can be shared across multiple deployable hosts — API, Function App, Scheduler, and integration tests — without duplicating registration code.

## Project Structure

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Bootstrapper/`

```
{Host}.Bootstrapper/
├── RegisterServices.cs        # Main static extension methods
├── IStartupTask.cs            # Startup task interface
└── IHostExtensions.cs         # Host extension for running startup tasks
```

## Registration Pattern

`RegisterServices.cs` is a **static class** with extension methods on `IServiceCollection`, organized by layer:

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Bootstrapper/RegisterServices.cs` for the full registration pattern including DbContext pooling, repository registration, service registration with per-service settings, caching, message handlers, and startup tasks.

```csharp
// Compact pattern — see sampleapp for full implementation
public static class RegisterServices
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
        services.AddChannelBackgroundTaskQueue();
        return services;
    }
}
```

## Startup Task Pattern

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Bootstrapper/IStartupTask.cs` and `sampleapp/src/TaskFlow/TaskFlow.Bootstrapper/IHostExtensions.cs`

### Interface

```csharp
public interface IStartupTask
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
```

### Host Extension

```csharp
public static async Task RunStartupTasks(this IHost host)
{
    var msgBus = host.Services.GetRequiredService<IInternalMessageBus>();
    msgBus.AutoRegisterHandlers();
    using var scope = host.Services.CreateScope();
    foreach (var task in scope.ServiceProvider.GetServices<IStartupTask>())
        await task.ExecuteAsync();
}
```

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

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Api/Program.cs` (API), `sampleapp/src/Functions/TaskFlow.FunctionApp/Program.cs` (Functions)

### API Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services
    .RegisterInfrastructureServices(config)
    .RegisterDomainServices(config)
    .RegisterApplicationServices(config)
    .RegisterBackgroundServices(config)
    .RegisterApiServices(config, startupLogger);  // API-specific

var app = builder.Build().ConfigurePipeline();
await app.RunStartupTasks();
await app.RunAsync();
```

### Function App Program.cs

```csharp
builder.Services
    .RegisterInfrastructureServices(builder.Configuration)
    .RegisterDomainServices(builder.Configuration)
    .RegisterApplicationServices(builder.Configuration);
    // No RegisterApiServices — functions don't need endpoints/auth pipeline
```

## Key Principles

1. **One Bootstrapper project** — All non-host-specific DI goes here
2. **Host projects only add host-specific concerns** — API adds endpoints/auth, Functions add triggers, etc.
3. **Extension method chaining** — Each `Register*()` returns `IServiceCollection` for fluent chaining
4. **Private helper methods** — Keep the public surface clean; group related registrations
5. **Configuration-driven** — Settings classes loaded from `IConfiguration` sections
6. **Startup tasks run after Build()** — Before `RunAsync()`, migrations applied, caches warmed

---

## Verification

After generating the Bootstrapper, confirm:

- [ ] Single `RegisterServices.cs` with public `AddBootstrapper(this IServiceCollection, IConfiguration)` extension method
- [ ] DbContexts registered via pooled factory with scoped wrappers
- [ ] All `I{Entity}RepositoryTrxn` and `I{Entity}RepositoryQuery` interfaces registered
- [ ] All `I{Entity}Service` implementations registered
- [ ] `IInternalMessageBus.AutoRegisterHandlers()` called to pick up all `IMessageHandler<T>` types
- [ ] FusionCache registered with Redis backplane (if caching is enabled)
- [ ] Startup tasks registered as `IStartupTask` (migrations, cache warmup)
- [ ] No host-specific concerns (no endpoints, no triggers, no YARP) — those belong in the host project
- [ ] Cross-references: Every service/repository in [solution-structure.md](solution-structure.md) reference map is registered here
