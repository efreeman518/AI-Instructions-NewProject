// ═══════════════════════════════════════════════════════════════
// Pattern: Category service — static data service with contrived mock data.
// In production, calls Gateway via Kiota-generated client.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Client;

namespace TaskFlow.UI.Business.Services.Categories;

public class CategoryService(
    TaskFlowApiClient api,
    IMessenger messenger) : ICategoryService
{
    private static readonly List<Category> _mockCategories =
    [
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Name = "Development", Description = "Coding and implementation tasks" },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                Name = "Testing", Description = "Quality assurance and test writing" },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                Name = "DevOps", Description = "CI/CD, deployment, and infrastructure" },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                Name = "Design", Description = "UI/UX design and prototyping", IsActive = false },
    ];

    public async ValueTask<IImmutableList<Category>> GetAll(CancellationToken ct)
    {
        await Task.Delay(80, ct);
        return _mockCategories.Where(c => c.IsActive).ToImmutableList();
    }

    public async ValueTask<Category> GetById(Guid id, CancellationToken ct)
    {
        await Task.Delay(50, ct);
        return _mockCategories.First(c => c.Id == id);
    }

    public async ValueTask Create(Category category, CancellationToken ct)
    {
        var newCategory = category with { Id = Guid.NewGuid() };
        _mockCategories.Add(newCategory);
        messenger.Send(new EntityMessage<Category>(EntityChange.Created, newCategory));
    }
}
