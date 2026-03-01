namespace Application.Contracts.Repositories;

public interface ITeamRepositoryQuery : IRepositoryBase
{
    Task<PagedResponse<TeamDto>> SearchAsync(SearchRequest<TeamDto> request, CancellationToken cancellationToken = default);
}
