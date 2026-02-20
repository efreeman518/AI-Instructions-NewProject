// Pattern: Hierarchy validation rule — prevents circular references
// and enforces maximum nesting depth for self-referencing entities.
// The service layer resolves the ancestor chain before invoking this rule.

namespace Domain.Model.Rules;

/// <summary>
/// Rule: TodoItem hierarchy must not exceed max depth and must not create circular references.
/// </summary>
public class TodoItemHierarchyRule : RuleBase<(Guid ItemId, Guid? ProposedParentId, IReadOnlyList<Guid> AncestorChain)>
{
    /// <summary>Maximum allowed nesting depth. Configurable per business need.</summary>
    public const int MaxDepth = 5;

    public override string ErrorMessage => _errorMessage;
    private string _errorMessage = string.Empty;

    public override bool IsSatisfiedBy((Guid ItemId, Guid? ProposedParentId, IReadOnlyList<Guid> AncestorChain) context)
    {
        // No parent = root item, always valid
        if (!context.ProposedParentId.HasValue) return true;

        // Pattern: Circular reference detection — check if the item itself
        // appears in the ancestor chain of the proposed parent.
        if (context.AncestorChain.Contains(context.ItemId))
        {
            _errorMessage = "Circular reference detected — the proposed parent is a descendant of this item.";
            return false;
        }

        // Pattern: Max depth enforcement — ancestor chain length + 1 (for the item itself)
        if (context.AncestorChain.Count + 1 > MaxDepth)
        {
            _errorMessage = $"Maximum hierarchy depth of {MaxDepth} levels exceeded.";
            return false;
        }

        return true;
    }
}
