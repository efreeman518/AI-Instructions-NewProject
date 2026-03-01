namespace Domain.Model;

public class Team : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<Team> Create(Guid tenantId, string name, string? description = null)
    {
        var entity = new Team(tenantId, name, description);
        return entity.Valid().Map(_ => entity);
    }

    private Team(Guid tenantId, string name, string? description)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
    }

    // EF-compatible constructor
    private Team() { }

    public Guid TenantId { get; init; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public ICollection<TeamMember> Members { get; private set; } = [];

    public DomainResult<Team> Update(string? name = null, string? description = null, bool? isActive = null)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
        if (isActive.HasValue) IsActive = isActive.Value;
        return Valid();
    }

    public DomainResult<TeamMember> AddMember(Guid userId, string displayName, TeamMemberRole role = TeamMemberRole.Member, decimal? hourlyRate = null)
    {
        var existing = Members.FirstOrDefault(m => m.UserId == userId);
        if (existing != null)
            return DomainResult<TeamMember>.Failure("User is already a member of this team.");

        var result = TeamMember.Create(TenantId, Id, userId, displayName, role, hourlyRate);
        if (result.IsFailure) return result;
        Members.Add(result.Value!);
        return result;
    }

    public DomainResult RemoveMember(Guid memberId)
    {
        var toRemove = Members.FirstOrDefault(m => m.Id == memberId);
        if (toRemove != null) Members.Remove(toRemove);
        return DomainResult.Success();
    }

    private DomainResult<Team> Valid()
    {
        var errors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > DomainConstants.NAME_MAX_LENGTH)
            errors.Add(DomainError.Create("Team name is required."));
        return (errors.Count > 0)
            ? DomainResult<Team>.Failure(errors)
            : DomainResult<Team>.Success(this);
    }
}
