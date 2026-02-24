// Pattern: Event-driven, append-only, read-only entity.
// Created by IMessageHandler<TodoItemChangedEvent> — never via direct CRUD.
// No Create()/Update() factory — only an internal constructor for the handler.
// Read-only repository (Query only, no Trxn).
// Demonstrates: audit trail / event sourcing pattern.

using Domain.Model.Enums;
using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Immutable history record capturing state changes on a TodoItem.
/// Created exclusively by the TodoItemHistoryHandler in response to domain events.
/// Read-only — no updates or deletes through the application layer.
/// </summary>
public class TodoItemHistory : EntityBase, ITenantEntity<Guid>
{
    public Guid TenantId { get; init; }

    /// <summary>FK to the TodoItem that changed.</summary>
    public Guid TodoItemId { get; init; }

    /// <summary>What action triggered this history entry.</summary>
    public string Action { get; init; } = string.Empty; // e.g., "Created", "StatusChanged", "Assigned"

    /// <summary>Previous status flags (null if newly created).</summary>
    public TodoItemStatus? PreviousStatus { get; init; }

    /// <summary>New status flags after the change.</summary>
    public TodoItemStatus? NewStatus { get; init; }

    /// <summary>Previous assignee ID (null if unassigned or newly created).</summary>
    public Guid? PreviousAssignedToId { get; init; }

    /// <summary>New assignee ID after the change.</summary>
    public Guid? NewAssignedToId { get; init; }

    /// <summary>Optional human-readable description of the change.</summary>
    public string? ChangeDescription { get; init; }

    /// <summary>User who made the change (from IRequestContext.AuditId).</summary>
    public string ChangedBy { get; init; } = string.Empty;

    /// <summary>When the change occurred.</summary>
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;

    // Pattern: Internal constructor — only the message handler assembly can create these.
    // No public factory, no Create() method, no Update() method.
    private TodoItemHistory() { }

    /// <summary>
    /// Internal factory — called by TodoItemHistoryHandler only.
    /// Uses 'internal' visibility so only the Application.MessageHandlers assembly
    /// (which has InternalsVisibleTo) can create instances.
    /// </summary>
    internal static TodoItemHistory Record(
        Guid tenantId,
        Guid todoItemId,
        string action,
        TodoItemStatus? previousStatus,
        TodoItemStatus? newStatus,
        Guid? previousAssignedToId,
        Guid? newAssignedToId,
        string? changeDescription,
        string changedBy)
    {
        return new TodoItemHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TodoItemId = todoItemId,
            Action = action,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            PreviousAssignedToId = previousAssignedToId,
            NewAssignedToId = newAssignedToId,
            ChangeDescription = changeDescription,
            ChangedBy = changedBy,
            ChangedAt = DateTimeOffset.UtcNow
        };
    }
}
