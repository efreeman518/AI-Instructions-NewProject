// ═══════════════════════════════════════════════════════════════
// Pattern: Service implementation — the richest service in the solution.
// Demonstrates: DI via primary constructor, IRequestContext for tenant/role,
// IFusionCacheProvider for caching, tenant boundary validation,
// domain rule evaluation, event publishing via IInternalMessageBus,
// and delegation to Updater for writes.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using EF.Common;
using EF.Domain;
using EF.Domain.Contracts;
using Application.Contracts.Events;
using Application.Contracts.Mappers;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.TodoItem;
using Domain.Model.Entities;
using Domain.Model.Enums;
using Domain.Model.Rules;
using Domain.Model.ValueObjects;
using Domain.Shared;
using ZiggyCreatures.Caching.Fusion;

namespace Application.Services;

/// <summary>
/// Pattern: Service class — internal (only exposed through its interface).
/// Uses primary constructor for dependency injection.
/// All public methods return Result{T} — never throw exceptions for business failures.
/// </summary>
internal class TodoItemService(
    ILogger<TodoItemService> logger,
    IRequestContext<string, Guid?> requestContext,
    ITodoItemRepositoryQuery repoQuery,
    ITodoItemUpdater updater,
    IInternalMessageBus messageBus,
    IFusionCacheProvider fusionCacheProvider) : ITodoItemService
{
    // Pattern: Get a named cache instance — matches the cache name from Constants.
    private readonly IFusionCache _cache = fusionCacheProvider.GetCache(Constants.CacheNames.Default);

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Convenience properties from IRequestContext.
    // IRequestContext is populated by middleware from JWT claims.
    // ═══════════════════════════════════════════════════════════════
    private Guid? CallerTenantId => requestContext.TenantId;
    private IReadOnlyCollection<string> CallerRoles => requestContext.Roles;
    private string CallerUserId => requestContext.UserId ?? "system";
    private bool IsGlobalAdmin => CallerRoles.Contains(Constants.Roles.GlobalAdmin);

    // ═══════════════════════════════════════════════════════════════
    // Search — paged projection query
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<PagedResponse<TodoItemDto>>> SearchAsync(
        TodoItemSearchFilter filter, CancellationToken ct = default)
    {
        // Pattern: Enforce tenant boundary for non-global-admin callers.
        // Global admins can search across tenants; regular users are confined.
        if (!IsGlobalAdmin)
        {
            filter.TenantId = CallerTenantId;
        }

        logger.LogDebug("SearchAsync: TenantId={TenantId}, Status={Status}, Text={Text}",
            filter.TenantId, filter.Status, filter.SearchText);

        var result = await repoQuery.QueryPageProjectionAsync(filter, ct);
        return Result<PagedResponse<TodoItemDto>>.Success(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // Get by ID — single entity with caching
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<TodoItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Pattern: Cache-aside — try cache first, fall back to repository.
        var dto = await _cache.GetOrSetAsync(
            $"TodoItem:{id}",
            async (ctx, cancellation) => await repoQuery.QueryByIdProjectionAsync(id, cancellation),
            token: ct);

        if (dto is null)
        {
            logger.LogWarning("TodoItem {Id} not found", id);
            return Result<TodoItemDto>.NotFound();
        }

        // Pattern: Tenant boundary check — ensure the caller can access this entity.
        if (!IsGlobalAdmin && dto.TenantId != CallerTenantId)
        {
            logger.LogWarning("Tenant boundary violation: caller {CallerTenant} tried to access TodoItem in tenant {EntityTenant}",
                CallerTenantId, dto.TenantId);
            return Result<TodoItemDto>.Forbidden("Access denied — tenant boundary violation.");
        }

        return Result<TodoItemDto>.Success(dto);
    }

    // ═══════════════════════════════════════════════════════════════
    // Create — validate, delegate to updater, publish event
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<TodoItemDto>> CreateAsync(TodoItemDto dto, CancellationToken ct = default)
    {
        // Pattern: Enforce tenant from caller context (prevent spoofing).
        if (!IsGlobalAdmin)
            dto.TenantId = CallerTenantId ?? dto.TenantId;

        // Pattern: Validate hierarchy depth before creating.
        if (dto.ParentId.HasValue)
        {
            var hierarchyRule = new TodoItemHierarchyRule(dto.ParentId.Value, []);
            var ruleResult = hierarchyRule.Evaluate(new TodoItem()); // lightweight check
            if (!ruleResult.IsSuccess)
                return Result<TodoItemDto>.Failure(ruleResult.Errors);
        }

        // Pattern: Delegate write to Updater — it handles repo + cache + mapping.
        var result = await updater.CreateAsync(dto, ct);
        if (!result.IsSuccess)
            return result;

        // Pattern: Publish domain event — consumed by MessageHandlers (audit, etc.).
        await messageBus.PublishAsync(new TodoItemCreatedEvent(
            TodoItemId: result.Value!.Id,
            TenantId: dto.TenantId,
            Title: dto.Title,
            AssignedToId: dto.AssignedToId,
            CreatedBy: CallerUserId), ct);

        logger.LogInformation("TodoItem {Id} created by {User}", result.Value!.Id, CallerUserId);
        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    // Update — validate status transition, delegate, publish events
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<TodoItemDto>> UpdateAsync(TodoItemDto dto, CancellationToken ct = default)
    {
        // Pattern: Fetch existing to compare for change detection.
        var existing = await repoQuery.QueryByIdProjectionAsync(dto.Id, ct);
        if (existing is null)
            return Result<TodoItemDto>.NotFound();

        // Pattern: Tenant boundary — prevent cross-tenant updates.
        if (!IsGlobalAdmin && existing.TenantId != CallerTenantId)
            return Result<TodoItemDto>.Forbidden("Access denied — tenant boundary violation.");

        // Pattern: Prevent tenant change on update.
        if (existing.TenantId != dto.TenantId)
            return Result<TodoItemDto>.Failure("Tenant cannot be changed after creation.");

        // Pattern: Status transition validation — use domain rule.
        if (existing.Status != dto.Status)
        {
            var transitionRule = new TodoItemStatusTransitionRule(existing.Status, dto.Status);
            var ruleResult = transitionRule.Evaluate(new TodoItem());
            if (!ruleResult.IsSuccess)
                return Result<TodoItemDto>.Failure(ruleResult.Errors);
        }

        var result = await updater.UpdateAsync(dto, ct);
        if (!result.IsSuccess)
            return result;

        // Pattern: Publish different events based on what changed.
        await messageBus.PublishAsync(new TodoItemUpdatedEvent(
            TodoItemId: dto.Id,
            TenantId: dto.TenantId,
            Title: dto.Title,
            PreviousStatus: existing.Status,
            NewStatus: dto.Status,
            PreviousAssignedToId: existing.AssignedToId,
            NewAssignedToId: dto.AssignedToId,
            UpdatedBy: CallerUserId), ct);

        // Pattern: Conditional event — only publish assignment event if assignee actually changed.
        if (existing.AssignedToId != dto.AssignedToId && dto.AssignedToId.HasValue)
        {
            await messageBus.PublishAsync(new TodoItemAssignedEvent(
                TodoItemId: dto.Id,
                TenantId: dto.TenantId,
                Title: dto.Title,
                PreviousAssignedToId: existing.AssignedToId,
                NewAssignedToId: dto.AssignedToId.Value,
                AssignedBy: CallerUserId), ct);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    // Delete — idempotent, validate no children
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await repoQuery.QueryByIdProjectionAsync(id, ct);
        if (existing is null)
            return Result.Success(); // Pattern: Idempotent delete — not found = success.

        if (!IsGlobalAdmin && existing.TenantId != CallerTenantId)
            return Result.Forbidden("Access denied — tenant boundary violation.");

        // Pattern: Hierarchy constraint — cannot delete parent with children.
        var children = await repoQuery.GetChildrenAsync(id, ct);
        if (children.Count > 0)
            return Result.Failure($"Cannot delete TodoItem with {children.Count} child item(s). Remove children first.");

        return await updater.DeleteAsync(id, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // ChangeStatus — domain-specific operation with state machine
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<TodoItemDto>> ChangeStatusAsync(
        Guid id, TodoItemStatus newStatus, CancellationToken ct = default)
    {
        var existing = await repoQuery.QueryByIdProjectionAsync(id, ct);
        if (existing is null)
            return Result<TodoItemDto>.NotFound();

        if (!IsGlobalAdmin && existing.TenantId != CallerTenantId)
            return Result<TodoItemDto>.Forbidden("Access denied — tenant boundary violation.");

        // Pattern: Domain rule evaluation — TodoItemStatusTransitionRule checks the state machine.
        var rule = new TodoItemStatusTransitionRule(existing.Status, newStatus);
        var ruleResult = rule.Evaluate(new TodoItem());
        if (!ruleResult.IsSuccess)
            return Result<TodoItemDto>.Failure(ruleResult.Errors);

        existing.Status = newStatus;
        return await updater.UpdateAsync(existing, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // Assign — domain-specific operation
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<TodoItemDto>> AssignAsync(
        Guid id, Guid assignedToId, CancellationToken ct = default)
    {
        var existing = await repoQuery.QueryByIdProjectionAsync(id, ct);
        if (existing is null)
            return Result<TodoItemDto>.NotFound();

        if (!IsGlobalAdmin && existing.TenantId != CallerTenantId)
            return Result<TodoItemDto>.Forbidden("Access denied — tenant boundary violation.");

        var previousAssignedToId = existing.AssignedToId;
        existing.AssignedToId = assignedToId;

        var result = await updater.UpdateAsync(existing, ct);
        if (!result.IsSuccess)
            return result;

        // Pattern: Publish specific event for assignment change.
        await messageBus.PublishAsync(new TodoItemAssignedEvent(
            TodoItemId: id,
            TenantId: existing.TenantId,
            Title: existing.Title,
            PreviousAssignedToId: previousAssignedToId,
            NewAssignedToId: assignedToId,
            AssignedBy: CallerUserId), ct);

        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    // AddComment — child entity management through parent service
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<TodoItemDto>> AddCommentAsync(
        Guid todoItemId, string text, Guid authorId, CancellationToken ct = default)
    {
        // Pattern: Child creation through parent — services don't expose child CRUD directly.
        // The updater fetches the entity, calls entity.AddComment(), and saves.
        var existing = await repoQuery.QueryByIdProjectionAsync(todoItemId, ct);
        if (existing is null)
            return Result<TodoItemDto>.NotFound();

        if (!IsGlobalAdmin && existing.TenantId != CallerTenantId)
            return Result<TodoItemDto>.Forbidden("Access denied — tenant boundary violation.");

        // Pattern: Add comment data to the DTO and let the updater handle persistence.
        existing.Comments.Add(new Application.Models.Comment.CommentDto
        {
            TodoItemId = todoItemId,
            Text = text,
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return await updater.UpdateAsync(existing, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // SetTags — many-to-many junction management
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result> SetTagsAsync(
        Guid todoItemId, IEnumerable<Guid> tagIds, CancellationToken ct = default)
    {
        var existing = await repoQuery.QueryByIdProjectionAsync(todoItemId, ct);
        if (existing is null)
            return Result.NotFound();

        if (!IsGlobalAdmin && existing.TenantId != CallerTenantId)
            return Result.Forbidden("Access denied — tenant boundary violation.");

        // Pattern: Replace junction collection — updater handles diff (add new, remove missing).
        existing.Tags = tagIds.ToList();
        var result = await updater.UpdateAsync(existing, ct);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Errors);
    }

    // ═══════════════════════════════════════════════════════════════
    // GetChildren — hierarchy query
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<IReadOnlyList<TodoItemDto>>> GetChildrenAsync(
        Guid parentId, CancellationToken ct = default)
    {
        // Pattern: Tenant boundary is enforced by the repository's query filter.
        var children = await repoQuery.GetChildrenAsync(parentId, ct);
        return Result<IReadOnlyList<TodoItemDto>>.Success(children);
    }
}
