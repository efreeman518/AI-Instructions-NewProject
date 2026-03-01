namespace Application.Mappers;

public static class TeamMapper
{
    public static TeamDto ToDto(this Team entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            Members = entity.Members?.Select(m => m.ToDto()).ToList() ?? [],
        };
}
