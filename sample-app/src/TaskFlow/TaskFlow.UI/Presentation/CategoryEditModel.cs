namespace TaskFlow.UI.Presentation;

/// <summary>
/// Category edit model — handles creating new and editing existing categories.
/// When navigated with a CategorySummary data parameter, it edits; otherwise it creates.
/// Uses the MVUX Command pattern (Given/When/Then) for save.
/// </summary>
public partial record CategoryEditModel
{
    private readonly INavigator _navigator;
    private readonly ICategoryService _service;
    private readonly IMessenger _messenger;
    private readonly bool _isNew;

    public CategoryEditModel(
        INavigator navigator,
        ICategoryService service,
        IMessenger messenger,
        CategorySummary? item = null)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
        _isNew = item is null;
        _editItem = item ?? new CategorySummary();
    }

    private readonly CategorySummary _editItem;

    /// <summary>Whether this is a new item (create) vs editing an existing one.</summary>
    public bool IsNew => _isNew;

    /// <summary>Page title — "New Category" or "Edit Category".</summary>
    public string PageTitle => _isNew ? "New Category" : "Edit Category";

    /// <summary>The item being edited — single state with two-way bound properties via MVUX proxy.</summary>
    public IState<CategorySummary> Item => State.Value(this, () => _editItem);

    /// <summary>Save command — reads current Item state via Given/When/Then pattern.</summary>
    public ICommand Save => Command.Create(b => b
        .Given(Item)
        .When(item => item is not null && !string.IsNullOrWhiteSpace(item.Name))
        .Then(DoSave));

    private async ValueTask DoSave(CategorySummary item, CancellationToken ct)
    {
        CategorySummary? result;
        if (_isNew)
        {
            item = item with { Id = Guid.NewGuid() };
            result = await _service.Create(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<CategorySummary>(EntityChange.Created, result));
        }
        else
        {
            result = await _service.Update(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<CategorySummary>(EntityChange.Updated, result));
        }

        await _navigator.NavigateBackAsync(this, cancellation: ct);
    }

    /// <summary>Cancel and go back.</summary>
    public async ValueTask Cancel(CancellationToken ct) =>
        await _navigator.NavigateBackAsync(this, cancellation: ct);
}
