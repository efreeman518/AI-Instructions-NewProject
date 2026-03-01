namespace TaskFlow.UI.Presentation;

/// <summary>
/// Category list model — displays all categories with MVUX reactive feeds.
/// </summary>
public partial record CategoryListModel
{
    private readonly INavigator _navigator;
    private readonly ICategoryService _service;
    private readonly IMessenger _messenger;

    public CategoryListModel(INavigator navigator, ICategoryService service, IMessenger messenger)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
    }

    /// <summary>
    /// All categories — observed for cross-model refresh.
    /// </summary>
    public IListState<CategorySummary> Items => ListState
        .Async(this, _service.GetAll)
        .Observe(_messenger, x => x.Id);

    /// <summary>
    /// Delete a category.
    /// </summary>
    public async ValueTask DeleteItem(CategorySummary item, CancellationToken ct)
    {
        await _service.Delete(item.Id, ct);
        _messenger.Send(new EntityMessage<CategorySummary>(EntityChange.Deleted, item));
    }

    /// <summary>
    /// Navigate to create a new category.
    /// </summary>
    public async ValueTask CreateItem(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "CategoryEdit", cancellation: ct);

    /// <summary>
    /// Navigate to edit an existing category.
    /// </summary>
    public async ValueTask EditItem(CategorySummary item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "CategoryEdit", data: item, cancellation: ct);
}
