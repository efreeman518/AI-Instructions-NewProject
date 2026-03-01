namespace TaskFlow.UI.Presentation;

/// <summary>
/// Tag list model — displays all tags with MVUX reactive feeds.
/// </summary>
public partial record TagListModel
{
    private readonly INavigator _navigator;
    private readonly ITagService _service;
    private readonly IMessenger _messenger;

    public TagListModel(INavigator navigator, ITagService service, IMessenger messenger)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
    }

    /// <summary>
    /// All tags — observed for cross-model refresh.
    /// </summary>
    public IListState<TagSummary> Items => ListState
        .Async(this, _service.GetAll)
        .Observe(_messenger, x => x.Id);

    /// <summary>
    /// Delete a tag.
    /// </summary>
    public async ValueTask DeleteItem(TagSummary item, CancellationToken ct)
    {
        await _service.Delete(item.Id, ct);
        _messenger.Send(new EntityMessage<TagSummary>(EntityChange.Deleted, item));
    }

    /// <summary>
    /// Navigate to create a new tag.
    /// </summary>
    public async ValueTask CreateItem(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TagEdit", cancellation: ct);

    /// <summary>
    /// Navigate to edit an existing tag.
    /// </summary>
    public async ValueTask EditItem(TagSummary item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TagEdit", data: item, cancellation: ct);
}
