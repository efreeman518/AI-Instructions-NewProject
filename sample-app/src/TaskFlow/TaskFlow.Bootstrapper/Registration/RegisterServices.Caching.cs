using Azure.Identity;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EF.Cache;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddCachingServices(IServiceCollection services, IConfiguration config)
    {
        List<CacheSettings> cacheSettings = [];
        config.GetSection("CacheSettings").Bind(cacheSettings);
        foreach (var cacheSettingsInstance in cacheSettings)
        {
            ConfigureFusionCacheInstance(services, config, cacheSettingsInstance);
        }
    }

    private static void ConfigureFusionCacheInstance(IServiceCollection services, IConfiguration config, CacheSettings cacheSettingsInstance)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve,
        };

        var fcBuilder = services.AddFusionCache(cacheSettingsInstance.Name)
            .WithSystemTextJsonSerializer(jsonOptions)
            .WithCacheKeyPrefix($"{cacheSettingsInstance.Name}:")
            .WithDefaultEntryOptions(new FusionCacheEntryOptions()
            {
                Duration = TimeSpan.FromMinutes(cacheSettingsInstance.DurationMinutes),
                DistributedCacheDuration = TimeSpan.FromMinutes(cacheSettingsInstance.DistributedCacheDurationMinutes),
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromMinutes(cacheSettingsInstance.FailSafeMaxDurationMinutes),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(cacheSettingsInstance.FailSafeThrottleDurationMinutes),
                JitterMaxDuration = TimeSpan.FromSeconds(10),
                FactorySoftTimeout = TimeSpan.FromSeconds(1),
                FactoryHardTimeout = TimeSpan.FromSeconds(30),
                EagerRefreshThreshold = 0.9f
            });

        ConfigureFusionCacheRedis(fcBuilder, config, cacheSettingsInstance);
    }

    private static void ConfigureFusionCacheRedis(IFusionCacheBuilder fcBuilder, IConfiguration config, CacheSettings cacheSettingsInstance)
    {
        if (!string.IsNullOrEmpty(cacheSettingsInstance.RedisConnectionStringName))
        {
            var redisConnectionString = config.GetConnectionString(cacheSettingsInstance.RedisConnectionStringName);
            fcBuilder
                .WithDistributedCache(new RedisCache(new RedisCacheOptions()
                {
                    Configuration = redisConnectionString
                }))
                .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
                {
                    Configuration = redisConnectionString
                }));
        }
    }
}
