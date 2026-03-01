using Microsoft.Extensions.Logging;

namespace Infrastructure.Repositories;

public class MaintenanceRepository(
    TaskFlowDbContextTrxn dbContext,
    ILogger<MaintenanceRepository> logger) : IMaintenanceRepository
{
    public async Task<int> PurgeHistoryAsync(int retentionDays, CancellationToken ct = default)
    {
        logger.LogInformation("Purging TodoItemHistory records older than {RetentionDays} days", retentionDays);

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await dbContext.TodoItemHistories
            .Where(h => h.ChangedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        logger.LogInformation("Purged {DeletedCount} TodoItemHistory records", deleted);
        return deleted;
    }
}
