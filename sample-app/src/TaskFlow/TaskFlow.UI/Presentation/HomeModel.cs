namespace TaskFlow.UI.Presentation;

/// <summary>
/// Home model — dashboard with summary data.
/// Shows recent/upcoming todo items and quick stats.
/// </summary>
public partial record HomeModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _todoItemService;
    private readonly IMessenger _messenger;

    public HomeModel(INavigator navigator, ITodoItemService todoItemService, IMessenger messenger)
    {
        _navigator = navigator;
        _todoItemService = todoItemService;
        _messenger = messenger;
    }

    /// <summary>
    /// All todo items for dashboard display — observed for cross-model refresh.
    /// </summary>
    public IListState<TodoItemSummary> RecentItems => ListState
        .Async(this, _todoItemService.GetAll)
        .Observe(_messenger, x => x.Id);

    /// <summary>
    /// Navigate to a specific todo item detail.
    /// </summary>
    public async ValueTask OpenTodoItem(TodoItemSummary item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TodoItemDetail", data: item, cancellation: ct);

    /// <summary>
    /// Navigate to the full todo item list.
    /// </summary>
    public async ValueTask ViewAllTodoItems(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TodoItems", cancellation: ct);
}
