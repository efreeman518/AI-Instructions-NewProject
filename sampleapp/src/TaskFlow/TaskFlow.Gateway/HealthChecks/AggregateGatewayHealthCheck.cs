// ═══════════════════════════════════════════════════════════════
// Pattern: Aggregate health check — verifies downstream services.
// Calls each downstream API's /alive endpoint.
// Returns Degraded (not Unhealthy) when a downstream is unreachable —
// the gateway itself is still operational.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskFlow.Gateway.HealthChecks;

/// <summary>
/// Pattern: Aggregate downstream health check.
/// Checks /alive endpoint on each downstream service registered in YARP clusters.
/// Uses Aspire service discovery URLs (e.g., "https+http://taskflowapi").
/// </summary>
public class AggregateGatewayHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<AggregateGatewayHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(AggregateGatewayHealthCheck));
        var results = new Dictionary<string, object>();
        var allHealthy = true;

        // Pattern: Check each downstream service's /alive endpoint.
        var downstreamServices = new Dictionary<string, string?>
        {
            ["api"] = config["services:taskflowapi:https:0"]
                      ?? config["ReverseProxy:Clusters:api-cluster:Destinations:api:Address"],
            ["scheduler"] = config["services:taskflowscheduler:https:0"]
                            ?? config["ReverseProxy:Clusters:scheduler-cluster:Destinations:scheduler:Address"]
        };

        foreach (var (name, baseUrl) in downstreamServices)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                results[name] = "Not configured";
                continue;
            }

            try
            {
                var response = await client.GetAsync($"{baseUrl}/alive", cancellationToken);
                results[name] = response.IsSuccessStatusCode ? "Healthy" : $"Unhealthy ({response.StatusCode})";
                if (!response.IsSuccessStatusCode) allHealthy = false;
            }
            catch (Exception ex)
            {
                results[name] = $"Unreachable ({ex.GetType().Name})";
                allHealthy = false;
                logger.LogWarning(ex, "Downstream health check failed for {Service}", name);
            }
        }

        // Pattern: Return Degraded (not Unhealthy) when downstream is down.
        // The gateway itself is still operational — it just can't forward requests.
        return allHealthy
            ? HealthCheckResult.Healthy("All downstream services are reachable", results)
            : HealthCheckResult.Degraded("One or more downstream services are unreachable", data: results);
    }
}
