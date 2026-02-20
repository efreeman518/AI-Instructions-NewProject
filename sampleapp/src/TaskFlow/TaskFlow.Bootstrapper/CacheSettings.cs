// ═══════════════════════════════════════════════════════════════
// Pattern: CacheSettings POCO — bound from "CacheSettings" array in appsettings.json.
// Each entry configures a named FusionCache instance (Default, StaticData, etc.).
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Cache configuration per named cache — supports L1 memory + L2 Redis + backplane.
/// Bound from appsettings "CacheSettings" array:
/// <code>
/// "CacheSettings": [
///   { "Name": "Default", "DurationMinutes": 30, "DistributedCacheDurationMinutes": 60, ... },
///   { "Name": "StaticData", "DurationMinutes": 1440, ... }
/// ]
/// </code>
/// </summary>
public class CacheSettings
{
    /// <summary>Named cache identifier — matches CacheNames constants (e.g., "Default", "StaticData").</summary>
    public string Name { get; set; } = "Default";

    /// <summary>L1 memory cache duration in minutes.</summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>L2 distributed (Redis) cache duration in minutes.</summary>
    public int DistributedCacheDurationMinutes { get; set; } = 60;

    /// <summary>Max stale data duration for fail-safe in minutes.</summary>
    public int FailSafeMaxDurationMinutes { get; set; } = 120;

    /// <summary>Throttle duration for fail-safe retries in seconds.</summary>
    public int FailSafeThrottleDurationSeconds { get; set; } = 1;

    /// <summary>Connection string name for Redis (resolved from ConnectionStrings section).</summary>
    public string? RedisConnectionStringName { get; set; } = "Redis1";

    /// <summary>Redis backplane channel name for cache sync across instances.</summary>
    public string? BackplaneChannelName { get; set; } = "cache-sync";
}
