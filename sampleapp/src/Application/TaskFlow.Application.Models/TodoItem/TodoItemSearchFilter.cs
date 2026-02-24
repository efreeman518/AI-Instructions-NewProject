// ═══════════════════════════════════════════════════════════════
// Pattern: SearchFilter — paging, sorting, and filtering criteria for list queries.
// Extends a base SearchFilterBase which provides Page, PageSize, SortField, SortDirection.
// Entity-specific filters add domain properties.
// The repository's FilterBuilder and OrderBuilder translate these into EF LINQ expressions.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Enums;
using EF.Domain;

namespace Application.Models.TodoItem;

/// <summary>
/// Search filter for TodoItem queries.
/// Each filterable property becomes a parameter that the FilterBuilder checks.
/// Null properties are ignored (not applied as AND conditions).
/// </summary>
public class TodoItemSearchFilter : SearchFilterBase
{
    /// <summary>Pattern: Tenant boundary filter — always applied for tenant entities.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Pattern: Text search — applied as LIKE/Contains on Title and Description.</summary>
    public string? SearchText { get; set; }

    /// <summary>Pattern: Enum filter — exact match on [Flags] status.</summary>
    public TodoItemStatus? Status { get; set; }

    /// <summary>Pattern: Range filter — items with priority >= this value.</summary>
    public int? MinPriority { get; set; }

    /// <summary>Pattern: FK filter — filter items by category.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Pattern: FK filter — filter items assigned to a specific user.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Pattern: Hierarchy filter — filter by parent (null = root items only).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Pattern: Boolean filter — show only overdue items.</summary>
    public bool? IsOverdue { get; set; }

    /// <summary>Pattern: Date range filter — items due within a date window.</summary>
    public DateTimeOffset? DueDateFrom { get; set; }
    public DateTimeOffset? DueDateTo { get; set; }

    /// <summary>Pattern: Tag filter — items that have ANY of these tags (OR logic).</summary>
    public List<Guid>? TagIds { get; set; }
}
