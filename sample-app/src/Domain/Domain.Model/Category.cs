namespace Domain.Model;

public class Category : EntityBase, ITenantEntity<Guid>
{
    public static DomainResult<Category> Create(Guid tenantId, string name, string? description = null,
        string? colorHex = null, int? displayOrder = null)
    {
        var entity = new Category(tenantId, name, description, colorHex, displayOrder);
        return entity.Valid().Map(_ => entity);
    }

    private Category(Guid tenantId, string name, string? description, string? colorHex, int? displayOrder)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        ColorHex = colorHex;
        DisplayOrder = displayOrder ?? 0;
    }

    // EF-compatible constructor
    private Category() { }

    public Guid TenantId { get; init; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? ColorHex { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DomainResult<Category> Update(string? name = null, string? description = null,
        string? colorHex = null, int? displayOrder = null, bool? isActive = null)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
        if (colorHex is not null) ColorHex = colorHex;
        if (displayOrder.HasValue) DisplayOrder = displayOrder.Value;
        if (isActive.HasValue) IsActive = isActive.Value;
        return Valid();
    }

    private DomainResult<Category> Valid()
    {
        var errors = new List<DomainError>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > DomainConstants.NAME_MAX_LENGTH)
            errors.Add(DomainError.Create("Category name is required and must be 100 characters or fewer."));
        if (ColorHex is not null && !System.Text.RegularExpressions.Regex.IsMatch(ColorHex, @"^#[0-9A-Fa-f]{6}$"))
            errors.Add(DomainError.Create("Color must be a valid hex color (#RRGGBB)."));
        return (errors.Count > 0)
            ? DomainResult<Category>.Failure(errors)
            : DomainResult<Category>.Success(this);
    }
}
