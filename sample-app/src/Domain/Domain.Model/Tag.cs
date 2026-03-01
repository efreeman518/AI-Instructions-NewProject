namespace Domain.Model;

public class Tag : EntityBase
{
    public static DomainResult<Tag> Create(string name, string? description = null)
    {
        var entity = new Tag(name, description);
        return entity.Valid().Map(_ => entity);
    }

    private Tag(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    // EF-compatible constructor
    private Tag() { }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    // Many-to-many navigation
    public ICollection<TodoItem> TodoItems { get; private set; } = [];

    public DomainResult<Tag> Update(string? name = null, string? description = null)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
        return Valid();
    }

    private DomainResult<Tag> Valid()
    {
        var errors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > DomainConstants.TAG_NAME_MAX_LENGTH)
            errors.Add(DomainError.Create("Tag name is required and must be 50 characters or fewer."));
        return (errors.Count > 0)
            ? DomainResult<Tag>.Failure(errors)
            : DomainResult<Tag>.Success(this);
    }
}
