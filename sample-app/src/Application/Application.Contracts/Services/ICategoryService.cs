namespace Application.Contracts.Services;

public interface ICategoryService
{
    Task<PagedResponse<CategoryDto>> SearchAsync(SearchRequest<CategoryDto> request, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateAsync(CategoryDto dto, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> UpdateAsync(CategoryDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
