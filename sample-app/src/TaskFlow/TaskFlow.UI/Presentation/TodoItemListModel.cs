namespace TaskFlow.UI.Presentation;

/// <summary>
/// TodoItem list model — search/filter with MVUX reactive feeds.
/// Follows the Chefs SearchModel pattern: IListState with Observe for cross-model refresh,
/// Feed.Combine for composing search term with data feed.
/// </summary>
public partial record TodoItemListModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _service;
    private readonly IMessenger _messenger;

    public TodoItemListModel(INavigator navigator, ITodoItemService service, IMessenger messenger)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
    }

    /// <summary>
    /// Search term bound to the search box.
    /// </summary>
    public IState<string> SearchTerm => State<string>.Value(this, () => string.Empty);

    /// <summary>
    /// All items — base data feed.
    /// </summary>
    public IListFeed<TodoItemSummary> AllItems => ListFeed.Async(_service.GetAll);

    /// <summary>
    /// Filtered items — combines search term with all items, observed for cross-model updates.
    /// </summary>
    public IListState<TodoItemSummary> Items => ListState
        .FromFeed(this, Feed.Combine(SearchTerm, AllItems.AsFeed())
            .SelectAsync(FilterItems)
            .AsListFeed())
        .Observe(_messenger, x => x.Id);

    /// <summary>
    /// Navigate to todo item detail view.
    /// </summary>
    public async ValueTask OpenDetail(TodoItemSummary item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TodoItemDetail", data: item, cancellation: ct);

    /// <summary>
    /// Navigate to create a new todo item.
    /// </summary>
    public async ValueTask CreateItem(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TodoItemEdit", cancellation: ct);

    /// <summary>
    /// Delete a todo item.
    /// </summary>
    public async ValueTask DeleteItem(TodoItemSummary item, CancellationToken ct)
    {
        await _service.Delete(item.Id, ct);
        _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Deleted, item));
    }

    private static async ValueTask<IImmutableList<TodoItemSummary>> FilterItems(
        (string? Search, IImmutableList<TodoItemSummary> Items) input,
        CancellationToken ct)
    {
        await ValueTask.CompletedTask;
        var (search, items) = input;

        if (string.IsNullOrWhiteSpace(search))
            return items;

        return items
            .Where(i => i.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                     || (i.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                     || (i.CategoryName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToImmutableList();
    }
}
