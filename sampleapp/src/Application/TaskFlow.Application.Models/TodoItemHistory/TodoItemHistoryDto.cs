// Pattern: Event-driven read-only DTO — TodoItemHistory.
// No IEntityBaseDto because history records are created by message handlers, not CRUD operations.

namespace Application.Models.TodoItemHistory;

public class TodoItemHistoryDto
{
    public Guid Id { get; set; }
    public Guid TodoItemId { get; set; }

    /// <summary>Describes the action: "Created", "StatusChanged", "Assigned", "CommentAdded", etc.</summary>
    public string Action { get; set; } = string.Empty;

    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? ChangeDescription { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
}
