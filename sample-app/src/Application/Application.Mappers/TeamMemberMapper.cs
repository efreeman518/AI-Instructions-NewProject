namespace Application.Mappers;

public static class TeamMemberMapper
{
    public static TeamMemberDto ToDto(this TeamMember entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TeamId = entity.TeamId,
            UserId = entity.UserId,
            DisplayName = entity.DisplayName,
            Role = entity.Role,
            HourlyRate = entity.HourlyRate,
            JoinedAt = entity.JoinedAt,
        };
}
