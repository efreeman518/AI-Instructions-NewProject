// Pattern: Static mapper for a simple entity — Category.
// Demonstrates the same ToDto/ToEntity/Projector pattern on a simpler entity.

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.Category;

namespace Application.Contracts.Mappers;

public static class CategoryMapper
{
    public static CategoryDto ToDto(this Category entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Name = entity.Name,
        Description = entity.Description,
        ColorHex = entity.ColorHex,
        DisplayOrder = entity.DisplayOrder,
        IsActive = entity.IsActive
    };

    public static DomainResult<Category> ToEntity(this CategoryDto dto)
    {
        return Category.Create(
            tenantId: dto.TenantId,
            name: dto.Name,
            description: dto.Description,
            colorHex: dto.ColorHex,
            displayOrder: dto.DisplayOrder);
    }

    /// <summary>Search projector — minimal fields for list views.</summary>
    public static Expression<Func<Category, CategoryDto>> ProjectorSearch => entity => new CategoryDto
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Name = entity.Name,
        ColorHex = entity.ColorHex,
        DisplayOrder = entity.DisplayOrder,
        IsActive = entity.IsActive
    };

    /// <summary>Full projector for detail views.</summary>
    public static Expression<Func<Category, CategoryDto>> ProjectorRoot => entity => new CategoryDto
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Name = entity.Name,
        Description = entity.Description,
        ColorHex = entity.ColorHex,
        DisplayOrder = entity.DisplayOrder,
        IsActive = entity.IsActive
    };

    /// <summary>Lookup projector — ID + display text for dropdowns.</summary>
    public static Expression<Func<Category, StaticItem<Guid, Guid?>>> ProjectorStaticItems =>
        entity => new StaticItem<Guid, Guid?>
        {
            Id = entity.Id,
            DisplayText = entity.Name
        };
}
