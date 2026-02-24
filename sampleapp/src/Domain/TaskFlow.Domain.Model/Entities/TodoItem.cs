// Pattern: Rich domain entity — the most complex entity in the sample.
// Demonstrates: private ctor, Create()/Update()/Valid() factory pattern, DomainResult<T>,
// [Flags] enum status, self-referencing hierarchy (ParentId → TodoItem),
// child collections (Comments, TodoItemTags), owned value object (DateRange),
// ITenantEntity<Guid>, computed properties, Add/Remove child methods.

using Domain.Model.Enums;
using Domain.Model.ValueObjects;
using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Core entity representing a task/todo item with hierarchical sub-task support.
/// Multi-tenant. Self-referencing parent-child for unlimited nesting depth.
/// </summary>
public class TodoItem : EntityBase, ITenantEntity<Guid>
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: init for invariants (set once at creation, never changed).
    // private set for mutable state (changed via Update() method).
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Tenant isolation — set once at creation, never changes.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Title of the todo item. Required, max 200 chars.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Optional detailed description. Max 2000 chars.</summary>
    public string? Description { get; private set; }

    /// <summary>Current status flags — combinable (e.g., IsStarted | IsBlocked).</summary>
    public TodoItemStatus Status { get; private set; }

    /// <summary>Priority: 1 (highest) to 5 (lowest). Default 3.</summary>
    public int Priority { get; private set; } = 3;

    /// <summary>Estimated hours to complete. Decimal for partial hours.</summary>
    public decimal? EstimatedHours { get; private set; }

    /// <summary>Actual hours spent. Updated as work progresses.</summary>
    public decimal? ActualHours { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Owned value object — stored as columns on this table.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Date range (start/due) — owned type, no separate table.</summary>
    public DateRange Schedule { get; private set; } = null!;

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Self-referencing hierarchy — ParentId FK to same table.
    // Enables unlimited sub-task nesting depth.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Parent todo item ID. Null = root-level item.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>Navigation to parent item. Not loaded by default.</summary>
    public TodoItem? Parent { get; private set; }

    /// <summary>Child sub-tasks. Loaded via explicit Include().</summary>
    public ICollection<TodoItem> Children { get; private set; } = [];

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Foreign key relationships — nullable for optional assignment.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Category FK. Null = uncategorized.</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Assigned team member FK. Null = unassigned.</summary>
    public Guid? AssignedToId { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Child collections as ICollection<T>, initialized to [].
    // Never use List<T>. Add/Remove managed via methods on this entity.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Comments on this item. Append-only via AddComment().</summary>
    public ICollection<Comment> Comments { get; private set; } = [];

    /// <summary>Tags attached to this item via junction entity.</summary>
    public ICollection<TodoItemTag> TodoItemTags { get; private set; } = [];

    /// <summary>Reminders scheduled for this item.</summary>
    public ICollection<Reminder> Reminders { get; private set; } = [];

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Computed/convenience properties — expression-bodied getters.
    // These are NOT stored in the database; computed at read time.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>True if the due date has passed and item is not completed/cancelled.</summary>
    public bool IsOverdue => Schedule?.IsOverdue == true
        && !Status.HasFlag(TodoItemStatus.IsCompleted)
        && !Status.HasFlag(TodoItemStatus.IsCancelled);

    /// <summary>True if this is a root-level item (no parent).</summary>
    public bool IsRootItem => ParentId is null;

    /// <summary>True if status contains the IsCompleted flag.</summary>
    public bool IsCompleted => Status.HasFlag(TodoItemStatus.IsCompleted);

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Private parameterless constructor — required by EF Core
    // for entity materialization. Never called by application code.
    // ═══════════════════════════════════════════════════════════════
    private TodoItem() { }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Static Create() factory returning DomainResult<T>.
    // This is the ONLY way to create a new entity instance.
    // Calls Valid() internally to ensure invariants before construction.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Factory method — creates a new TodoItem with validated state.
    /// Returns DomainResult.Failure if validation fails.
    /// </summary>
    public static DomainResult<TodoItem> Create(
        Guid tenantId,
        string title,
        string? description,
        int priority,
        decimal? estimatedHours,
        Guid? categoryId,
        Guid? assignedToId,
        Guid? parentId,
        DateRange schedule)
    {
        var entity = new TodoItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Description = description,
            Priority = priority,
            EstimatedHours = estimatedHours,
            CategoryId = categoryId,
            AssignedToId = assignedToId,
            ParentId = parentId,
            Schedule = schedule,
            Status = TodoItemStatus.None  // Always starts as None
        };

        // Pattern: Validate after populating all fields — checks cross-field rules.
        return entity.Valid() ? DomainResult<TodoItem>.Success(entity)
                              : DomainResult<TodoItem>.Failure(entity._validationErrors);
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Update() method returning DomainResult<T>.
    // Mutates existing entity state and re-validates.
    // Called by the service layer after fetching from DB.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Updates mutable properties and re-validates the entity.
    /// Returns DomainResult.Failure if the new state is invalid.
    /// </summary>
    public DomainResult<TodoItem> Update(
        string title,
        string? description,
        TodoItemStatus status,
        int priority,
        decimal? estimatedHours,
        decimal? actualHours,
        Guid? categoryId,
        Guid? assignedToId,
        Guid? parentId,
        DateRange schedule)
    {
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        EstimatedHours = estimatedHours;
        ActualHours = actualHours;
        CategoryId = categoryId;
        AssignedToId = assignedToId;
        ParentId = parentId;
        Schedule = schedule;

        return Valid() ? DomainResult<TodoItem>.Success(this)
                       : DomainResult<TodoItem>.Failure(_validationErrors);
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Private Valid() method — invoked by both Create() and Update().
    // Collects all validation errors into _validationErrors list.
    // For complex/cross-entity rules, use the Specification pattern instead
    // (see Rules/ folder). Valid() handles simple field-level checks only.
    // ═══════════════════════════════════════════════════════════════

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(Title))
            _validationErrors.Add("Title is required.");

        if (Title?.Length > 200)
            _validationErrors.Add("Title must not exceed 200 characters.");

        if (Description?.Length > 2000)
            _validationErrors.Add("Description must not exceed 2000 characters.");

        if (Priority is < 1 or > 5)
            _validationErrors.Add("Priority must be between 1 and 5.");

        if (EstimatedHours is < 0)
            _validationErrors.Add("EstimatedHours cannot be negative.");

        if (ActualHours is < 0)
            _validationErrors.Add("ActualHours cannot be negative.");

        // Pattern: Cannot set both IsCompleted and IsBlocked simultaneously.
        if (Status.HasFlag(TodoItemStatus.IsCompleted) && Status.HasFlag(TodoItemStatus.IsBlocked))
            _validationErrors.Add("An item cannot be both Completed and Blocked.");

        // Pattern: Cancelled is a terminal state — cannot combine with Started.
        if (Status.HasFlag(TodoItemStatus.IsCancelled) && Status.HasFlag(TodoItemStatus.IsStarted))
            _validationErrors.Add("A cancelled item cannot be in Started state.");

        // Pattern: Self-reference guard — an item cannot be its own parent.
        if (ParentId.HasValue && ParentId.Value == Id)
            _validationErrors.Add("An item cannot be its own parent.");

        return _validationErrors.Count == 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Child collection management methods on parent entity.
    // Idempotent add (returns existing if already present).
    // Ensures the collection relationship is always managed through the aggregate root.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a comment to this item. Comments are append-only (no update).
    /// </summary>
    public DomainResult<Comment> AddComment(string text, string authorId)
    {
        var commentResult = Comment.Create(Id, TenantId, text, authorId);
        if (!commentResult.IsSuccess) return commentResult;

        Comments.Add(commentResult.Value!);
        return commentResult;
    }

    /// <summary>
    /// Removes a comment by ID. Idempotent — returns success if not found.
    /// </summary>
    public void RemoveComment(Guid commentId)
    {
        var comment = Comments.FirstOrDefault(c => c.Id == commentId);
        if (comment is not null) Comments.Remove(comment);
    }
}
