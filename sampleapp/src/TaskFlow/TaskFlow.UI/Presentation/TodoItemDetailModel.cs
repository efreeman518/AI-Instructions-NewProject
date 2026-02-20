// ═══════════════════════════════════════════════════════════════
// Pattern: MVUX Detail Model — receives entity via navigation data.
// DataViewMap<Page, Model, DataType> injects the entity into constructor.
// Demonstrates mutable state (IState<bool>), child feeds, and back navigation.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Business.Services.TodoItems;

namespace TaskFlow.UI.Presentation;

public partial record TodoItemDetailModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _todoItemService;
    private readonly IMessenger _messenger;

    /// <summary>
    /// Pattern: Entity injected via navigation data — DataViewMap passes the selected TodoItem.
    /// </summary>
    public TodoItemDetailModel(
        TodoItem todoItem,          // Injected by DataViewMap navigation
        INavigator navigator,
        ITodoItemService todoItemService,
        IMessenger messenger)
    {
        _navigator = navigator;
        _todoItemService = todoItemService;
        _messenger = messenger;
        TodoItem = todoItem;
    }

    /// <summary>The entity being viewed/edited.</summary>
    public TodoItem TodoItem { get; }

    // Pattern: Mutable state for UI interaction — toggleable completion.
    public IState<bool> IsCompleted =>
        State.Value(this, () => TodoItem.IsCompleted);

    // Pattern: Command — toggle completion and broadcast change.
    public async ValueTask ToggleComplete(CancellationToken ct)
    {
        await _todoItemService.ToggleComplete(TodoItem, ct);
        await IsCompleted.UpdateAsync(current => !current);
    }

    // Pattern: Command — delete with back navigation.
    public async ValueTask Delete(CancellationToken ct)
    {
        await _todoItemService.Delete(TodoItem.Id, ct);
        await _navigator.GoBack(this);
    }

    // Pattern: Back navigation command.
    public async ValueTask GoBack(CancellationToken ct) =>
        await _navigator.GoBack(this);
}
