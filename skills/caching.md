# Caching

## Overview

Caching uses **FusionCache** as the primary abstraction with Redis as an L2 distributed cache and backplane for cache invalidation across instances. Named cache instances allow different TTLs and behaviors per domain concern.

## Architecture

```
Application Code → FusionCache (L1 Memory) → Redis (L2 Distributed) → Database
                                            ↕ Redis Backplane (invalidation)
```

- **L1 (Memory)** — In-process, fastest, per-instance
- **L2 (Redis)** — Distributed, shared across instances
- **Backplane** — Redis pub/sub for cross-instance L1 invalidation
- **Fail-safe** — Returns stale data if factory/Redis fails

## Configuration

### appsettings.json

```json
{
  "CacheSettings": [
    {
      "Name": "Default",
      "DurationMinutes": 30,
      "DistributedCacheDurationMinutes": 60,
      "FailSafeMaxDurationMinutes": 120,
      "FailSafeThrottleDurationSeconds": 1,
      "RedisConnectionStringName": "Redis1",
      "BackplaneChannelName": "cache-sync"
    },
    {
      "Name": "StaticData",
      "DurationMinutes": 1440,
      "DistributedCacheDurationMinutes": 2880,
      "FailSafeMaxDurationMinutes": 4320,
      "FailSafeThrottleDurationSeconds": 5,
      "RedisConnectionStringName": "Redis1",
      "BackplaneChannelName": "cache-sync-static"
    }
  ]
}
```

## Registration (in Bootstrapper)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Bootstrapper/RegisterServices.cs` for cache service registration in the DI container, and `sampleapp/src/Application/TaskFlow.Application.Contracts/Constants/AppConstants.cs` for cache-related constants.

```csharp
private static void AddCachingServices(IServiceCollection services, IConfiguration config)
{
    List<CacheSettings> cacheSettings = [];
    config.GetSection("CacheSettings").Bind(cacheSettings);

    foreach (var settings in cacheSettings)
    {
        ConfigureFusionCacheInstance(services, config, settings);
    }
}

private static void ConfigureFusionCacheInstance(
    IServiceCollection services, IConfiguration config, CacheSettings settings)
{
    var jsonOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };

    var builder = services.AddFusionCache(settings.Name)
        .WithSystemTextJsonSerializer(jsonOptions)
        .WithCacheKeyPrefix($"{settings.Name}:")
        .WithDefaultEntryOptions(new FusionCacheEntryOptions
        {
            Duration = TimeSpan.FromMinutes(settings.DurationMinutes),
            DistributedCacheDuration = TimeSpan.FromMinutes(settings.DistributedCacheDurationMinutes),
            IsFailSafeEnabled = true,
            FailSafeMaxDuration = TimeSpan.FromMinutes(settings.FailSafeMaxDurationMinutes),
            FailSafeThrottleDuration = TimeSpan.FromSeconds(settings.FailSafeThrottleDurationSeconds),
            JitterMaxDuration = TimeSpan.FromSeconds(10),
            FactorySoftTimeout = TimeSpan.FromSeconds(1),
            FactoryHardTimeout = TimeSpan.FromSeconds(30),
            EagerRefreshThreshold = 0.9f
        });

    // Add Redis L2 + backplane
    var redisConnStr = config.GetConnectionString(settings.RedisConnectionStringName);
    if (!string.IsNullOrEmpty(redisConnStr))
    {
        builder
            .WithDistributedCache(new RedisCache(new RedisCacheOptions
            {
                Configuration = redisConnStr
            }))
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                Configuration = redisConnStr
            }));
    }
}
```

## Usage in Services

### Getting a Named Cache

```csharp
public class TodoItemService(
    IFusionCacheProvider fusionCacheProvider,
    // ... other deps
) : ITodoItemService
{
    private readonly IFusionCache _cache = fusionCacheProvider.GetCache(AppConstants.DEFAULT_CACHE);
}
```

### Cache-Aside Pattern

```csharp
public async Task<TodoItemDto?> GetCachedAsync(Guid id, CancellationToken ct)
{
    return await _cache.GetOrSetAsync(
        $"todoitem:{id}",
        async (ctx, ct) =>
        {
            var entity = await repoQuery.GetTodoItemAsync(id, ct);
            return entity?.ToDto();
        },
        cancellationToken: ct);
}
```

### Cache Invalidation

```csharp
public async Task InvalidateTodoItemCacheAsync(Guid id, CancellationToken ct)
{
    await _cache.RemoveAsync($"todoitem:{id}", token: ct);
}
```

