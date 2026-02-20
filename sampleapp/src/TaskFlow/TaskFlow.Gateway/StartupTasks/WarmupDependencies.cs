// ═══════════════════════════════════════════════════════════════
// Pattern: Gateway startup task — pre-warm YARP cluster token cache.
// Runs at application startup BEFORE the first request arrives.
// Iterates all configured ReverseProxy clusters and acquires client
// credential tokens so the first user request doesn't pay the warm-up cost.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Gateway.StartupTasks;

/// <summary>
/// Pattern: IStartupTask — runs sequentially after Build(), before app.Run().
/// Pre-warms the <see cref="TokenService"/> cache by acquiring tokens for each
/// YARP cluster that has ServiceAuth configuration.
/// <para>
/// Config dependency: Reads "ReverseProxy:Clusters" to discover cluster IDs,
/// then calls <see cref="TokenService.GetAccessTokenAsync"/> for each.
/// </para>
/// </summary>
public class WarmupDependencies(
    IConfiguration config,
    TokenService tokenService,
    ILogger<WarmupDependencies> logger) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Gateway warmup: starting token cache pre-warm...");

        // ═══════════════════════════════════════════════════════════════
        // Pattern: Discover YARP clusters from config and pre-warm tokens.
        // Each cluster in ReverseProxy:Clusters may have a matching
        // ServiceAuth:{clusterId} section for client credential auth.
        // ═══════════════════════════════════════════════════════════════

        var clustersSection = config.GetSection("ReverseProxy:Clusters");
        var clusterIds = clustersSection.GetChildren().Select(c => c.Key).ToList();

        if (clusterIds.Count == 0)
        {
            logger.LogWarning("Gateway warmup: no YARP clusters found in configuration");
            return;
        }

        var warmupTasks = new List<Task>();

        foreach (var clusterId in clusterIds)
        {
            // Pattern: Only warm clusters that have ServiceAuth config.
            var authSection = config.GetSection($"ServiceAuth:{clusterId}");
            if (!authSection.Exists())
            {
                logger.LogDebug("Gateway warmup: skipping cluster {ClusterId} — no ServiceAuth config", clusterId);
                continue;
            }

            logger.LogDebug("Gateway warmup: acquiring token for cluster {ClusterId}...", clusterId);

            // Pattern: Fire-and-gather — start all token acquisitions concurrently.
            warmupTasks.Add(WarmClusterTokenAsync(clusterId, cancellationToken));
        }

        if (warmupTasks.Count > 0)
        {
            await Task.WhenAll(warmupTasks);
        }

        logger.LogInformation("Gateway warmup complete. Pre-warmed {Count} cluster tokens.", warmupTasks.Count);
    }

    private async Task WarmClusterTokenAsync(string clusterId, CancellationToken ct)
    {
        try
        {
            var token = await tokenService.GetAccessTokenAsync(clusterId);
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("Gateway warmup: empty token for cluster {ClusterId}", clusterId);
            }
            else
            {
                logger.LogDebug("Gateway warmup: token acquired for cluster {ClusterId}", clusterId);
            }
        }
        catch (Exception ex)
        {
            // Pattern: Warmup failures are non-fatal — log and continue.
            // The first real request will retry token acquisition.
            logger.LogWarning(ex, "Gateway warmup: failed to acquire token for cluster {ClusterId}", clusterId);
        }
    }
}

/// <summary>
/// Pattern: IStartupTask interface — implemented by tasks that run at app startup.
/// Registered in DI, discovered and executed sequentially by the host.
/// </summary>
public interface IStartupTask
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
