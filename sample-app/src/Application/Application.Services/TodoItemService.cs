using Application.Contracts.Constants;
using Application.Contracts.Events;
using EF.BackgroundServices.InternalMessageBus;
using EF.Domain;
using EF.Domain.Contracts;

namespace Application.Services;

internal class TodoItemService(
    ILogger<TodoItemService> logger,
    IRequestContext<string, Guid?> requestContext,
    ITodoItemRepositoryTrxn repoTrxn,
    ITodoItemRepositoryQuery repoQuery,
    IInternalMessageBus messageBus) : ITodoItemService
{
    private Guid? CallerTenantId => requestContext.TenantId;

    public async Task<PagedResponse<TodoItemDto>> SearchAsync(SearchRequest<TodoItemSearchFilter> request, CancellationToken ct = default)
    {
        logger.LogDebug("Searching todo items");
        return await repoQuery.SearchAsync(request, ct);
    }

    public async Task<Result<TodoItemDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, includeChildren: true, ct);
        if (entity == null) return Result<TodoItemDto>.None();
        return Result<TodoItemDto>.Success(entity.ToDto());
    }

    public async Task<Result<TodoItemDto>> CreateAsync(TodoItemDto dto, CancellationToken ct = default)
    {
        var tenantId = dto.TenantId != Guid.Empty ? dto.TenantId : CallerTenantId ?? Guid.Empty;
        var entityResult = TodoItem.Create(tenantId, dto.Title, dto.Description, dto.Priority, dto.CategoryId, dto.TeamId);
        if (entityResult.IsFailure) return Result<TodoItemDto>.Failure(entityResult.ErrorMessage);

        var entity = entityResult.Value!;
        if (dto.EstimatedHours.HasValue || dto.ActualHours.HasValue)
        {
            var updateResult = entity.Update(estimatedHours: dto.EstimatedHours, actualHours: dto.ActualHours);
            if (updateResult.IsFailure) return Result<TodoItemDto>.Failure(updateResult.ErrorMessage);
        }
        if (dto.StartDate.HasValue || dto.DueDate.HasValue)
            entity.SetSchedule(dto.StartDate, dto.DueDate);
        if (dto.AssignedToId.HasValue)
            entity.Assign(dto.AssignedToId);
        if (dto.ParentId.HasValue)
            entity.SetParent(dto.ParentId);

        repoTrxn.Create(ref entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

        messageBus.Publish(InternalMessageBusProcessMode.Topic, [new TodoItemCreatedEvent(
            entity.Id, entity.TenantId, entity.Title, entity.AssignedToId,
            Guid.Empty)]);

        return Result<TodoItemDto>.Success(entity.ToDto());
    }

    public async Task<Result<TodoItemDto>> UpdateAsync(TodoItemDto dto, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(dto.Id, false, ct);
        if (entity == null) return Result<TodoItemDto>.None();

        var previousStatus = entity.Status;
        var previousAssignedToId = entity.AssignedToId;

        var updateResult = entity.Update(dto.Title, dto.Description, dto.Priority,
            dto.EstimatedHours, dto.ActualHours, dto.CategoryId, dto.TeamId);
        if (updateResult.IsFailure) return Result<TodoItemDto>.Failure(updateResult.ErrorMessage);

        if (dto.StartDate.HasValue || dto.DueDate.HasValue)
            entity.SetSchedule(dto.StartDate, dto.DueDate);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

        messageBus.Publish(InternalMessageBusProcessMode.Topic, [new TodoItemUpdatedEvent(
            entity.Id, entity.TenantId, entity.Title,
            previousStatus, entity.Status,
            previousAssignedToId, entity.AssignedToId,
            Guid.Empty)]);

        return Result<TodoItemDto>.Success(entity.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, false, ct);
        if (entity == null) return Result.Success();
        repoTrxn.Delete(entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result.Success();
    }

    // ── State transitions ────────────────────────────────────

    public Task<Result<TodoItemDto>> StartAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Start(), ct);
    public Task<Result<TodoItemDto>> CompleteAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Complete(), ct);
    public Task<Result<TodoItemDto>> BlockAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Block(), ct);
    public Task<Result<TodoItemDto>> UnblockAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Unblock(), ct);
    public Task<Result<TodoItemDto>> CancelAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Cancel(), ct);
    public Task<Result<TodoItemDto>> ArchiveAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Archive(), ct);
    public Task<Result<TodoItemDto>> RestoreAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Restore(), ct);
    public Task<Result<TodoItemDto>> ReopenAsync(Guid id, CancellationToken ct = default) => ExecuteTransitionAsync(id, e => e.Reopen(), ct);

    private async Task<Result<TodoItemDto>> ExecuteTransitionAsync(Guid id, Func<TodoItem, DomainResult<TodoItem>> transition, CancellationToken ct)
    {
        var entity = await repoTrxn.GetAsync(id, false, ct);
        if (entity == null) return Result<TodoItemDto>.None();

        var previousStatus = entity.Status;
        var result = transition(entity);
        if (result.IsFailure) return Result<TodoItemDto>.Failure(result.ErrorMessage);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

        messageBus.Publish(InternalMessageBusProcessMode.Topic, [new TodoItemUpdatedEvent(
            entity.Id, entity.TenantId, entity.Title,
            previousStatus, entity.Status, null, null,
            Guid.Empty)]);

        return Result<TodoItemDto>.Success(entity.ToDto());
    }

    // ── Assignment ───────────────────────────────────────────

    public async Task<Result<TodoItemDto>> AssignAsync(Guid id, Guid? assignedToId, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, false, ct);
        if (entity == null) return Result<TodoItemDto>.None();

        var previousAssignedToId = entity.AssignedToId;
        entity.Assign(assignedToId);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

        messageBus.Publish(InternalMessageBusProcessMode.Topic, [new TodoItemAssignedEvent(
            entity.Id, entity.TenantId, entity.Title,
            previousAssignedToId, assignedToId,
            Guid.Empty)]);

        return Result<TodoItemDto>.Success(entity.ToDto());
    }

    // ── Comments ─────────────────────────────────────────────

    public async Task<Result<CommentDto>> AddCommentAsync(Guid todoItemId, CommentDto comment, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(todoItemId, false, ct);
        if (entity == null) return Result<CommentDto>.None();

        var commentResult = entity.AddComment(comment.Text, comment.AuthorId);
        if (commentResult.IsFailure) return Result<CommentDto>.Failure(commentResult.ErrorMessage);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<CommentDto>.Success(commentResult.Value!.ToDto());
    }
}
