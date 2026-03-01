namespace Application.Contracts.Repositories;

public interface ITodoItemRepositoryQuery : IRepositoryBase
{
    Task<PagedResponse<TodoItemDto>> SearchAsync(SearchRequest<TodoItemSearchFilter> request, CancellationToken cancellationToken = default);
}
