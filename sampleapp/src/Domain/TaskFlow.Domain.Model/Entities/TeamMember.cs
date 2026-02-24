// Pattern: Child entity of Team — managed via parent's Add/Remove methods.
// Demonstrates: simple enum property (MemberRole), FK relationship,
// decimal field (HourlyRate) for time-tracking/money-type coverage.

using Domain.Model.Enums;
using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// A member of a Team. Created/removed through Team.AddMember()/RemoveMember().
/// Links a user identity (UserId from Entra/claims) to a team with a role.
/// </summary>
public class TeamMember : EntityBase, ITenantEntity<Guid>
{
    public Guid TenantId { get; init; }

    /// <summary>FK to the parent Team.</summary>
    public Guid TeamId { get; init; }

    /// <summary>External user identity ID (from Entra ID / claims). Not a FK — external reference.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Cached display name for UI (denormalized from identity provider).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Role within the team — Owner, Admin, or Member.</summary>
    public MemberRole Role { get; private set; }

    /// <summary>Hourly rate for time tracking. Null if not applicable.</summary>
    /// <remarks>
    /// Pattern: Decimal field for money/rate coverage.
    /// Configured in EF as decimal(10,4) — see TeamMemberConfiguration.
    /// </remarks>
    public decimal? HourlyRate { get; private set; }

    /// <summary>When this member joined the team.</summary>
    public DateTimeOffset JoinedAt { get; init; } = DateTimeOffset.UtcNow;

    // Pattern: Navigation to parent but NOT back to child collections from here.
    // Parent collection reference is on Team.Members only.
    public Team Team { get; private set; } = null!;

    private TeamMember() { }

    /// <summary>
    /// Factory — called by Team.AddMember(), not directly by application code.
    /// </summary>
    internal static DomainResult<TeamMember> Create(
        Guid teamId,
        Guid tenantId,
        string userId,
        string displayName,
        MemberRole role)
    {
        var entity = new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            TenantId = tenantId,
            UserId = userId,
            DisplayName = displayName,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow
        };

        return entity.Valid() ? DomainResult<TeamMember>.Success(entity)
                              : DomainResult<TeamMember>.Failure(entity._validationErrors);
    }

    public DomainResult<TeamMember> Update(string displayName, MemberRole role, decimal? hourlyRate)
    {
        DisplayName = displayName;
        Role = role;
        HourlyRate = hourlyRate;

        return Valid() ? DomainResult<TeamMember>.Success(this)
                       : DomainResult<TeamMember>.Failure(_validationErrors);
    }

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(UserId))
            _validationErrors.Add("UserId is required.");

        if (string.IsNullOrWhiteSpace(DisplayName))
            _validationErrors.Add("DisplayName is required.");

        if (DisplayName?.Length > 200)
            _validationErrors.Add("DisplayName must not exceed 200 characters.");

        if (HourlyRate is < 0)
            _validationErrors.Add("HourlyRate cannot be negative.");

        return _validationErrors.Count == 0;
    }
}
