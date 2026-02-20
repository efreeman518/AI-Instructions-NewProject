// Pattern: Simple enum (non-flags) for role-based membership.
// Used on child entity (TeamMember) to control permissions.

namespace Domain.Model.Enums;

public enum MemberRole
{
    /// <summary>Standard team member — can be assigned tasks.</summary>
    Member = 0,

    /// <summary>Team administrator — can manage members and settings.</summary>
    Admin = 1,

    /// <summary>Team owner — full control, cannot be removed except by global admin.</summary>
    Owner = 2
}
