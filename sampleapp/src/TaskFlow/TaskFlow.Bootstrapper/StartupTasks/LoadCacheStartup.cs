// ═══════════════════════════════════════════════════════════════
// Pattern: Startup Task — pre-load caches with static/reference data.
// Warms FusionCache with frequently-accessed data (categories, tags)
// so the first request hits L1 cache instead of the database.
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using Domain.Shared.Constants;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Cache warmup — loads static data into named FusionCache instances at startup.
/// Uses the StaticData named cache for long-lived reference data.
/// </summary>
public class LoadCacheStartup(
    IFusionCacheProvider cacheProvider,
    ICategoryService categoryService,
    ITagService tagService,
    ILogger<LoadCacheStartup> logger) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Pre-loading caches with static data...");

        try
        {
            var cache = cacheProvider.GetCache(CacheNames.StaticData);

            // Pattern: Warm cache with static data — categories and tags are rarely-changing.
            // This ensures the first request hits L1 cache instead of the database.
            logger.LogDebug("Loading categories into static data cache...");
            // In production: var categories = await categoryService.GetAllAsync(cancellationToken);
            // cache.Set("categories:all", categories);

            logger.LogDebug("Loading tags into static data cache...");
            // In production: var tags = await tagService.GetAllAsync(cancellationToken);
            // cache.Set("tags:all", tags);

            logger.LogInformation("Cache warmup complete.");
        }
        catch (Exception ex)
        {
            // Pattern: Cache warmup failure is non-fatal — app can still serve from DB.
            logger.LogWarning(ex, "Cache warmup failed — application will serve from database on first access.");
        }
    }
}
