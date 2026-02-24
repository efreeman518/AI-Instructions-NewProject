// Pattern: Non-tenant shared entity — Tags are global (shared across all tenants).
// Demonstrates: entity WITHOUT ITenantEntity, many:many via junction (TodoItemTag).
// Global admin endpoints manage these; regular users can read but not create/modify.

using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Global tag for labeling todo items (e.g., "bug", "feature", "high-priority").
/// NOT tenant-scoped — shared across all tenants. Managed by global admins.
/// Linked to TodoItems via the TodoItemTag junction entity.
/// </summary>
public class Tag : EntityBase
{
    /// <summary>Tag label. Required, max 50 chars, unique.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional tag description.</summary>
    public string? Description { get; private set; }

    // Pattern: Navigation to junction entity — not the other side directly.
    // To get TodoItems for a tag, go through TodoItemTags.
    public ICollection<TodoItemTag> TodoItemTags { get; private set; } = [];

    private Tag() { }

    public static DomainResult<Tag> Create(string name, string? description)
    {
        var entity = new Tag
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description
        };

        return entity.Valid() ? DomainResult<Tag>.Success(entity)
                              : DomainResult<Tag>.Failure(entity._validationErrors);
    }

    public DomainResult<Tag> Update(string name, string? description)
    {
        Name = name;
        Description = description;

        return Valid() ? DomainResult<Tag>.Success(this)
                       : DomainResult<Tag>.Failure(_validationErrors);
    }

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(Name))
            _validationErrors.Add("Tag name is required.");

        if (Name?.Length > 50)
            _validationErrors.Add("Tag name must not exceed 50 characters.");

        return _validationErrors.Count == 0;
    }
}
