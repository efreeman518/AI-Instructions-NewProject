// ═══════════════════════════════════════════════════════════════
// Pattern: Service interfaces in Application.Contracts/Services/.
// Services are the primary API consumed by endpoints.
// They return Result<T> or Result (from EF.Common).
// Services DO NOT return entities — always DTOs.
// ═══════════════════════════════════════════════════════════════

using EF.Common;
using EF.Domain;
using Application.Models.TodoItem;

namespace Application.Contracts.Services;

/// <summary>
/// Pattern: Service interface — full CRUD + domain-specific operations.
/// The service orchestrates Repository reads, Updater writes, and domain rule evaluation.
/// </summary>
public interface ITodoItemService
{
    // ── Standard CRUD ───────────────────────────────────────────

    /// <summary>
    /// Pattern: Paged search returning PagedResponse.
    /// Uses repository's QueryPageProjectionAsync with filter/order builders.
    /// </summary>
    Task<Result<PagedResponse<TodoItemDto>>> SearchAsync(
        TodoItemSearchFilter filter,
        CancellationToken ct = default);

    /// <summary>Pattern: Get by ID — returns single DTO or NotFound.</summary>
    Task<Result<TodoItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Create — validates via domain rules, delegates to Updater.
    /// Publishes TodoItemCreatedEvent via IInternalMessageBus.
    /// </summary>
    Task<Result<TodoItemDto>> CreateAsync(TodoItemDto dto, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Update — validates status transition rules, delegates to Updater.
    /// Publishes TodoItemUpdatedEvent (and optionally TodoItemAssignedEvent).
    /// </summary>
    Task<Result<TodoItemDto>> UpdateAsync(TodoItemDto dto, CancellationToken ct = default);

    /// <summary>
    /// Pattern: Delete — validates hierarchy rules (no children), delegates to Updater.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    // ── Domain-specific operations ──────────────────────────────

    /// <summary>
    /// Pattern: Domain operation — changes status with transition rule validation.
    /// Uses TodoItemStatusTransitionRule to enforce valid state machine transitions.
    /// </summary>
    Task<Result<TodoItemDto>> ChangeStatusAsync(
        Guid id,
        Domain.Model.Enums.TodoItemStatus newStatus,
        CancellationToken ct = default);

    /// <summary>
    /// Pattern: Domain operation — assigns item to a user.
    /// Publishes TodoItemAssignedEvent if assignee changes.
    /// </summary>
    Task<Result<TodoItemDto>> AssignAsync(
        Guid id,
        Guid assignedToId,
        CancellationToken ct = default);

    /// <summary>
    /// Pattern: Child entity management — adds comment to item.
    /// Uses TodoItem.AddComment() domain method, persists via Updater.
    /// </summary>
    Task<Result<TodoItemDto>> AddCommentAsync(
        Guid todoItemId,
        string text,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Pattern: Tag management — manages many-to-many junction records.
    /// Creates/removes TodoItemTag bridge entities.
    /// </summary>
    Task<Result> SetTagsAsync(
        Guid todoItemId,
        IEnumerable<Guid> tagIds,
        CancellationToken ct = default);

    /// <summary>
    /// Pattern: Hierarchy query — returns child items for a parent.
    /// </summary>
    Task<Result<IReadOnlyList<TodoItemDto>>> GetChildrenAsync(
        Guid parentId,
        CancellationToken ct = default);
}
