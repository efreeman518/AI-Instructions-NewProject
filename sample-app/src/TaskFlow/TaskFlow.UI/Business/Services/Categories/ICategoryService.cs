namespace TaskFlow.UI.Business.Services.Categories;

/// <summary>
/// UI-layer service interface for categories — calls Gateway API.
/// </summary>
public interface ICategoryService
{
    ValueTask<IImmutableList<CategorySummary>> GetAll(CancellationToken ct);
    ValueTask<CategorySummary?> GetById(Guid id, CancellationToken ct);
    ValueTask<CategorySummary?> Create(CategorySummary item, CancellationToken ct);
    ValueTask<CategorySummary?> Update(CategorySummary item, CancellationToken ct);
    ValueTask<bool> Delete(Guid id, CancellationToken ct);
}
