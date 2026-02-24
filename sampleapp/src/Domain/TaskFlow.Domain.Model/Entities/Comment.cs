// Pattern: Append-only child entity — supports Create() but no Update().
// Demonstrates: child of aggregate root (TodoItem), cascade delete,
// temporal ordering, tenant entity (inherits tenant from parent).

using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// A comment on a TodoItem. Append-only — comments cannot be edited after creation.
/// Deleted when the parent TodoItem is deleted (cascade).
/// Tenant-scoped via parent relationship.
/// </summary>
public class Comment : EntityBase, ITenantEntity<Guid>
{
    /// <summary>Tenant isolation — inherited from parent TodoItem at creation.</summary>
    public Guid TenantId { get; init; }

    /// <summary>FK to the parent TodoItem.</summary>
    public Guid TodoItemId { get; init; }

    /// <summary>Comment text content. Required, max 1000 chars.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>User ID of the comment author (from identity claims).</summary>
    public string AuthorId { get; init; } = string.Empty;

    /// <summary>When the comment was created. Used for chronological ordering.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Pattern: No Update() method — this is an append-only entity.
    // Once created, the text is immutable. Delete and re-create if correction needed.

    private Comment() { }

    /// <summary>
    /// Factory — creates a new Comment. No Update() exists (append-only pattern).
    /// </summary>
    public static DomainResult<Comment> Create(
        Guid todoItemId,
        Guid tenantId,
        string text,
        string authorId)
    {
        var entity = new Comment
        {
            Id = Guid.NewGuid(),
            TodoItemId = todoItemId,
            TenantId = tenantId,
            Text = text,
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return entity.Valid() ? DomainResult<Comment>.Success(entity)
                              : DomainResult<Comment>.Failure(entity._validationErrors);
    }

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(Text))
            _validationErrors.Add("Comment text is required.");

        if (Text?.Length > 1000)
            _validationErrors.Add("Comment text must not exceed 1000 characters.");

        if (string.IsNullOrWhiteSpace(AuthorId))
            _validationErrors.Add("AuthorId is required.");

        return _validationErrors.Count == 0;
    }
}
