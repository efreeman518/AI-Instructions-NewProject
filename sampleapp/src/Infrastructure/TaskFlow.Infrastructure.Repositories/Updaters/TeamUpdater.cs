// ═══════════════════════════════════════════════════════════════
// Pattern: Parent entity updater with child collection sync — Team + TeamMembers.
// Demonstrates: CollectionUtility.SyncCollectionWithResult for child entities,
// child Create/Update/Remove through parent aggregate methods,
// RelatedDeleteBehavior for cascade control.
// ═══════════════════════════════════════════════════════════════

using Application.Models.Team;
using Domain.Model.Entities;
using Domain.Model.Enums;
using Package.Infrastructure.Common;
using Package.Infrastructure.Domain;

namespace Infrastructure.Repositories.Updaters;

/// <summary>
/// Pattern: Updater with child sync — Team manages TeamMember children.
/// Uses CollectionUtility for the diff algorithm; delegates to entity domain methods.
/// </summary>
internal static class TeamUpdater
{
    /// <summary>
    /// Pattern: UpdateFromDto with child collection sync.
    /// 1. Updates Team entity properties via entity.Update().
    /// 2. Syncs TeamMembers collection — create new, update existing, remove missing.
    /// 3. Returns aggregated DomainResult with combined errors.
    /// </summary>
    public static DomainResult<Team> UpdateFromDto(
        this TaskFlowDbContextTrxn db,
        Team entity,
        TeamDto dto,
        RelatedDeleteBehavior relatedDeleteBehavior = RelatedDeleteBehavior.Delete)
    {
        // ── Step 1: Update parent entity ────────────────────────
        var updateResult = entity.Update(
            name: dto.Name,
            description: dto.Description,
            isActive: dto.IsActive);

        if (!updateResult.IsSuccess)
            return updateResult;

        // ── Step 2: Sync TeamMembers ────────────────────────────
        var memberErrors = SyncTeamMembers(db, entity, dto.Members, relatedDeleteBehavior);

        return memberErrors.Count > 0
            ? DomainResult<Team>.Failure(memberErrors)
            : DomainResult<Team>.Success(entity);
    }

    /// <summary>
    /// Pattern: Child collection sync via CollectionUtility.
    /// TeamMember supports Create, Update, and Remove (unlike Comment which is append-only).
    /// </summary>
    private static List<string> SyncTeamMembers(
        TaskFlowDbContextTrxn db,
        Team entity,
        List<TeamMemberDto> incomingMembers,
        RelatedDeleteBehavior deleteBehavior)
    {
        var errors = new List<string>();

        CollectionUtility.SyncCollectionWithResult(
            existing: entity.Members.ToList(),
            incoming: incomingMembers,
            existingKey: e => e.Id,
            incomingKey: i => i.Id == Guid.Empty ? null : (Guid?)i.Id,

            // Pattern: Update existing child — delegate to child entity's Update() method.
            update: (existingMember, incomingDto) =>
            {
                var result = existingMember.Update(
                    displayName: incomingDto.DisplayName,
                    role: incomingDto.Role,
                    hourlyRate: incomingDto.HourlyRate);
                if (!result.IsSuccess) errors.AddRange(result.Errors);
            },

            // Pattern: Create new child via parent's AddMember() domain method.
            add: incomingDto =>
            {
                var result = entity.AddMember(
                    userId: incomingDto.UserId.ToString(),
                    displayName: incomingDto.DisplayName,
                    role: incomingDto.Role);
                if (!result.IsSuccess)
                {
                    errors.AddRange(result.Errors);
                    return null;
                }
                return result.Value;
            },

            // Pattern: Remove child based on RelatedDeleteBehavior.
            remove: existingMember =>
            {
                if (deleteBehavior == RelatedDeleteBehavior.None) return;
                entity.RemoveMember(existingMember.UserId);
                // Pattern: Full delete — remove from DbContext to issue DELETE SQL.
                if (deleteBehavior == RelatedDeleteBehavior.Delete)
                    db.Set<TeamMember>().Remove(existingMember);
            });

        return errors;
    }
}