### Cache on Write

```csharp
// After create/update, set the cache proactively
await _cache.SetAsync($"todoitem:{entity.Id}", entity.ToDto(), token: ct);
```

## Entity Cache Provider

A centralized cache provider for commonly accessed entities (tenants, lookup data). This pattern is documented here as a reference for implementation — adapt to your domain entities.

> **Pattern context:** The sampleapp demonstrates cache registration and constants; use this section as the template for building a full `EntityCacheProvider`.

```csharp
namespace Application.Services;

public class EntityCacheProvider(
    IFusionCacheProvider fusionCacheProvider,
    IGenericRepositoryQuery repoQuery) : IEntityCacheProvider
{
    private readonly IFusionCache _cache = fusionCacheProvider.GetCache(AppConstants.DEFAULT_CACHE);

    public async Task<TenantInfoDto?> GetTenantInfoAsync(Guid tenantId, CancellationToken ct)
    {
        return await _cache.GetOrSetAsync(
            $"tenant-info:{tenantId}",
            async (ctx, ct) =>
            {
                var tenant = await repoQuery.GetEntityAsync<Tenant>(
                    filter: t => t.Id == tenantId, cancellationToken: ct);
                return tenant != null ? new TenantInfoDto { Id = tenant.Id, Name = tenant.Name } : null;
            },
            cancellationToken: ct);
    }

    public async Task<T?> GetOrSetEntityAsync<T>(Guid[] ids, CancellationToken ct) where T : EntityBase
    {
        var key = $"{typeof(T).Name}:{string.Join("-", ids)}";
        return await _cache.GetOrSetAsync(
            key,
            async (ctx, ct) => await repoQuery.GetEntityAsync<T>(
                filter: e => ids.Contains(e.Id), cancellationToken: ct),
            cancellationToken: ct);
    }

    public async Task WarmupAsync(CancellationToken ct)
    {
        // Pre-load frequently accessed data during startup
        // Called by LoadCacheStartup task
    }
}
```

## Cache Settings Model

```csharp
public class CacheSettings
{
    public string Name { get; set; } = "Default";
    public int DurationMinutes { get; set; } = 30;
    public int DistributedCacheDurationMinutes { get; set; } = 60;
    public int FailSafeMaxDurationMinutes { get; set; } = 120;
    public int FailSafeThrottleDurationSeconds { get; set; } = 1;
    public string? RedisConnectionStringName { get; set; }
    public string? RedisConfigurationSection { get; set; }
    public string? BackplaneChannelName { get; set; }
}
```

## Key Concepts

| Feature | Purpose |
|---------|---------|
| **Fail-safe** | Returns stale cache value if factory fails (DB down, timeout) |
| **Eager refresh** | Refreshes cache proactively when near expiration (0.9f = 90%) |
| **Factory soft/hard timeout** | Soft = return stale after 1s; Hard = absolute max 30s |
| **Jitter** | Random variation in TTL to prevent thundering herd |
| **Backplane** | Redis pub/sub ensures L1 cache is invalidated across all instances |
| **Named caches** | Different TTLs for different data types (transactional vs static) |

## Testing

In unit tests, mock `IFusionCacheProvider`:

```csharp
protected readonly Mock<IFusionCacheProvider> FusionCacheProviderMock = new();
protected readonly Mock<IFusionCache> FusionCacheMock = new();

protected TestBase()
{
    FusionCacheProviderMock.Setup(p => p.GetCache(It.IsAny<string>()))
        .Returns(FusionCacheMock.Object);
}
```

---

## Verification

After generating caching code, confirm:

- [ ] FusionCache registered in Bootstrapper with Redis backplane (L1 memory + L2 Redis)
- [ ] Cache keys follow pattern: `{entity}:{id}` or `{entity}:list:{filterHash}`
- [ ] Repository query methods use `GetOrSetAsync` with explicit duration and fail-safe
- [ ] Write operations (create/update/delete) invalidate related cache entries
- [ ] `IFusionCacheProvider` used (not `IFusionCache` directly) to support named caches
- [ ] Aspire AppHost registers Redis with `AddRedis()` (not a connection string)
- [ ] Tests mock `IFusionCacheProvider` + `IFusionCache` in `TestBase`
- [ ] Cross-references: Redis connection matches [aspire.md](aspire.md) resource name, cache registration in [bootstrapper.md](bootstrapper.md)
