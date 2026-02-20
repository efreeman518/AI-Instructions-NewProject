// Pattern: Cross-entity business rule — prevents deletion of a Category
// that still has active (non-archived, non-cancelled) TodoItems.
// The service layer fetches the relevant data and passes it to this rule.

using Domain.Model.Entities;
using Domain.Model.Enums;

namespace Domain.Model.Rules;

/// <summary>
/// Rule: A Category cannot be deleted if it has active TodoItems.
/// "Active" = not Archived and not Cancelled.
/// The service layer provides the collection of items for evaluation.
/// </summary>
public class CategoryDeletionRule : RuleBase<(Category Category, IEnumerable<TodoItem> Items)>
{
    public override string ErrorMessage =>
        "Cannot delete category — it still has active todo items. Archive or reassign them first.";

    public override bool IsSatisfiedBy((Category Category, IEnumerable<TodoItem> Items) context)
    {
        // Pattern: Cross-entity check — needs data from both category and its items.
        // The service must load/provide these before invoking this rule.
        return !context.Items.Any(item =>
            !item.Status.HasFlag(TodoItemStatus.IsArchived) &&
            !item.Status.HasFlag(TodoItemStatus.IsCancelled));
    }
}
