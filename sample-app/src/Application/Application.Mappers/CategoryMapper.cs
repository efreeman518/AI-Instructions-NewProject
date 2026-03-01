namespace Application.Mappers;

public static class CategoryMapper
{
    public static CategoryDto ToDto(this Category entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Description = entity.Description,
            ColorHex = entity.ColorHex,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
        };
}
