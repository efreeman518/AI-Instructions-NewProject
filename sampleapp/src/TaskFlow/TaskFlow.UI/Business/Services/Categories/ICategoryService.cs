// ═══════════════════════════════════════════════════════════════
// Pattern: Client-side Category service — demonstrates the service
// pattern for a simpler, cacheable entity (static data).
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Business.Services.Categories;

public interface ICategoryService
{
    ValueTask<IImmutableList<Category>> GetAll(CancellationToken ct);
    ValueTask<Category> GetById(Guid id, CancellationToken ct);
    ValueTask Create(Category category, CancellationToken ct);
}
