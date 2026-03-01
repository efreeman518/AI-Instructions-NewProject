namespace TaskFlow.UI.Business.Services.Teams;

/// <summary>
/// UI-layer service interface for teams — calls Gateway API.
/// </summary>
public interface ITeamService
{
    ValueTask<IImmutableList<TeamSummary>> GetAll(CancellationToken ct);
    ValueTask<TeamSummary?> GetById(Guid id, CancellationToken ct);
    ValueTask<TeamSummary?> Create(TeamSummary item, CancellationToken ct);
    ValueTask<TeamSummary?> Update(TeamSummary item, CancellationToken ct);
    ValueTask<bool> Delete(Guid id, CancellationToken ct);
}
