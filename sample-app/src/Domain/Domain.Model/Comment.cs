namespace Domain.Model;

public class Comment : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<Comment> Create(Guid tenantId, Guid todoItemId, string text, Guid authorId)
    {
        var entity = new Comment(tenantId, todoItemId, text, authorId);
        return entity.Valid().Map(_ => entity);
    }

    private Comment(Guid tenantId, Guid todoItemId, string text, Guid authorId)
    {
        TenantId = tenantId;
        TodoItemId = todoItemId;
        Text = text;
        AuthorId = authorId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF-compatible constructor
    private Comment() { }

    public Guid TenantId { get; init; }
    public Guid TodoItemId { get; private set; }
    public string Text { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation
    public TodoItem TodoItem { get; private set; } = null!;

    private DomainResult<Comment> Valid()
    {
        var errors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(Text) || Text.Length > DomainConstants.COMMENT_MAX_LENGTH)
            errors.Add(DomainError.Create("Comment text is required and must be 1000 characters or fewer."));
        if (AuthorId == Guid.Empty)
            errors.Add(DomainError.Create("Author is required."));
        return (errors.Count > 0)
            ? DomainResult<Comment>.Failure(errors)
            : DomainResult<Comment>.Success(this);
    }
}
