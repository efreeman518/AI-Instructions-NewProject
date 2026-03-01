namespace Application.Contracts.Repositories;

public interface ITagRepositoryQuery : IRepositoryBase
{
    Task<PagedResponse<TagDto>> SearchAsync(SearchRequest<TagDto> request, CancellationToken cancellationToken = default);
}
