namespace Application.Contracts.Repositories;

public interface ICategoryRepositoryTrxn : IRepositoryBase
{
    Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveItemsAsync(Guid categoryId, CancellationToken cancellationToken = default);
    void Create(ref Category entity);
}
