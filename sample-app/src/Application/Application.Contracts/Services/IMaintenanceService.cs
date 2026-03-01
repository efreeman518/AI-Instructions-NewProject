namespace Application.Contracts.Services;

public interface IMaintenanceService
{
    /// <summary>
    /// Purges TodoItemHistory records older than the retention period.
    /// </summary>
    Task<Result<int>> PurgeHistoryAsync(int retentionDays = 90, CancellationToken cancellationToken = default);
}
