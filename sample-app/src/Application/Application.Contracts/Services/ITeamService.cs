namespace Application.Contracts.Services;

public interface ITeamService
{
    Task<PagedResponse<TeamDto>> SearchAsync(SearchRequest<TeamDto> request, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> CreateAsync(TeamDto dto, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> UpdateAsync(TeamDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Member management
    Task<Result<TeamMemberDto>> AddMemberAsync(Guid teamId, TeamMemberDto member, CancellationToken cancellationToken = default);
    Task<Result> RemoveMemberAsync(Guid teamId, Guid memberId, CancellationToken cancellationToken = default);
}
