namespace Application.Contracts.Repositories;

public interface ITodoItemRepositoryTrxn : IRepositoryBase
{
    Task<TodoItem?> GetAsync(Guid id, bool includeChildren = false, CancellationToken cancellationToken = default);
    void Create(ref TodoItem entity);
}
