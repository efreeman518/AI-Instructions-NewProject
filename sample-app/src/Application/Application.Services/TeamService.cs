namespace Application.Services;

internal class TeamService(
    ILogger<TeamService> logger,
    IRequestContext<string, Guid?> requestContext,
    ITeamRepositoryTrxn repoTrxn,
    ITeamRepositoryQuery repoQuery) : ITeamService
{
    public async Task<PagedResponse<TeamDto>> SearchAsync(SearchRequest<TeamDto> request, CancellationToken ct = default)
    {
        logger.LogDebug("Searching teams");
        return await repoQuery.SearchAsync(request, ct);
    }

    public async Task<Result<TeamDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, includeMembers: true, ct);
        if (entity == null) return Result<TeamDto>.None();
        return Result<TeamDto>.Success(entity.ToDto());
    }

    public async Task<Result<TeamDto>> CreateAsync(TeamDto dto, CancellationToken ct = default)
    {
        var tenantId = dto.TenantId != Guid.Empty ? dto.TenantId : requestContext.TenantId ?? Guid.Empty;
        var entityResult = Team.Create(tenantId, dto.Name, dto.Description);
        if (entityResult.IsFailure) return Result<TeamDto>.Failure(entityResult.ErrorMessage);

        var entity = entityResult.Value!;
        repoTrxn.Create(ref entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<TeamDto>.Success(entity.ToDto());
    }

    public async Task<Result<TeamDto>> UpdateAsync(TeamDto dto, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(dto.Id, false, ct);
        if (entity == null) return Result<TeamDto>.None();

        var updateResult = entity.Update(dto.Name, dto.Description, dto.IsActive);
        if (updateResult.IsFailure) return Result<TeamDto>.Failure(updateResult.ErrorMessage);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<TeamDto>.Success(entity.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, false, ct);
        if (entity == null) return Result.Success();
        repoTrxn.Delete(entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result.Success();
    }

    public async Task<Result<TeamMemberDto>> AddMemberAsync(Guid teamId, TeamMemberDto member, CancellationToken ct = default)
    {
        var team = await repoTrxn.GetAsync(teamId, true, ct);
        if (team == null) return Result<TeamMemberDto>.None();

        var result = team.AddMember(member.UserId, member.DisplayName, member.Role, member.HourlyRate);
        if (result.IsFailure) return Result<TeamMemberDto>.Failure(result.ErrorMessage);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<TeamMemberDto>.Success(result.Value!.ToDto());
    }

    public async Task<Result> RemoveMemberAsync(Guid teamId, Guid memberId, CancellationToken ct = default)
    {
        var team = await repoTrxn.GetAsync(teamId, true, ct);
        if (team == null) return Result.Success();

        team.RemoveMember(memberId);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result.Success();
    }
}
