// ═══════════════════════════════════════════════════════════════
// Pattern: Concrete TickerQ Job — Database maintenance.
// Thin adapter — delegates all business logic to DatabaseMaintenanceHandler.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.Scheduler.Handlers;
using TaskFlow.Scheduler.Telemetry;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Enums;

namespace TaskFlow.Scheduler.Jobs;

/// <summary>
/// Pattern: Maintenance job — lower priority, runs off-peak hours.
/// Database cleanup, stale record archival, index maintenance.
/// </summary>
public class MaintenanceJobs(
    IServiceScopeFactory serviceScopeFactory,
    SchedulingMetrics metrics,
    ILogger<MaintenanceJobs> logger) : BaseTickerQJob(serviceScopeFactory, metrics, logger)
{
    /// <summary>
    /// Database maintenance — runs every Sunday at 2 AM UTC.
    /// Pattern: Normal priority — housekeeping runs during low-traffic window.
    /// Cron: 0 0 2 * * 0 = second 0, minute 0, hour 2, any day, any month, Sunday.
    /// </summary>
    [TickerFunction("DatabaseMaintenance", "0 0 2 * * 0", TickerTaskPriority.Normal)]
    public async Task DatabaseMaintenanceAsync(
        TickerFunctionContext context,
        CancellationToken cancellationToken)
    {
        await ExecuteJobAsync<DatabaseMaintenanceHandler>("DatabaseMaintenance", context, cancellationToken);
    }
}
