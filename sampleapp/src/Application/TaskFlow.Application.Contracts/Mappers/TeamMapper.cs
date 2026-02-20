// Pattern: Mapper for Team entity with child collection (Members).

using System.Linq.Expressions;
using Domain.Model.Entities;
using Application.Models.Team;

namespace Application.Contracts.Mappers;

public static class TeamMapper
{
    public static TeamDto ToDto(this Team entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Name = entity.Name,
        Description = entity.Description,
        IsActive = entity.IsActive,
        Members = entity.Members.Select(m => TeamMemberMapper.ToDto(m)).ToList()
    };

    public static DomainResult<Team> ToEntity(this TeamDto dto)
    {
        return Team.Create(dto.TenantId, dto.Name, dto.Description);
    }

    public static Expression<Func<Team, TeamDto>> ProjectorSearch => entity => new TeamDto
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Name = entity.Name,
        IsActive = entity.IsActive
    };

    public static Expression<Func<Team, TeamDto>> ProjectorRoot => entity => new TeamDto
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Name = entity.Name,
        Description = entity.Description,
        IsActive = entity.IsActive,
        Members = entity.Members.Select(m => new TeamMemberDto
        {
            Id = m.Id,
            TeamId = m.TeamId,
            UserId = m.UserId,
            DisplayName = m.DisplayName,
            Role = m.Role,
            HourlyRate = m.HourlyRate,
            JoinedAt = m.JoinedAt
        }).ToList()
    };
}
