namespace Application.Contracts.Services;

public interface ITagService
{
    Task<PagedResponse<TagDto>> SearchAsync(SearchRequest<TagDto> request, CancellationToken cancellationToken = default);
    Task<Result<TagDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TagDto>> CreateAsync(TagDto dto, CancellationToken cancellationToken = default);
    Task<Result<TagDto>> UpdateAsync(TagDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
