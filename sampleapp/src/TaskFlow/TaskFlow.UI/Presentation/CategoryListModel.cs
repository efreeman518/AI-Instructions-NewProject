// ═══════════════════════════════════════════════════════════════
// Pattern: Category List Model — simpler list model for static data entity.
// Demonstrates the same Feed/State/Command patterns as TodoItemListModel
// but for a cacheable entity with less complexity.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Business.Services.Categories;

namespace TaskFlow.UI.Presentation;

public partial record CategoryListModel
{
    private readonly ICategoryService _categoryService;
    private readonly IMessenger _messenger;

    public CategoryListModel(
        ICategoryService categoryService,
        IMessenger messenger)
    {
        _categoryService = categoryService;
        _messenger = messenger;
    }

    public IListFeed<Category> Items =>
        ListFeed.Async(_categoryService.GetAll);

    public IListState<Category> ObservableItems => ListState
        .FromFeed(this, Items)
        .Observe(_messenger, c => c.Id);
}
