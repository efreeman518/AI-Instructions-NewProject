// ═══════════════════════════════════════════════════════════════
// Pattern: Per-service settings — Team domain boundaries.
// Demonstrates a second settings class for a different aggregate.
// ═══════════════════════════════════════════════════════════════

namespace Application.Services;

/// <summary>
/// Pattern: Service settings — bound from "TeamServiceSettings" config section.
/// <code>
/// // appsettings.json:
/// "TeamServiceSettings": {
///   "MaxTeamSize": 50,
///   "AllowCrossTeamAssignment": false
/// }
/// </code>
/// </summary>
public class TeamServiceSettings
{
    public const string ConfigSectionName = "TeamServiceSettings";

    /// <summary>Maximum number of members allowed in a single team.</summary>
    public int MaxTeamSize { get; set; } = 50;

    /// <summary>Whether TodoItems can be assigned to members outside the item's team.</summary>
    public bool AllowCrossTeamAssignment { get; set; }

    /// <summary>Default role assigned to new team members.</summary>
    public string DefaultMemberRole { get; set; } = "Member";
}
