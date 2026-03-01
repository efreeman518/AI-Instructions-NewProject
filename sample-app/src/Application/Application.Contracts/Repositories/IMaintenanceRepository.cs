namespace Application.Contracts.Repositories;

public interface IMaintenanceRepository
{
    Task<int> PurgeHistoryAsync(int retentionDays, CancellationToken cancellationToken = default);
}
