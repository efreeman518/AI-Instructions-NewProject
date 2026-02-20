// ═══════════════════════════════════════════════════════════════
// Pattern: Health check — Database connectivity.
// Tests that the SQL Server / Azure SQL database is reachable.
// Registered via AddHealthChecks in RegisterApiServices.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.HealthChecks;

/// <summary>
/// Pattern: Custom IHealthCheck that validates database connectivity.
/// Uses the read-only DbContext to execute a lightweight query.
/// Aspire dashboard surfaces health status automatically.
/// </summary>
public class DatabaseHealthCheck(
    IDbContextFactory<TaskFlowDbContextQuery> contextFactory,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                logger.LogDebug("Database health check passed");
                return HealthCheckResult.Healthy("Database is reachable.");
            }

            logger.LogWarning("Database health check failed — CanConnect returned false");
            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check threw exception");
            return HealthCheckResult.Unhealthy("Database health check failed.", ex);
        }
    }
}
