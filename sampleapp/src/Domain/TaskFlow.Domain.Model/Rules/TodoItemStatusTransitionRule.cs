// Pattern: Status transition rule — validates that a flags enum state change is allowed.
// Uses a dictionary of allowed transitions to enforce the state machine.
// This prevents invalid jumps like Completed → Started without clearing first.

using Domain.Model.Enums;
using Domain.Model.Entities;

namespace Domain.Model.Rules;

/// <summary>
/// Validates that a TodoItem status transition is allowed.
/// Example: Cannot go from Completed to Started directly.
/// </summary>
public class TodoItemStatusTransitionRule : RuleBase<(TodoItemStatus Current, TodoItemStatus Proposed)>
{
    public override string ErrorMessage => $"Status transition from {_context.Current} to {_context.Proposed} is not allowed.";

    private (TodoItemStatus Current, TodoItemStatus Proposed) _context;

    // Pattern: Allowed transitions defined as a lookup.
    // Key = current status, Value = set of allowed next statuses.
    private static readonly Dictionary<TodoItemStatus, HashSet<TodoItemStatus>> _allowedTransitions = new()
    {
        // From None (new item) — can start, block, cancel, or archive
        [TodoItemStatus.None] = [TodoItemStatus.IsStarted, TodoItemStatus.IsBlocked, TodoItemStatus.IsCancelled, TodoItemStatus.IsArchived],

        // From Started — can complete, block, cancel, or go back to None
        [TodoItemStatus.IsStarted] = [TodoItemStatus.IsCompleted, TodoItemStatus.IsBlocked, TodoItemStatus.IsCancelled, TodoItemStatus.None],

        // From Blocked — can unblock to Started or None, or cancel
        [TodoItemStatus.IsBlocked] = [TodoItemStatus.IsStarted, TodoItemStatus.None, TodoItemStatus.IsCancelled],

        // From Completed — can reopen (back to Started) or archive
        [TodoItemStatus.IsCompleted] = [TodoItemStatus.IsStarted, TodoItemStatus.IsArchived],

        // From Archived — can unarchive back to None or Completed
        [TodoItemStatus.IsArchived] = [TodoItemStatus.None, TodoItemStatus.IsCompleted],

        // From Cancelled — terminal state, but allow reopen to None for correction
        [TodoItemStatus.IsCancelled] = [TodoItemStatus.None],
    };

    public override bool IsSatisfiedBy((TodoItemStatus Current, TodoItemStatus Proposed) context)
    {
        _context = context;

        // Same status is always allowed (no-op)
        if (context.Current == context.Proposed) return true;

        // Check if the transition is in the allowed set
        return _allowedTransitions.TryGetValue(context.Current, out var allowed)
            && allowed.Contains(context.Proposed);
    }
}
