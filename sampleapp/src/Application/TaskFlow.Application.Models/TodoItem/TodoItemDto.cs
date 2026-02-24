// ═══════════════════════════════════════════════════════════════
// Pattern: DTO classes live in Application.Models/{EntityName}/.
// DTOs implement IEntityBaseDto from EF.Domain.Contracts.
// They are flat, serializable, and carry no behavior.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Enums;
using EF.Domain.Contracts;

namespace Application.Models.TodoItem;

/// <summary>
/// Pattern: DTO for TodoItem — flat representation of the entity.
/// Implements IEntityBaseDto which provides Id, CreatedBy, CreatedDate, etc.
/// Used for both API request/response and internal service communication.
/// </summary>
public class TodoItemDto : IEntityBaseDto
{
    // ── IEntityBaseDto members ───────────────────────────────────
    public Guid Id { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }

    // ── Entity-specific fields ──────────────────────────────────
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Pattern: [Flags] enum serialized as int — allows bitwise operations in queries.</summary>
    public TodoItemStatus Status { get; set; }

    public int Priority { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }

    // Pattern: Owned value object (DateRange) is flattened into the DTO.
    // The mapper reconstructs the value object from these flat properties.
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>Pattern: Self-referencing hierarchy — nullable parent FK.</summary>
    public Guid? ParentId { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? AssignedToId { get; set; }

    /// <summary>Pattern: Computed property from domain entity — included in DTO for convenience.</summary>
    public bool IsOverdue { get; set; }

    // ── Child collections ───────────────────────────────────────
    // Pattern: Child DTOs are nested in the parent DTO.
    // Only populated when using ProjectorRoot (detail view), not ProjectorSearch (list view).
    public List<Application.Models.Comment.CommentDto> Comments { get; set; } = [];
    public List<Guid> Tags { get; set; } = [];
}

/// <summary>
/// Pattern: StaticItem — lightweight ID + display text for dropdowns/pickers.
/// Generic with TId (usually Guid) and TParentId (nullable for hierarchy support).
/// </summary>
public class StaticItem<TId, TParentId>
{
    public TId Id { get; set; } = default!;
    public TParentId? ParentId { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}
