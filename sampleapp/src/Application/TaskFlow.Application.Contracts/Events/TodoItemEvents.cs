// Pattern: Event DTOs as records in Application.Contracts/Events/.
// These are lightweight, immutable data carriers published via IInternalMessageBus.
// Handlers in Application.MessageHandlers consume these.

using Domain.Model.Enums;

namespace Application.Contracts.Events;

/// <summary>Published after a new TodoItem is created successfully.</summary>
public record TodoItemCreatedEvent(
    Guid TodoItemId,
    Guid TenantId,
    string Title,
    Guid? AssignedToId,
    string CreatedBy);

/// <summary>Published after a TodoItem is updated (status, assignment, or content change).</summary>
public record TodoItemUpdatedEvent(
    Guid TodoItemId,
    Guid TenantId,
    string Title,
    TodoItemStatus PreviousStatus,
    TodoItemStatus NewStatus,
    Guid? PreviousAssignedToId,
    Guid? NewAssignedToId,
    string UpdatedBy);

/// <summary>
/// Published when a TodoItem's AssignedToId changes — triggers notification to new assignee.
/// Separate from Updated event for targeted handler routing.
/// </summary>
public record TodoItemAssignedEvent(
    Guid TodoItemId,
    Guid TenantId,
    string Title,
    Guid? PreviousAssignedToId,
    Guid NewAssignedToId,
    string AssignedBy);
