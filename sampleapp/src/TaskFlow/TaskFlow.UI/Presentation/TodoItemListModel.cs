// ═══════════════════════════════════════════════════════════════
// Pattern: MVUX List Model — TodoItem list with search, filter, and navigation.
// Uses IListFeed<T> for read-only data, IState<T> for mutable search term,
// IListState<T> for filtered results with .Observe() for auto-refresh.
//
// Key MVUX concepts demonstrated:
// 1. IListFeed<T> → read-only async data source
// 2. IState<T> → two-way bindable mutable state
// 3. Feed.Combine() → merge multiple reactive sources
// 4. .Observe(_messenger, ...) → auto-refresh on EntityMessage
// 5. Public ValueTask methods → auto-generated as ICommand
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Business.Services.TodoItems;

namespace TaskFlow.UI.Presentation;

/// <summary>
/// Pattern: MVUX partial record — the source generator creates the bindable proxy.
/// NOT a ViewModel — MVUX models are records with reactive Feed/State properties.
/// </summary>
public partial record TodoItemListModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _todoItemService;
    private readonly IMessenger _messenger;

    public TodoItemListModel(
        INavigator navigator,
        ITodoItemService todoItemService,
        IMessenger messenger)
    {
        _navigator = navigator;
        _todoItemService = todoItemService;
        _messenger = messenger;
    }

    // Pattern: IListFeed<T> — read-only async feed, auto-refreshes when data changes.
    public IListFeed<TodoItem> Items =>
        ListFeed.Async(_todoItemService.GetAll);

    // Pattern: IState<T> — mutable state bound two-way from the search TextBox.
    public IState<string> SearchTerm =>
        State<string>.Value(this, () => string.Empty);

    // Pattern: IListState<T> — filtered results combining state + feed.
    // .Observe() triggers refresh when EntityMessage<TodoItem> is received.
    public IListState<TodoItem> FilteredItems => ListState
        .FromFeed(this, Feed
            .Combine(SearchTerm, Items.AsFeed())
            .SelectAsync(Search)
            .AsListFeed())
        .Observe(_messenger, item => item.Id);

    // Pattern: Public ValueTask → auto-generated as ICommand for XAML binding.
    public async ValueTask NavigateToDetail(TodoItem item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TodoItemDetail", data: item, cancellation: ct);

    public async ValueTask Create(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "CreateTodoItem", cancellation: ct);

    public async ValueTask ToggleComplete(TodoItem item, CancellationToken ct) =>
        await _todoItemService.ToggleComplete(item, ct);

    // Pattern: Private search logic — pure filter, no side effects.
    private async ValueTask<IImmutableList<TodoItem>> Search(
        (string term, IImmutableList<TodoItem> items) inputs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inputs.term))
            return inputs.items;

        return inputs.items
            .Where(x => x.Title?.Contains(inputs.term, StringComparison.OrdinalIgnoreCase) == true
                     || x.Description?.Contains(inputs.term, StringComparison.OrdinalIgnoreCase) == true)
            .ToImmutableList();
    }
}
