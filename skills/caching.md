# Caching

## Purpose

Use FusionCache as the application cache abstraction, with Redis as distributed layer and backplane for cross-instance invalidation.

## Architecture

```
Application -> FusionCache (L1 memory) -> Redis (L2 distributed)
                         \-> Redis backplane (invalidation sync)
```

## Non-Negotiables

1. Use `IFusionCacheProvider` (named caches), not a single global cache instance.
2. Configure cache instances from settings (`CacheSettings[]`).
3. Use cache-aside for reads and explicit invalidation on writes.
4. Keep fail-safe enabled for resilience under transient dependency failures.
5. Align Redis connection names with Aspire/bootstrapper config.

---

## Configuration

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
    }
  ]
}
```

`StaticData`-style long-lived caches can be added as separate named instances.

---

## Registration Pattern (Bootstrapper)

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
    IServiceCollection services,
    IConfiguration config,
    CacheSettings settings)
{
    var builder = services.AddFusionCache(settings.Name)
        .WithSystemTextJsonSerializer(new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve
        })
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

    var redisConnStr = config.GetConnectionString(settings.RedisConnectionStringName);
    if (!string.IsNullOrEmpty(redisConnStr))
    {
        builder
            .WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redisConnStr }))
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions { Configuration = redisConnStr }));
    }
}
```

---

## Usage Patterns

### Named cache resolution

```csharp
private readonly IFusionCache _cache = fusionCacheProvider.GetCache(AppConstants.DEFAULT_CACHE);
```

### Cache-aside read

```csharp
public Task<TodoItemDto?> GetCachedAsync(Guid id, CancellationToken ct)
{
    return _cache.GetOrSetAsync(
        $"todoitem:{id}",
        async (ctx, token) => (await repoQuery.GetTodoItemAsync(id, token))?.ToDto(),
        cancellationToken: ct);
}
```

### Invalidate / cache-on-write

```csharp
await _cache.RemoveAsync($"todoitem:{id}", token: ct);
await _cache.SetAsync($"todoitem:{entity.Id}", entity.ToDto(), token: ct);
```

---

## Cache Key Rules

- item keys: `{entity}:{id}`
- list/query keys: `{entity}:list:{filterHash}`
- tenant-aware keys should include tenant scope where applicable

Keep key format deterministic and versionable.

---

## CacheSettings Model

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

---

## Testing Guidance

Mock `IFusionCacheProvider` and `IFusionCache` in test base; verify:

- cache hits return expected value,
- misses call underlying repository once,
- write operations invalidate/update keys.

---

## Verification

- [ ] FusionCache registered with named instances
- [ ] Redis L2 + backplane configured where distributed caching is enabled
- [ ] key patterns are deterministic and tenant-safe
- [ ] reads use `GetOrSetAsync` cache-aside pattern
- [ ] writes invalidate or update relevant keys
- [ ] `IFusionCacheProvider` is used for named cache resolution
- [ ] Aspire Redis resource name aligns with connection string naming
- [ ] tests mock cache provider and validate behavior