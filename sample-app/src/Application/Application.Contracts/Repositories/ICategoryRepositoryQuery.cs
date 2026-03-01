namespace Application.Contracts.Repositories;

public interface ICategoryRepositoryQuery : IRepositoryBase
{
    Task<PagedResponse<CategoryDto>> SearchAsync(SearchRequest<CategoryDto> request, CancellationToken cancellationToken = default);
}
