// ═══════════════════════════════════════════════════════════════
// Pattern: Client-side Category record — simple cacheable entity.
// Demonstrates the same wire-DTO wrapping pattern for a simpler entity.
// Categories are static data — typically cached and rarely mutated.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Business.Models;

public partial record Category : IEntityBase
{
    internal Category(CategoryData data)
    {
        Id = data.Id ?? Guid.Empty;
        Name = data.Name;
        Description = data.Description;
        IsActive = data.IsActive ?? true;
    }

    public Category() { }

    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;

    internal CategoryData ToData() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        IsActive = IsActive
    };
}

// Pattern: Contrived wire DTO — Kiota-generated in real projects.
public class CategoryData
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
