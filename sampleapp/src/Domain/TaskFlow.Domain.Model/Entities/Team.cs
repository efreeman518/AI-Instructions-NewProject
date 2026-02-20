// Pattern: Tenant entity with child collection management (Add/Remove member methods).
// Demonstrates: cross-entity domain rule anchor (can't deactivate team with active items).
// The domain rule itself lives in Rules/ as a specification; the entity just provides
// the collection and methods. Services invoke the rule before status changes.

using Domain.Model.Enums;
using Package.Infrastructure.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Team entity — a group of people who can be assigned todo items.
/// Tenant-scoped. Parents TeamMember children.
/// </summary>
public class Team : EntityBase, ITenantEntity<Guid>
{
    public Guid TenantId { get; init; }

    /// <summary>Team display name. Required, max 100 chars.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional team description or mission statement.</summary>
    public string? Description { get; private set; }

    /// <summary>Whether this team is active. Deactivation subject to domain rules.</summary>
    public bool IsActive { get; private set; } = true;

    // Pattern: Child collection managed via Add/Remove methods on parent.
    // SyncCollectionWithResult in the Updater handles bulk sync from DTOs.
    public ICollection<TeamMember> Members { get; private set; } = [];

    private Team() { }

    public static DomainResult<Team> Create(
        Guid tenantId,
        string name,
        string? description)
    {
        var entity = new Team
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            IsActive = true
        };

        return entity.Valid() ? DomainResult<Team>.Success(entity)
                              : DomainResult<Team>.Failure(entity._validationErrors);
    }

    public DomainResult<Team> Update(string name, string? description, bool isActive)
    {
        Name = name;
        Description = description;
        IsActive = isActive;

        return Valid() ? DomainResult<Team>.Success(this)
                       : DomainResult<Team>.Failure(_validationErrors);
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Child collection management — Add/Remove on the aggregate root.
    // Idempotent add: if member already exists, return existing rather than duplicate.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a member to this team. Idempotent — returns existing if already a member.
    /// </summary>
    public DomainResult<TeamMember> AddMember(string userId, string displayName, MemberRole role)
    {
        // Pattern: Idempotent add — check for existing before creating.
        var existing = Members.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null) return DomainResult<TeamMember>.Success(existing);

        var memberResult = TeamMember.Create(Id, TenantId, userId, displayName, role);
        if (!memberResult.IsSuccess) return memberResult;

        Members.Add(memberResult.Value!);
        return memberResult;
    }

    /// <summary>
    /// Removes a member by user ID. Idempotent — no error if not found.
    /// Owners can only be removed by global admin (enforced at service layer).
    /// </summary>
    public void RemoveMember(string userId)
    {
        var member = Members.FirstOrDefault(m => m.UserId == userId);
        if (member is not null) Members.Remove(member);
    }

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(Name))
            _validationErrors.Add("Team name is required.");

        if (Name?.Length > 100)
            _validationErrors.Add("Team name must not exceed 100 characters.");

        return _validationErrors.Count == 0;
    }
}
