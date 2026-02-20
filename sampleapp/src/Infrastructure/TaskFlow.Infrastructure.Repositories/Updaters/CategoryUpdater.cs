// ═══════════════════════════════════════════════════════════════
// Pattern: Simple entity updater — Category has no child collections.
// Demonstrates: direct entity.Update() call, cache invalidation pattern note.
// Category is a cacheable static data entity; cache-on-write is handled at the service layer.
// ═══════════════════════════════════════════════════════════════

using Application.Models.Category;
using Domain.Model.Entities;
using Package.Infrastructure.Domain;

namespace Infrastructure.Repositories.Updaters;

/// <summary>
/// Pattern: Simple updater — no child collection sync needed.
/// Delegates to entity.Update() and returns the DomainResult.
/// </summary>
internal static class CategoryUpdater
{
    /// <summary>
    /// Updates a Category entity from its DTO.
    /// No child sync — Category is a simple flat entity.
    /// </summary>
    public static DomainResult<Category> UpdateFromDto(
        this TaskFlowDbContextTrxn db,
        Category entity,
        CategoryDto dto)
    {
        return entity.Update(
            name: dto.Name,
            description: dto.Description,
            colorHex: dto.ColorHex,
            displayOrder: dto.DisplayOrder,
            isActive: dto.IsActive);
    }
}
