namespace Application.Contracts.Repositories;

public interface ITeamRepositoryTrxn : IRepositoryBase
{
    Task<Team?> GetAsync(Guid id, bool includeMembers = false, CancellationToken cancellationToken = default);
    void Create(ref Team entity);
}
