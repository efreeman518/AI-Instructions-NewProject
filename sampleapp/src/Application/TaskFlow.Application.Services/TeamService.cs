// Pattern: Service with child entity management — Team + TeamMembers.
// Demonstrates cross-entity rule (TeamDeactivationRule) and child CRUD.

using Microsoft.Extensions.Logging;
using EF.Common;
using EF.Domain;
using EF.Domain.Contracts;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.Team;
using Domain.Model.Rules;
using Domain.Shared;

namespace Application.Services;

internal class TeamService(
    ILogger<TeamService> logger,
    IRequestContext<string, Guid?> requestContext,
    ITeamRepositoryQuery repoQuery,
    ITeamUpdater updater,
    ITodoItemRepositoryQuery todoItemRepo) : ITeamService
{
    private Guid? CallerTenantId => requestContext.TenantId;
    private bool IsGlobalAdmin => requestContext.Roles.Contains(Constants.Roles.GlobalAdmin);

    public async Task<Result<PagedResponse<TeamDto>>> SearchAsync(
        TeamSearchFilter filter, CancellationToken ct = default)
    {
        if (!IsGlobalAdmin) filter.TenantId = CallerTenantId;
        var result = await repoQuery.QueryPageProjectionAsync(filter, ct);
        return Result<PagedResponse<TeamDto>>.Success(result);
    }

    public async Task<Result<TeamDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Pattern: Use ProjectorRoot to include child Members in detail view.
        var dto = await repoQuery.GetWithMembersAsync(id, ct);
        if (dto is null) return Result<TeamDto>.NotFound();
        if (!IsGlobalAdmin && dto.TenantId != CallerTenantId)
            return Result<TeamDto>.Forbidden("Access denied.");
        return Result<TeamDto>.Success(dto);
    }

    public async Task<Result<TeamDto>> CreateAsync(TeamDto dto, CancellationToken ct = default)
    {
        if (!IsGlobalAdmin) dto.TenantId = CallerTenantId ?? dto.TenantId;
        return await updater.CreateAsync(dto, ct);
    }

    public async Task<Result<TeamDto>> UpdateAsync(TeamDto dto, CancellationToken ct = default)
        => await updater.UpdateAsync(dto, ct);

    /// <summary>
    /// Pattern: Domain operation with cross-entity rule — cannot deactivate team
    /// if it still has TodoItems assigned to team members.
    /// </summary>
    public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var activeItemCount = await todoItemRepo.CountActiveItemsForTeamAsync(id, ct);
        var rule = new TeamDeactivationRule(activeItemCount);
        var ruleResult = rule.Evaluate(new Domain.Model.Entities.Team());
        if (!ruleResult.IsSuccess)
            return Result.Failure(ruleResult.Errors);

        var team = await repoQuery.GetWithMembersAsync(id, ct);
        if (team is null) return Result.Success();

        team.IsActive = false;
        var result = await updater.UpdateAsync(team, ct);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Errors);
    }

    /// <summary>Pattern: Child management through parent service.</summary>
    public async Task<Result> AddMemberAsync(
        Guid teamId, TeamMemberDto memberDto, CancellationToken ct = default)
        => await updater.AddMemberAsync(teamId, memberDto, ct);

    public async Task<Result> RemoveMemberAsync(
        Guid teamId, Guid memberId, CancellationToken ct = default)
        => await updater.RemoveMemberAsync(teamId, memberId, ct);
}
