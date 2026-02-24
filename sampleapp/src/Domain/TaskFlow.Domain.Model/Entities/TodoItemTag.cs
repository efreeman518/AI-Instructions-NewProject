// Pattern: Many-to-many bridge/junction entity.
// Composite key (TodoItemId, TagId) — configured in EF with HasKey().
// Lightweight entity — no business logic, no DomainResult factory.
// Child collection sync via CollectionUtility.SyncCollectionWithResult in the Updater.

using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Junction entity linking TodoItem ↔ Tag (many-to-many).
/// Composite primary key on (TodoItemId, TagId).
/// No Id property — this is not an EntityBase derivative.
/// </summary>
public class TodoItemTag
{
    /// <summary>FK to the todo item.</summary>
    public Guid TodoItemId { get; init; }

    /// <summary>FK to the global tag.</summary>
    public Guid TagId { get; init; }

    // Pattern: Navigation properties for both sides of the junction.
    public TodoItem TodoItem { get; private set; } = null!;
    public Tag Tag { get; private set; } = null!;

    /// <summary>Optional: when this tag was applied (for audit/display).</summary>
    public DateTimeOffset AppliedAt { get; init; } = DateTimeOffset.UtcNow;

    // Pattern: Junction entities use a simple constructor — no DomainResult needed.
    // Validation is handled at the service layer (ensure tag and item exist).
    private TodoItemTag() { }

    public static TodoItemTag Create(Guid todoItemId, Guid tagId)
    {
        return new TodoItemTag
        {
            TodoItemId = todoItemId,
            TagId = tagId,
            AppliedAt = DateTimeOffset.UtcNow
        };
    }
}
