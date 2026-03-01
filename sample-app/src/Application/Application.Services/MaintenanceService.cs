namespace Application.Services;

internal class MaintenanceService(
    ILogger<MaintenanceService> logger,
    IMaintenanceRepository maintenanceRepository) : IMaintenanceService
{
    public async Task<Result<int>> PurgeHistoryAsync(int retentionDays = 90, CancellationToken ct = default)
    {
        logger.LogInformation("Purging TodoItemHistory records older than {RetentionDays} days", retentionDays);
        var deletedCount = await maintenanceRepository.PurgeHistoryAsync(retentionDays, ct);
        logger.LogInformation("Purged {DeletedCount} TodoItemHistory records", deletedCount);
        return Result<int>.Success(deletedCount);
    }
}
