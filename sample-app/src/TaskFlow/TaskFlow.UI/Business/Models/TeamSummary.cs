using Domain.Shared;

namespace TaskFlow.UI.Business.Models;

/// <summary>
/// UI model for teams.
/// </summary>
public partial record TeamSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public int MemberCount { get; init; }
    public IImmutableList<TeamMemberSummary> Members { get; init; } = ImmutableList<TeamMemberSummary>.Empty;
}

/// <summary>
/// UI model for team members.
/// </summary>
public partial record TeamMemberSummary
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public TeamMemberRole Role { get; init; }
    public DateTimeOffset JoinedAt { get; init; }
}
