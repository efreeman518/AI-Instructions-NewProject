using Domain.Shared;

namespace TaskFlow.UI.Presentation;

/// <summary>
/// TodoItem detail model — receives selected item via navigation data.
/// Provides item state and action commands (start, complete, block, etc.).
/// </summary>
public partial record TodoItemDetailModel
{
    private readonly INavigator _navigator;
    private readonly ITodoItemService _service;
    private readonly IMessenger _messenger;

    public TodoItemDetailModel(
        TodoItemSummary item,
        INavigator navigator,
        ITodoItemService service,
        IMessenger messenger)
    {
        Item = item;
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
    }

    /// <summary>
    /// The current todo item — passed as navigation data.
    /// </summary>
    public TodoItemSummary Item { get; }

    /// <summary>
    /// Mutable state for the current item — supports refresh after actions.
    /// </summary>
    public IState<TodoItemSummary> CurrentItem => State.Value(this, () => Item);

    /// <summary>
    /// Whether the item can be started (not already started/completed/cancelled).
    /// </summary>
    public IFeed<bool> CanStart => CurrentItem.Select(i =>
        i is not null && i.Status == TodoItemStatus.None);

    /// <summary>
    /// Whether the item can be completed.
    /// </summary>
    public IFeed<bool> CanComplete => CurrentItem.Select(i =>
        i is not null && i.Status.HasFlag(TodoItemStatus.IsStarted) && !i.IsCompleted);

    public async ValueTask StartItem(CancellationToken ct)
    {
        var updated = await _service.Start(Item.Id, ct);
        if (updated is not null)
        {
            await CurrentItem.UpdateAsync(_ => updated, ct);
            _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Updated, updated));
        }
    }

    public async ValueTask CompleteItem(CancellationToken ct)
    {
        var updated = await _service.Complete(Item.Id, ct);
        if (updated is not null)
        {
            await CurrentItem.UpdateAsync(_ => updated, ct);
            _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Updated, updated));
        }
    }

    public async ValueTask BlockItem(CancellationToken ct)
    {
        var updated = await _service.Block(Item.Id, ct);
        if (updated is not null)
        {
            await CurrentItem.UpdateAsync(_ => updated, ct);
            _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Updated, updated));
        }
    }

    public async ValueTask UnblockItem(CancellationToken ct)
    {
        var updated = await _service.Unblock(Item.Id, ct);
        if (updated is not null)
        {
            await CurrentItem.UpdateAsync(_ => updated, ct);
            _messenger.Send(new EntityMessage<TodoItemSummary>(EntityChange.Updated, updated));
        }
    }

    /// <summary>
    /// Navigate to edit this todo item.
    /// </summary>
    public async ValueTask EditItem(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TodoItemEdit", data: Item, cancellation: ct);

    public async ValueTask GoBack(CancellationToken ct) =>
        await _navigator.NavigateBackAsync(this, cancellation: ct);
}
