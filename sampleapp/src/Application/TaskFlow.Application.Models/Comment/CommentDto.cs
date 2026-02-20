// Pattern: Append-only child entity DTO — Comment.
// No UpdatedBy/UpdatedDate since comments are immutable once created.

namespace Application.Models.Comment;

public class CommentDto
{
    public Guid Id { get; set; }
    public Guid TodoItemId { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
