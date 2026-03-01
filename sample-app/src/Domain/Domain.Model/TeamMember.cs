namespace Domain.Model;

public class TeamMember : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<TeamMember> Create(Guid tenantId, Guid teamId, Guid userId, string displayName,
        TeamMemberRole role = TeamMemberRole.Member, decimal? hourlyRate = null)
    {
        var entity = new TeamMember(tenantId, teamId, userId, displayName, role, hourlyRate);
        return entity.Valid().Map(_ => entity);
    }

    private TeamMember(Guid tenantId, Guid teamId, Guid userId, string displayName, TeamMemberRole role, decimal? hourlyRate)
    {
        TenantId = tenantId;
        TeamId = teamId;
        UserId = userId;
        DisplayName = displayName;
        Role = role;
        HourlyRate = hourlyRate;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    // EF-compatible constructor
    private TeamMember() { }

    public Guid TenantId { get; init; }
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public TeamMemberRole Role { get; private set; } = TeamMemberRole.Member;
    public decimal? HourlyRate { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    // Navigation
    public Team Team { get; private set; } = null!;

    public DomainResult<TeamMember> Update(string? displayName = null, TeamMemberRole? role = null, decimal? hourlyRate = null)
    {
        if (displayName is not null) DisplayName = displayName;
        if (role.HasValue) Role = role.Value;
        if (hourlyRate.HasValue) HourlyRate = hourlyRate.Value;
        return Valid();
    }

    private DomainResult<TeamMember> Valid()
    {
        var errors = new List<DomainError>();
        if (UserId == Guid.Empty) errors.Add(DomainError.Create("UserId cannot be empty."));
        if (string.IsNullOrWhiteSpace(DisplayName)) errors.Add(DomainError.Create("DisplayName is required."));
        return (errors.Count > 0)
            ? DomainResult<TeamMember>.Failure(errors)
            : DomainResult<TeamMember>.Success(this);
    }
}
