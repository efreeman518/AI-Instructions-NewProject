// Pattern: Child entity mapper — TeamMember.

using Domain.Model.Entities;
using Application.Models.Team;

namespace Application.Contracts.Mappers;

public static class TeamMemberMapper
{
    public static TeamMemberDto ToDto(this TeamMember entity) => new()
    {
        Id = entity.Id,
        TeamId = entity.TeamId,
        UserId = entity.UserId,
        DisplayName = entity.DisplayName,
        Role = entity.Role,
        HourlyRate = entity.HourlyRate,
        JoinedAt = entity.JoinedAt
    };
}
