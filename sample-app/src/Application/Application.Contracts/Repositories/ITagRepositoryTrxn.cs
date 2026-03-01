namespace Application.Contracts.Repositories;

public interface ITagRepositoryTrxn : IRepositoryBase
{
    Task<Tag?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    void Create(ref Tag entity);
}
