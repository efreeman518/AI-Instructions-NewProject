// ═══════════════════════════════════════════════════════════════
// Pattern: Health check — Redis connectivity.
// Verifies L2 cache (Redis) is reachable by sending a PING command.
// Registered via AddHealthChecks in RegisterApiServices.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace TaskFlow.Api.HealthChecks;

/// <summary>
/// Pattern: Custom IHealthCheck for Redis connectivity.
/// Uses <see cref="IConnectionMultiplexer"/> (registered by FusionCache Redis setup)
/// to send a lightweight PING to the Redis server.
/// Returns Degraded (not Unhealthy) because the app can still function with L1 cache only.
/// </summary>
public class RedisHealthCheck(
    IConnectionMultiplexer redis,
    ILogger<RedisHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var latency = await db.PingAsync();

            if (latency < TimeSpan.FromSeconds(2))
            {
                logger.LogDebug("Redis health check passed. Latency={Latency}ms", latency.TotalMilliseconds);
                return HealthCheckResult.Healthy($"Redis is reachable. Latency: {latency.TotalMilliseconds:F1}ms");
            }

            logger.LogWarning("Redis health check: high latency {Latency}ms", latency.TotalMilliseconds);
            return HealthCheckResult.Degraded($"Redis is reachable but slow. Latency: {latency.TotalMilliseconds:F1}ms");
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "Redis health check failed — connection error");
            return HealthCheckResult.Unhealthy("Redis is not reachable.", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis health check threw unexpected exception");
            return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
        }
    }
}
