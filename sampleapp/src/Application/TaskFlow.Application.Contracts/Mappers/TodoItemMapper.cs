// Pattern: Static mapper class — no DI, no state, pure transformation methods.
// Lives in Application.Contracts/Mappers/ so both Services and Repositories can use it.
// Three types of mappings:
//   1. ToDto() — entity → DTO (extension method)
//   2. ToEntity() — DTO → entity returning DomainResult<T> (extension method)
//   3. Projectors — Expression<Func<T, TDto>> for EF-safe projections (no method calls)

using System.Linq.Expressions;
using Domain.Model.Entities;
using Domain.Model.ValueObjects;
using Application.Models.TodoItem;

namespace Application.Contracts.Mappers;

/// <summary>
/// Static mapper for TodoItem ↔ TodoItemDto transformations.
/// Projectors are EF-translatable expressions used in repository queries.
/// </summary>
public static class TodoItemMapper
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: ToDto() extension method — entity → DTO.
    // Used after fetching a tracked entity (e.g., after Create/Update).
    // ═══════════════════════════════════════════════════════════════

    public static TodoItemDto ToDto(this TodoItem entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Title = entity.Title,
        Description = entity.Description,
        Status = entity.Status,
        Priority = entity.Priority,
        EstimatedHours = entity.EstimatedHours,
        ActualHours = entity.ActualHours,
        StartDate = entity.Schedule?.StartDate,
        DueDate = entity.Schedule?.DueDate,
        ParentId = entity.ParentId,
        CategoryId = entity.CategoryId,
        AssignedToId = entity.AssignedToId,
        IsOverdue = entity.IsOverdue,
        // Pattern: Map child collections — use child mappers for nested entities.
        Comments = entity.Comments.Select(c => CommentMapper.ToDto(c)).ToList(),
        Tags = entity.TodoItemTags.Select(t => t.TagId).ToList()
    };

    // ═══════════════════════════════════════════════════════════════
    // Pattern: ToEntity() extension method — DTO → entity via DomainResult<T>.
    // Delegates to the entity's Create() factory. Never calls 'new' directly.
    // ═══════════════════════════════════════════════════════════════

    public static DomainResult<TodoItem> ToEntity(this TodoItemDto dto)
    {
        // Pattern: Create the value object first, then pass to entity factory.
        var scheduleResult = DateRange.Create(dto.StartDate, dto.DueDate);
        if (!scheduleResult.IsSuccess)
            return DomainResult<TodoItem>.Failure(scheduleResult.Errors);

        return TodoItem.Create(
            tenantId: dto.TenantId,
            title: dto.Title,
            description: dto.Description,
            priority: dto.Priority,
            estimatedHours: dto.EstimatedHours,
            categoryId: dto.CategoryId,
            assignedToId: dto.AssignedToId,
            parentId: dto.ParentId,
            schedule: scheduleResult.Value!);
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: EF-safe projectors — Expression<Func<T, TDto>>.
    // These are compiled by EF into SQL SELECT statements.
    // CRITICAL: No method calls inside — only property access and inline expressions.
    // Multiple projectors per entity for different use cases.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Minimal projector for search/list views — only essential fields.
    /// Used by QueryPageProjectionAsync for paged listing.
    /// </summary>
    public static Expression<Func<TodoItem, TodoItemDto>> ProjectorSearch => entity => new TodoItemDto
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Title = entity.Title,
        Status = entity.Status,
        Priority = entity.Priority,
        DueDate = entity.Schedule != null ? entity.Schedule.DueDate : null,
        CategoryId = entity.CategoryId,
        AssignedToId = entity.AssignedToId,
        IsOverdue = entity.Schedule != null && entity.Schedule.DueDate.HasValue
            && entity.Schedule.DueDate.Value < DateTimeOffset.UtcNow
            && (entity.Status & Domain.Model.Enums.TodoItemStatus.IsCompleted) == 0
    };

    /// <summary>
    /// Full projector for detail views — all fields including children.
    /// Used when fetching a single entity with all related data.
    /// </summary>
    public static Expression<Func<TodoItem, TodoItemDto>> ProjectorRoot => entity => new TodoItemDto
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        Title = entity.Title,
        Description = entity.Description,
        Status = entity.Status,
        Priority = entity.Priority,
        EstimatedHours = entity.EstimatedHours,
        ActualHours = entity.ActualHours,
        StartDate = entity.Schedule != null ? entity.Schedule.StartDate : null,
        DueDate = entity.Schedule != null ? entity.Schedule.DueDate : null,
        ParentId = entity.ParentId,
        CategoryId = entity.CategoryId,
        AssignedToId = entity.AssignedToId,
        IsOverdue = entity.Schedule != null && entity.Schedule.DueDate.HasValue
            && entity.Schedule.DueDate.Value < DateTimeOffset.UtcNow
            && (entity.Status & Domain.Model.Enums.TodoItemStatus.IsCompleted) == 0,
        Tags = entity.TodoItemTags.Select(t => t.TagId).ToList()
    };

    /// <summary>
    /// Lookup/autocomplete projector — returns StaticItem for pickers/dropdowns.
    /// Pattern: ProjectorStaticItems returns lightweight ID + display text pairs.
    /// </summary>
    public static Expression<Func<TodoItem, StaticItem<Guid, Guid?>>> ProjectorStaticItems =>
        entity => new StaticItem<Guid, Guid?>
        {
            Id = entity.Id,
            ParentId = entity.ParentId,
            DisplayText = entity.Title
        };
}
