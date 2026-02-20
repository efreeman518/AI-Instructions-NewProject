// ═══════════════════════════════════════════════════════════════
// Pattern: Custom health check — Scheduler status.
// Reports scheduler configuration as health data.
// Aspire dashboard surfaces this automatically.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskFlow.Scheduler.Infrastructure;

/// <summary>
/// Pattern: IHealthCheck for the scheduler host.
/// Returns scheduler configuration as structured data.
/// The TickerQ DB check is registered separately via AddDbContextCheck.
/// </summary>
public class SchedulerHealthCheck(IConfiguration config) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["persistence"] = config.GetValue<bool>("Scheduling:UsePersistence", true),
            ["dashboard"] = config.GetValue<bool>("Scheduling:EnableDashboard", true),
            ["pollInterval"] = config.GetValue<int>("Scheduling:PollIntervalSeconds", 30),
            ["nodeIdentifier"] = Environment.MachineName
        };

        return Task.FromResult(HealthCheckResult.Healthy("Scheduler is running", data));
    }
}
