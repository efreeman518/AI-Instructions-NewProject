// ═══════════════════════════════════════════════════════════════
// Pattern: Simple updater for a non-tenant entity — Tag has no TenantId, no children.
// Demonstrates: updater for global (non-tenant) entities.
// ═══════════════════════════════════════════════════════════════

using Application.Models.Tag;
using Domain.Model.Entities;
using Package.Infrastructure.Domain;

namespace Infrastructure.Repositories.Updaters;

/// <summary>
/// Pattern: Non-tenant entity updater — no tenant boundary check in the updater.
/// Tenant boundary enforcement happens at the service layer (global admin only).
/// </summary>
internal static class TagUpdater
{
    /// <summary>
    /// Updates a Tag entity from its DTO. No child sync needed.
    /// </summary>
    public static DomainResult<Tag> UpdateFromDto(
        this TaskFlowDbContextTrxn db,
        Tag entity,
        TagDto dto)
    {
        return entity.Update(
            name: dto.Name,
            description: dto.Description);
    }
}
