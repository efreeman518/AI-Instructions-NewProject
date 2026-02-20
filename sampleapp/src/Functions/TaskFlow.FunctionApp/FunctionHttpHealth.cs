// ═══════════════════════════════════════════════════════════════
// Pattern: HTTP Health Check — Anonymous authorization.
// Only health endpoints use AuthorizationLevel.Anonymous.
// Returns HealthCheckResult for monitoring integration.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Anonymous health check endpoint.
/// Rule: AuthorizationLevel.Anonymous ONLY for health checks — never for business endpoints.
/// local: http://localhost:7071/api/FunctionHttpHealth
/// </summary>
public class FunctionHttpHealth(
    ILogger<FunctionHttpHealth> logger,
    IConfiguration configuration,
    IOptions<Settings> settings)
{
    [Function(nameof(FunctionHttpHealth))]
    public async Task<HealthCheckResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        logger.LogInformation("FunctionHttpHealth - Start");
        var status = HealthStatus.Healthy;

        try
        {
            // Pattern: Check dependent services (database, cache, external APIs).
            // var dbHealthy = await dbContext.Database.CanConnectAsync();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            status = HealthStatus.Unhealthy;
            logger.LogError(ex, "FunctionHttpHealth - Error {Status}", status);
        }

        return new HealthCheckResult(status,
            description: $"Function Service is {status}.",
            exception: null,
            data: null);
    }
}
