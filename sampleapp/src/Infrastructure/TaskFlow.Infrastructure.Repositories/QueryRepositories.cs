// ═══════════════════════════════════════════════════════════════
// Pattern: Query (read) repositories — read-optimized with NoTracking DbContext.
// Each query repository has: SearchAsync (paged), LookupAsync (autocomplete),
// BuildFilter (Expression builder), BuildOrderBy (sort builder).
// Uses Projectors from Mapper classes — never returns full entities from queries.
// ═══════════════════════════════════════════════════════════════

using System.Linq.Expressions;
using Application.Contracts.Mappers;
using Application.Contracts.Repositories;
using Application.Models;
using Domain.Model.Entities;
using Domain.Model.Enums;
using EF.Common;
using EF.Data;

namespace Infrastructure.Repositories;

// ═══════════════════════════════════════════════════════════════
// TodoItem Query Repository — demonstrates full search with filters + ordering.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Entity-specific Query repository.
/// Uses TaskFlowDbContextQuery (NoTracking) for all read operations.
/// Returns DTOs via Projectors — never exposes domain entities to callers.
/// </summary>
public class TodoItemRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITodoItemRepositoryQuery
{
    /// <summary>
    /// Pattern: Search with paged projection — uses QueryPageProjectionAsync from RepositoryBase.
    /// Combines filter builder, order builder, and projector for efficient SQL generation.
    /// The projector (Select expression) runs in SQL — only requested columns are fetched.
    /// </summary>
    public async Task<PagedResponse<TodoItemDto>> SearchTodoItemsAsync(
        SearchRequest<TodoItemSearchFilter> request, CancellationToken ct = default)
    {
        return await QueryPageProjectionAsync(
            projector: TodoItemMapper.ProjectorSearch,     // Pattern: Expression-based projection → SQL SELECT.
            filter: BuildFilter(request.Filter),           // Pattern: Dynamic WHERE clause.
            orderBy: BuildOrderBy(request),                // Pattern: Dynamic ORDER BY.
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            ct: ct);
    }

    /// <summary>
    /// Pattern: Lookup for autocomplete/dropdowns — returns lightweight StaticItem list.
    /// Uses ProjectorStaticItems which selects only Id, TenantId, and display text.
    /// </summary>
    public async Task<StaticList<StaticItem<Guid, Guid?>>> LookupTodoItemsAsync(
        Guid? tenantId, string? search, CancellationToken ct = default)
    {
        // Pattern: QueryPageProjectionAsync with small page size for autocomplete.
        var result = await QueryPageProjectionAsync(
            projector: TodoItemMapper.ProjectorStaticItems,
            filter: e => (tenantId == null || e.TenantId == tenantId)
                      && (search == null || e.Title.Contains(search)),
            orderBy: q => q.OrderBy(e => e.Title),
            pageSize: 50,
            pageIndex: 1,
            ct: ct);

        return new StaticList<StaticItem<Guid, Guid?>>(result.Data);
    }

    /// <summary>
    /// Pattern: BuildFilter — static method that constructs a WHERE clause Expression.
    /// Null-safe: each filter property is only applied if it has a value.
    /// Always includes TenantId for security.
    /// </summary>
    private static Expression<Func<TodoItem, bool>> BuildFilter(TodoItemSearchFilter? filter)
    {
        // Pattern: Start with "always true" and narrow down.
        if (filter == null) return _ => true;

        return e =>
            (filter.TenantId == null || e.TenantId == filter.TenantId) &&
            (filter.Title == null || e.Title.Contains(filter.Title)) &&
            (filter.Status == null || (e.Status & filter.Status.Value) != 0) &&            // Pattern: Flag enum filter with bitwise AND.
            (filter.CategoryId == null || e.CategoryId == filter.CategoryId) &&
            (filter.AssignedToId == null || e.AssignedToId == filter.AssignedToId) &&
            (filter.TeamId == null || e.TeamId == filter.TeamId) &&
            (filter.MinPriority == null || e.Priority >= filter.MinPriority) &&
            (filter.MaxPriority == null || e.Priority <= filter.MaxPriority) &&
            (filter.ParentId == null || e.ParentId == filter.ParentId);
    }

    /// <summary>
    /// Pattern: BuildOrderBy — maps sort field names to entity properties.
    /// Uses Expression-based approach for SQL translation.
    /// Falls back to CreatedDate descending if no sort specified.
    /// </summary>
    private static Func<IQueryable<TodoItem>, IOrderedQueryable<TodoItem>> BuildOrderBy(
        SearchRequest<TodoItemSearchFilter> request)
    {
        var sortField = request.SortField?.ToLowerInvariant();
        var descending = request.SortDescending;

        return sortField switch
        {
            "title" => descending
                ? q => q.OrderByDescending(e => e.Title)
                : q => q.OrderBy(e => e.Title),
            "priority" => descending
                ? q => q.OrderByDescending(e => e.Priority)
                : q => q.OrderBy(e => e.Priority),
            "status" => descending
                ? q => q.OrderByDescending(e => e.Status)
                : q => q.OrderBy(e => e.Status),
            "duedate" => descending
                ? q => q.OrderByDescending(e => e.DateRange!.EndDate)
                : q => q.OrderBy(e => e.DateRange!.EndDate),
            _ => q => q.OrderByDescending(e => e.CreatedDate)    // Pattern: Default sort.
        };
    }
}

