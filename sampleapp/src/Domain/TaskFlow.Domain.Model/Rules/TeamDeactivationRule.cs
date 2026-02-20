// Pattern: Cross-entity business rule — prevents deactivation of a Team
// that has TodoItems assigned to its members.
// Demonstrates: the rule receives pre-loaded data from the service layer.

using Domain.Model.Entities;

namespace Domain.Model.Rules;

/// <summary>
/// Rule: A Team cannot be deactivated if any of its members have
/// active TodoItems assigned to them.
/// </summary>
public class TeamDeactivationRule : RuleBase<(Team Team, int ActiveAssignedItemCount)>
{
    public override string ErrorMessage =>
        "Cannot deactivate team — members still have active todo items assigned. Reassign items first.";

    public override bool IsSatisfiedBy((Team Team, int ActiveAssignedItemCount) context)
    {
        // If the team is already inactive, this rule doesn't apply (idempotent)
        if (!context.Team.IsActive) return true;

        // The service pre-counts active items assigned to this team's members
        return context.ActiveAssignedItemCount == 0;
    }
}
