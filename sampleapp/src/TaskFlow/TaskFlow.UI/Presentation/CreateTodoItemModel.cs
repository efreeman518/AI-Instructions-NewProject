// ═══════════════════════════════════════════════════════════════
// Pattern: MVUX Create Model — form-based entity creation.
// Uses IState<T> for each editable field, bound two-way in XAML.
// Save command validates, calls service, then navigates back.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Business.Services.TodoItems;
using TaskFlow.UI.Business.Services.Categories;

namespace TaskFlow.UI.Presentation;

public partial record CreateTodoItemModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _todoItemService;
    private readonly ICategoryService _categoryService;

    public CreateTodoItemModel(
        INavigator navigator,
        ITodoItemService todoItemService,
        ICategoryService categoryService)
    {
        _navigator = navigator;
        _todoItemService = todoItemService;
        _categoryService = categoryService;
    }

    // Pattern: Mutable state for form fields — two-way bound in XAML.
    public IState<string> Title => State<string>.Value(this, () => string.Empty);
    public IState<string> Description => State<string>.Value(this, () => string.Empty);
    public IState<int> Priority => State.Value(this, () => 1);

    // Pattern: Feed for dropdown/picker data (categories list).
    public IListFeed<Category> Categories =>
        ListFeed.Async(_categoryService.GetAll);

    public IState<Category?> SelectedCategory =>
        State<Category?>.Value(this, () => null);

    // Pattern: Save command — create entity and navigate back.
    public async ValueTask Save(CancellationToken ct)
    {
        var title = await Title;
        var description = await Description;
        var priority = await Priority;
        var category = await SelectedCategory;

        if (string.IsNullOrWhiteSpace(title)) return;

        var newItem = new TodoItem
        {
            Title = title,
            Description = description,
            Priority = priority,
            CategoryName = category?.Name,
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000099")
        };

        await _todoItemService.Create(newItem, ct);
        await _navigator.GoBack(this);
    }

    public async ValueTask Cancel(CancellationToken ct) =>
        await _navigator.GoBack(this);
}
