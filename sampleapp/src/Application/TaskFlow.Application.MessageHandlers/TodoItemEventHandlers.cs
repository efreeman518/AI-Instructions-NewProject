// ═══════════════════════════════════════════════════════════════
// Pattern: MessageHandler — consumes internal events published by services.
// Handlers are registered with IInternalMessageBus and process events asynchronously.
// This handler creates audit/history records when TodoItems change.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using EF.BackgroundServices.InternalMessageBus;
using Application.Contracts.Events;
using Application.Contracts.Repositories;
using Domain.Model.Entities;

namespace Application.MessageHandlers;

/// <summary>
/// Pattern: IMessageHandler{T} — handles a specific event type.
/// Creates TodoItemHistory records for audit trail.
/// This is the ONLY place history records are created (enforced by internal Record() factory).
/// </summary>
public class TodoItemCreatedEventHandler(
    ILogger<TodoItemCreatedEventHandler> logger,
    ITodoItemHistoryRepositoryTrxn historyRepo) : IMessageHandler<TodoItemCreatedEvent>
{
    public async Task HandleAsync(TodoItemCreatedEvent message, CancellationToken ct = default)
    {
        logger.LogDebug("Handling TodoItemCreatedEvent for {TodoItemId}", message.TodoItemId);

        // Pattern: Create history record via internal factory — only accessible from this assembly.
        var history = TodoItemHistory.Record(
            todoItemId: message.TodoItemId,
            action: "Created",
            previousStatus: null,
            newStatus: "None",
            changeDescription: $"TodoItem '{message.Title}' created",
            changedBy: message.CreatedBy);

        historyRepo.Add(history);
        await historyRepo.SaveChangesAsync(ct);

        logger.LogInformation("Audit record created for new TodoItem {TodoItemId}", message.TodoItemId);
    }
}

/// <summary>
/// Pattern: Handler for the Updated event — records status changes and other mutations.
/// </summary>
public class TodoItemUpdatedEventHandler(
    ILogger<TodoItemUpdatedEventHandler> logger,
    ITodoItemHistoryRepositoryTrxn historyRepo) : IMessageHandler<TodoItemUpdatedEvent>
{
    public async Task HandleAsync(TodoItemUpdatedEvent message, CancellationToken ct = default)
    {
        logger.LogDebug("Handling TodoItemUpdatedEvent for {TodoItemId}", message.TodoItemId);

        var description = message.PreviousStatus != message.NewStatus
            ? $"Status changed from {message.PreviousStatus} to {message.NewStatus}"
            : $"TodoItem '{message.Title}' updated";

        var history = TodoItemHistory.Record(
            todoItemId: message.TodoItemId,
            action: message.PreviousStatus != message.NewStatus ? "StatusChanged" : "Updated",
            previousStatus: message.PreviousStatus.ToString(),
            newStatus: message.NewStatus.ToString(),
            changeDescription: description,
            changedBy: message.UpdatedBy);

        historyRepo.Add(history);
        await historyRepo.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Pattern: Handler that triggers notifications — assignment changes notify the new assignee.
/// Demonstrates cross-cutting concern: audit + notification from a single event.
/// </summary>
public class TodoItemAssignedEventHandler(
    ILogger<TodoItemAssignedEventHandler> logger,
    ITodoItemHistoryRepositoryTrxn historyRepo,
    INotificationService notificationService) : IMessageHandler<TodoItemAssignedEvent>
{
    public async Task HandleAsync(TodoItemAssignedEvent message, CancellationToken ct = default)
    {
        logger.LogDebug("Handling TodoItemAssignedEvent for {TodoItemId}", message.TodoItemId);

        // Pattern: Audit trail for assignment changes.
        var history = TodoItemHistory.Record(
            todoItemId: message.TodoItemId,
            action: "Assigned",
            previousStatus: null,
            newStatus: null,
            changeDescription: $"Assigned to {message.NewAssignedToId} (was {message.PreviousAssignedToId})",
            changedBy: message.AssignedBy);

        historyRepo.Add(history);
        await historyRepo.SaveChangesAsync(ct);

        // Pattern: Trigger notification — delegates to Infrastructure.Notification.
        await notificationService.SendAssignmentNotificationAsync(
            userId: message.NewAssignedToId,
            todoItemTitle: message.Title,
            ct);

        logger.LogInformation("Assignment notification sent for TodoItem {TodoItemId} to user {UserId}",
            message.TodoItemId, message.NewAssignedToId);
    }
}

/// <summary>
/// Pattern: Notification service interface — lives here because it's consumed by MessageHandlers.
/// Implemented in Infrastructure.Notification.
/// </summary>
public interface INotificationService
{
    Task SendAssignmentNotificationAsync(Guid userId, string todoItemTitle, CancellationToken ct = default);
    Task SendReminderNotificationAsync(Guid userId, string message, CancellationToken ct = default);
    Task SendOverdueNotificationAsync(Guid userId, string todoItemTitle, CancellationToken ct = default);
}
