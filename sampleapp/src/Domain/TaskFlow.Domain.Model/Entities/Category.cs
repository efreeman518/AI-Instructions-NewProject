// Pattern: Simple tenant entity — 1:many parent of TodoItems.
// Demonstrates: cacheable static data entity, tenant entity, basic CRUD.
// Categories are loaded into FusionCache at startup and refreshed on write.

using EF.Domain;

namespace Domain.Model.Entities;

/// <summary>
/// Categorization for todo items (e.g., "Work", "Personal", "Urgent").
/// Tenant-scoped — each tenant has their own categories.
/// Cached as static data with long TTL.
/// </summary>
public class Category : EntityBase, ITenantEntity<Guid>
{
    /// <summary>Tenant isolation — set once at creation.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Display name. Required, max 100 chars.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional description of what this category is for.</summary>
    public string? Description { get; private set; }

    /// <summary>Display color hex code (e.g., "#FF5733"). Optional.</summary>
    public string? ColorHex { get; private set; }

    /// <summary>Sort order for UI display. Lower = higher priority.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Whether this category is active. Inactive categories hidden from pickers.</summary>
    public bool IsActive { get; private set; } = true;

    // Pattern: Navigation collection — TodoItems in this category.
    // Not loaded by default; use Include() when needed.
    public ICollection<TodoItem> TodoItems { get; private set; } = [];

    // Pattern: Private parameterless constructor for EF Core.
    private Category() { }

    /// <summary>
    /// Factory — creates a new Category with validated state.
    /// </summary>
    public static DomainResult<Category> Create(
        Guid tenantId,
        string name,
        string? description,
        string? colorHex,
        int displayOrder)
    {
        var entity = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            ColorHex = colorHex,
            DisplayOrder = displayOrder,
            IsActive = true
        };

        return entity.Valid() ? DomainResult<Category>.Success(entity)
                              : DomainResult<Category>.Failure(entity._validationErrors);
    }

    public DomainResult<Category> Update(
        string name,
        string? description,
        string? colorHex,
        int displayOrder,
        bool isActive)
    {
        Name = name;
        Description = description;
        ColorHex = colorHex;
        DisplayOrder = displayOrder;
        IsActive = isActive;

        return Valid() ? DomainResult<Category>.Success(this)
                       : DomainResult<Category>.Failure(_validationErrors);
    }

    private List<string> _validationErrors = [];

    private bool Valid()
    {
        _validationErrors = [];

        if (string.IsNullOrWhiteSpace(Name))
            _validationErrors.Add("Name is required.");

        if (Name?.Length > 100)
            _validationErrors.Add("Name must not exceed 100 characters.");

        if (ColorHex is not null && !System.Text.RegularExpressions.Regex.IsMatch(ColorHex, @"^#[0-9A-Fa-f]{6}$"))
            _validationErrors.Add("ColorHex must be a valid hex color (e.g., #FF5733).");

        return _validationErrors.Count == 0;
    }
}
