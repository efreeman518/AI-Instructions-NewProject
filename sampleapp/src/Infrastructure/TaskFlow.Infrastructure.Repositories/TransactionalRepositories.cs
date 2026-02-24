// ═══════════════════════════════════════════════════════════════
// Pattern: Transactional (write) repositories — each entity gets a RepositoryTrxn.
// Inherits from RepositoryBase which provides: GetEntityAsync, Create, UpdateFull,
// DeleteAsync, SaveChangesAsync (with optimistic concurrency), QueryPageAsync.
// Custom methods handle includes for child collections and updater delegation.
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Repositories;
using Application.Models;
using Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using EF.Data;

namespace Infrastructure.Repositories;

// ═══════════════════════════════════════════════════════════════
// TodoItem — the most complex repository demonstrating all patterns.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Entity-specific Trxn repository.
/// Constructor injection of TaskFlowDbContextTrxn (scoped via DbContextScopedFactory).
/// Inherits full CRUD from RepositoryBase. Custom methods handle complex includes.
/// </summary>
public class TodoItemRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITodoItemRepositoryTrxn
{
    /// <summary>
    /// Pattern: GetEntity with conditional includes — only loads child collections
    /// when explicitly requested. Uses SplitQuery for performance with multiple includes.
    /// </summary>
    public async Task<TodoItem?> GetTodoItemAsync(
        Guid id, bool includeChildren = false, CancellationToken ct = default)
    {
        // Pattern: Build dynamic includes list based on caller's needs.
        var includes = new List<string>();
        if (includeChildren)
        {
            includes.Add(nameof(TodoItem.Comments));
            includes.Add(nameof(TodoItem.TodoItemTags));
            includes.Add(nameof(TodoItem.SubTasks));
        }

        // Pattern: GetEntityAsync from RepositoryBase — handles includes, split query.
        return await GetEntityAsync<TodoItem>(
            id,
            includeNavigationProperties: includes,
            splitQueryThresholdOptions: SplitQueryThresholdOptions.Default,
            ct: ct);
    }

    /// <summary>
    /// Pattern: UpdateFromDto — delegates to the Updater extension method
    /// which handles child collection synchronization.
    /// RelatedDeleteBehavior controls what happens to children not in the DTO.
    /// </summary>
    public DomainResult<TodoItem> UpdateFromDto(
        TodoItem entity, TodoItemDto dto, RelatedDeleteBehavior deleteBehavior = RelatedDeleteBehavior.Delete)
    {
        // Pattern: The updater is a static extension method on the DbContext.
        return DB.UpdateFromDto(entity, dto, deleteBehavior);
    }
}

// ═══════════════════════════════════════════════════════════════
// Category — simple tenant entity repository.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Simple entity Trxn repository — no custom methods needed beyond base CRUD.
/// Demonstrates that most entities need minimal repository code.
/// </summary>
public class CategoryRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ICategoryRepositoryTrxn
{
    // Pattern: Base class provides Create, UpdateFull, DeleteAsync, SaveChangesAsync.
    // No custom methods needed for simple entities.
}

// ═══════════════════════════════════════════════════════════════
// Tag — non-tenant global entity.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Non-tenant entity repository — no tenant query filter applies.
/// Tags are shared across all tenants.
/// </summary>
public class TagRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITagRepositoryTrxn
{
}

// ═══════════════════════════════════════════════════════════════
// Comment — append-only child entity.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Append-only child repository — Create and Delete only, no Update.
/// </summary>
public class CommentRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ICommentRepositoryTrxn
{
}

// ═══════════════════════════════════════════════════════════════
// Attachment — polymorphic entity via EntityType discriminator.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Polymorphic entity repository — queries filter by EntityType + EntityId.
/// </summary>
public class AttachmentRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), IAttachmentRepositoryTrxn
{
}

// ═══════════════════════════════════════════════════════════════
// Team — parent entity with child collection (TeamMembers).
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Parent entity repository with child includes.
/// </summary>
public class TeamRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITeamRepositoryTrxn
{
    public async Task<Team?> GetTeamAsync(
        Guid id, bool includeMembers = false, CancellationToken ct = default)
    {
        var includes = new List<string>();
        if (includeMembers)
        {
            includes.Add(nameof(Team.Members));
        }

        return await GetEntityAsync<Team>(
            id, includeNavigationProperties: includes, ct: ct);
    }
}

// ═══════════════════════════════════════════════════════════════
// Reminder — time-based entity for scheduler processing.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Scheduler-oriented repository — custom query for due reminders.
/// </summary>
public class ReminderRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), IReminderRepositoryTrxn
{
    /// <summary>
    /// Pattern: Custom query for scheduler — finds unsent reminders due before a given time.
    /// Uses IgnoreQueryFilters because scheduler runs cross-tenant.
    /// </summary>
    public async Task<List<Reminder>> GetDueRemindersAsync(
        DateTime asOf, int batchSize = 50, CancellationToken ct = default)
    {
        return await DB.Set<Reminder>()
            .IgnoreQueryFilters()                          // Pattern: Cross-tenant scheduler query.
            .Where(r => !r.IsSent && r.DueDate <= asOf)
            .OrderBy(r => r.DueDate)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}

// ═══════════════════════════════════════════════════════════════
// TodoItemHistory — event-driven, read-only entity (write via message handlers).
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Write-side repository for event-driven entities.
/// Only used by message handlers to Add history records.
/// </summary>
public class TodoItemHistoryRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITodoItemHistoryRepositoryTrxn
{
}

// ═══════════════════════════════════════════════════════════════
// GenericRepositoryTrxn — used for shared/utility write operations.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Generic repository for operations that don't need entity-specific methods.
/// Used by updaters and shared infrastructure code.
/// </summary>
public class GenericRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), IGenericRepositoryTrxn
{
}
