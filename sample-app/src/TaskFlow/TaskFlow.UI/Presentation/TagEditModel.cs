namespace TaskFlow.UI.Presentation;

/// <summary>
/// Tag edit model — handles creating new and editing existing tags.
/// When navigated with a TagSummary data parameter, it edits; otherwise it creates.
/// Uses the MVUX Command pattern (Given/When/Then) for save.
/// </summary>
public partial record TagEditModel
{
    private readonly INavigator _navigator;
    private readonly ITagService _service;
    private readonly IMessenger _messenger;
    private readonly bool _isNew;

    public TagEditModel(
        INavigator navigator,
        ITagService service,
        IMessenger messenger,
        TagSummary? item = null)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
        _isNew = item is null;
        _editItem = item ?? new TagSummary();
    }

    private readonly TagSummary _editItem;

    /// <summary>Whether this is a new item (create) vs editing an existing one.</summary>
    public bool IsNew => _isNew;

    /// <summary>Page title — "New Tag" or "Edit Tag".</summary>
    public string PageTitle => _isNew ? "New Tag" : "Edit Tag";

    /// <summary>The item being edited — single state with two-way bound properties via MVUX proxy.</summary>
    public IState<TagSummary> Item => State.Value(this, () => _editItem);

    /// <summary>Save command — reads current Item state via Given/When/Then pattern.</summary>
    public ICommand Save => Command.Create(b => b
        .Given(Item)
        .When(item => item is not null && !string.IsNullOrWhiteSpace(item.Name))
        .Then(DoSave));

    private async ValueTask DoSave(TagSummary item, CancellationToken ct)
    {
        TagSummary? result;
        if (_isNew)
        {
            item = item with { Id = Guid.NewGuid() };
            result = await _service.Create(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<TagSummary>(EntityChange.Created, result));
        }
        else
        {
            result = await _service.Update(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<TagSummary>(EntityChange.Updated, result));
        }

        await _navigator.NavigateBackAsync(this, cancellation: ct);
    }

    /// <summary>Cancel and go back.</summary>
    public async ValueTask Cancel(CancellationToken ct) =>
        await _navigator.NavigateBackAsync(this, cancellation: ct);
}
