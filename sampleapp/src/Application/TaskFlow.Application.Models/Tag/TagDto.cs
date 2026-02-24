// Pattern: Non-tenant entity DTO — Tag (no TenantId).

using EF.Domain.Contracts;

namespace Application.Models.Tag;

public class TagDto : IEntityBaseDto
{
    public Guid Id { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Search filter for Tag — no TenantId since Tags are global.</summary>
public class TagSearchFilter : EF.Domain.SearchFilterBase
{
    public string? SearchText { get; set; }
}
