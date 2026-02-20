// Pattern: Simple entity DTO — Category.

using Package.Infrastructure.Domain.Contracts;

namespace Application.Models.Category;

public class CategoryDto : IEntityBaseDto
{
    public Guid Id { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }

    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Pattern: Color/visual metadata field — used in UI for category badges.</summary>
    public string? ColorHex { get; set; }

    /// <summary>Pattern: Display order — manual sort override for static data.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Pattern: Search filter for a simple entity.
/// Demonstrates that even simple entities follow the same filter pattern.
/// </summary>
public class CategorySearchFilter : Package.Infrastructure.Domain.SearchFilterBase
{
    public Guid? TenantId { get; set; }
    public string? SearchText { get; set; }
    public bool? IsActive { get; set; }
}
