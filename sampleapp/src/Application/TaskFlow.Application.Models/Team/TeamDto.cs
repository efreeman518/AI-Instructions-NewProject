// Pattern: Parent entity DTO with child collection — Team + TeamMemberDto.

using Domain.Model.Enums;
using EF.Domain.Contracts;

namespace Application.Models.Team;

public class TeamDto : IEntityBaseDto
{
    public Guid Id { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }

    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Pattern: Child collection — populated in detail view (ProjectorRoot).</summary>
    public List<TeamMemberDto> Members { get; set; } = [];
}

/// <summary>Pattern: Child entity DTO — no IEntityBaseDto (simpler lifecycle).</summary>
public class TeamMemberDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public MemberRole Role { get; set; }

    /// <summary>Pattern: Decimal field — monetary/rate data stored with precision.</summary>
    public decimal? HourlyRate { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}

public class TeamSearchFilter : EF.Domain.SearchFilterBase
{
    public Guid? TenantId { get; set; }
    public string? SearchText { get; set; }
    public bool? IsActive { get; set; }
}
