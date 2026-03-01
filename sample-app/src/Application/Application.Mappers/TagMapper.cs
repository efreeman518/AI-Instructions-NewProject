namespace Application.Mappers;

public static class TagMapper
{
    public static TagDto ToDto(this Tag entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
        };
}
