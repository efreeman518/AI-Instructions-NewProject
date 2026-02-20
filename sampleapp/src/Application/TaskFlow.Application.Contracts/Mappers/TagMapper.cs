// Pattern: Mapper for non-tenant shared entity (Tag).
// Note: no TenantId in the DTO since Tags are global.

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.Tag;

namespace Application.Contracts.Mappers;

public static class TagMapper
{
    public static TagDto ToDto(this Tag entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description
    };

    public static DomainResult<Tag> ToEntity(this TagDto dto)
    {
        return Tag.Create(dto.Name, dto.Description);
    }

    public static Expression<Func<Tag, TagDto>> ProjectorSearch => entity => new TagDto
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description
    };

    public static Expression<Func<Tag, StaticItem<Guid, Guid?>>> ProjectorStaticItems =>
        entity => new StaticItem<Guid, Guid?>
        {
            Id = entity.Id,
            DisplayText = entity.Name
        };
}
