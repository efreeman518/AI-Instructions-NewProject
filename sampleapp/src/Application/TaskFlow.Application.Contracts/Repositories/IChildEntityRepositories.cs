// Pattern: Repository interfaces for child/auxiliary entities.
// These entities may only need Query or Trxn, not both.

using EF.Domain;
using Domain.Model.Entities;
using Application.Models.Comment;
using Application.Models.Attachment;
using Application.Models.Reminder;
using Application.Models.TodoItemHistory;

namespace Application.Contracts.Repositories;

// ═══════════════════════════════════════════════════════════════
// Comment — append-only child
// Pattern: Query + Trxn but Trxn only has Add/Delete (no Update — append-only).
// ═══════════════════════════════════════════════════════════════

public interface ICommentRepositoryQuery : IBaseRepositoryQuery<Comment, CommentDto>
{
    Task<IReadOnlyList<CommentDto>> GetByTodoItemIdAsync(Guid todoItemId, CancellationToken ct = default);
}

public interface ICommentRepositoryTrxn : IBaseRepositoryTrxn<Comment> { }

// ═══════════════════════════════════════════════════════════════
// Attachment — polymorphic, immutable (no Update)
// ═══════════════════════════════════════════════════════════════

public interface IAttachmentRepositoryQuery : IBaseRepositoryQuery<Attachment, AttachmentDto>
{
    /// <summary>
    /// Pattern: Polymorphic query — filters by EntityId AND EntityType discriminator.
    /// Returns attachments for any entity type (TodoItem, Comment, etc.).
    /// </summary>
    Task<IReadOnlyList<AttachmentDto>> GetByEntityAsync(
        Guid entityId,
        Domain.Model.Enums.EntityType entityType,
        CancellationToken ct = default);
}

public interface IAttachmentRepositoryTrxn : IBaseRepositoryTrxn<Attachment> { }

// ═══════════════════════════════════════════════════════════════
// Reminder — time-based entity, queried by scheduler
// ═══════════════════════════════════════════════════════════════

public interface IReminderRepositoryQuery : IBaseRepositoryQuery<Reminder, ReminderDto>
{
    /// <summary>
    /// Pattern: Scheduler query — returns active reminders due for firing.
    /// Used by the TickerQ job to find reminders that need processing.
    /// </summary>
    Task<IReadOnlyList<ReminderDto>> GetDueRemindersAsync(
        DateTimeOffset asOfUtc,
        CancellationToken ct = default);
}

public interface IReminderRepositoryTrxn : IBaseRepositoryTrxn<Reminder> { }

// ═══════════════════════════════════════════════════════════════
// TodoItemHistory — event-driven, read-only from service perspective
// Pattern: Only Query interface — Trxn writes happen in MessageHandler directly.
// ═══════════════════════════════════════════════════════════════

public interface ITodoItemHistoryRepositoryQuery : IBaseRepositoryQuery<TodoItemHistory, TodoItemHistoryDto>
{
    Task<IReadOnlyList<TodoItemHistoryDto>> GetByTodoItemIdAsync(
        Guid todoItemId,
        CancellationToken ct = default);
}

/// <summary>
/// Pattern: Trxn interface exists but is only injected into MessageHandlers, not Services.
/// This establishes that history records are created only as side effects of domain events.
/// </summary>
public interface ITodoItemHistoryRepositoryTrxn : IBaseRepositoryTrxn<TodoItemHistory> { }
