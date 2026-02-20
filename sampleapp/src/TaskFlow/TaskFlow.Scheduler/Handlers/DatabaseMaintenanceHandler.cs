// ═══════════════════════════════════════════════════════════════
// Pattern: Scheduled job handler — DatabaseMaintenance.
// Housekeeping: archive old history, clean soft-deleted records,
// purge stale attachments. Runs off-peak (Sunday 2 AM UTC).
// ═══════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.Scheduler.Abstractions;

namespace TaskFlow.Scheduler.Handlers;

/// <summary>
/// Pattern: Maintenance handler — database housekeeping.
/// Uses the write DbContext directly for bulk operations.
/// Idempotent: running twice deletes nothing extra (age-based cutoff).
/// </summary>
public class DatabaseMaintenanceHandler(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DatabaseMaintenanceHandler> logger) : IScheduledJobHandler
{
    public string JobName => "DatabaseMaintenance";

    /// <summary>
    /// Cleanup old TodoItemHistory records older than 90 days.
    /// Pattern: Bulk delete via ExecuteDeleteAsync — no entity tracking overhead.
    /// </summary>
    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting database maintenance — JobId: {JobId}", context.JobId);

        using var scope = serviceScopeFactory.CreateScope();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<TaskFlowDbContextTrxn>>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Pattern: Bulk delete old history records — ExecuteDeleteAsync (EF 7+).
        // No need to load entities into memory — translates to a single DELETE statement.
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-90);
        var deletedHistoryCount = await dbContext.Set<Domain.Model.Entities.TodoItemHistory>()
            .Where(h => h.CreatedDate < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "Database maintenance complete — Deleted {HistoryCount} old history records, JobId: {JobId}",
            deletedHistoryCount, context.JobId);
    }
}