// ═══════════════════════════════════════════════════════════════
// Category Query Repository — cacheable static data with lookup.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Simple query repository for static/reference data.
/// </summary>
public class CategoryRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ICategoryRepositoryQuery
{
    public async Task<PagedResponse<CategoryDto>> SearchCategoriesAsync(
        SearchRequest<CategorySearchFilter> request, CancellationToken ct = default)
    {
        return await QueryPageProjectionAsync(
            projector: CategoryMapper.ProjectorSearch,
            filter: BuildFilter(request.Filter),
            orderBy: q => q.OrderBy(e => e.DisplayOrder).ThenBy(e => e.Name),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            ct: ct);
    }

    public async Task<StaticList<StaticItem<Guid, Guid?>>> LookupCategoriesAsync(
        Guid? tenantId, string? search, CancellationToken ct = default)
    {
        var result = await QueryPageProjectionAsync(
            projector: CategoryMapper.ProjectorStaticItems,
            filter: e => (tenantId == null || e.TenantId == tenantId)
                      && (search == null || e.Name.Contains(search)),
            orderBy: q => q.OrderBy(e => e.DisplayOrder),
            pageSize: 100,
            pageIndex: 1,
            ct: ct);

        return new StaticList<StaticItem<Guid, Guid?>>(result.Data);
    }

    private static Expression<Func<Category, bool>> BuildFilter(CategorySearchFilter? filter)
    {
        if (filter == null) return _ => true;

        return e =>
            (filter.TenantId == null || e.TenantId == filter.TenantId) &&
            (filter.Name == null || e.Name.Contains(filter.Name)) &&
            (filter.IsActive == null || e.IsActive == filter.IsActive);
    }
}

// ═══════════════════════════════════════════════════════════════
// Tag Query Repository — non-tenant, global entity.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Non-tenant query repository — no TenantId in filter.
/// </summary>
public class TagRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITagRepositoryQuery
{
    public async Task<PagedResponse<TagDto>> SearchTagsAsync(
        SearchRequest<TagSearchFilter> request, CancellationToken ct = default)
    {
        return await QueryPageProjectionAsync(
            projector: TagMapper.ProjectorSearch,
            filter: BuildFilter(request.Filter),
            orderBy: q => q.OrderBy(e => e.Name),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            ct: ct);
    }

    private static Expression<Func<Tag, bool>> BuildFilter(TagSearchFilter? filter)
    {
        if (filter == null) return _ => true;

        return e =>
            (filter.Name == null || e.Name.Contains(filter.Name));
    }
}

// ═══════════════════════════════════════════════════════════════
// Team Query Repository — parent with child count projection.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Query repository for parent entity — includes member count in search results.
/// </summary>
public class TeamRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITeamRepositoryQuery
{
    public async Task<PagedResponse<TeamDto>> SearchTeamsAsync(
        SearchRequest<TeamSearchFilter> request, CancellationToken ct = default)
    {
        return await QueryPageProjectionAsync(
            projector: TeamMapper.ProjectorSearch,
            filter: BuildFilter(request.Filter),
            orderBy: q => q.OrderBy(e => e.Name),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            ct: ct);
    }

    private static Expression<Func<Team, bool>> BuildFilter(TeamSearchFilter? filter)
    {
        if (filter == null) return _ => true;

        return e =>
            (filter.TenantId == null || e.TenantId == filter.TenantId) &&
            (filter.Name == null || e.Name.Contains(filter.Name)) &&
            (filter.IsActive == null || e.IsActive == filter.IsActive);
    }
}

// ═══════════════════════════════════════════════════════════════
// Child Entity Query Repositories — Comment, Attachment, Reminder, TodoItemHistory.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Child entity query repository — queries scoped to parent entity.
/// </summary>
public class CommentRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ICommentRepositoryQuery
{
    public async Task<List<CommentDto>> GetCommentsByTodoItemAsync(
        Guid todoItemId, CancellationToken ct = default)
    {
        return await DB.Set<Comment>()
            .Where(c => c.TodoItemId == todoItemId)
            .OrderByDescending(c => c.CreatedDate)
            .Select(CommentMapper.Projector)
            .ToListAsync(ct);
    }
}

/// <summary>
/// Pattern: Polymorphic query — filter by EntityType + EntityId.
/// </summary>
public class AttachmentRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), IAttachmentRepositoryQuery
{
    public async Task<List<AttachmentDto>> GetAttachmentsByEntityAsync(
        EntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        return await DB.Set<Attachment>()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedDate)
            .Select(AttachmentMapper.Projector)
            .ToListAsync(ct);
    }
}

/// <summary>
/// Pattern: Scheduler query repository — finds reminders by time range.
/// </summary>
public class ReminderRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), IReminderRepositoryQuery
{
    public async Task<List<ReminderDto>> GetRemindersByTodoItemAsync(
        Guid todoItemId, CancellationToken ct = default)
    {
        return await DB.Set<Reminder>()
            .Where(r => r.TodoItemId == todoItemId)
            .OrderBy(r => r.DueDate)
            .Select(ReminderMapper.Projector)
            .ToListAsync(ct);
    }
}

/// <summary>
/// Pattern: Timeline query — ordered by ChangedAt descending for audit trail display.
/// </summary>
public class TodoItemHistoryRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITodoItemHistoryRepositoryQuery
{
    public async Task<List<TodoItemHistoryDto>> GetHistoryByTodoItemAsync(
        Guid todoItemId, CancellationToken ct = default)
    {
        return await DB.Set<TodoItemHistory>()
            .Where(h => h.TodoItemId == todoItemId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(TodoItemHistoryMapper.Projector)
            .ToListAsync(ct);
    }
}

// ═══════════════════════════════════════════════════════════════
// GenericRepositoryQuery — used for shared read operations.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Pattern: Generic query repository — used by EntityCacheProvider and shared code.
/// </summary>
public class GenericRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), IGenericRepositoryQuery
{
}
