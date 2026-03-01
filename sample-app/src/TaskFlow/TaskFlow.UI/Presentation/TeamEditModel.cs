namespace TaskFlow.UI.Presentation;

/// <summary>
/// Team edit model — handles creating new and editing existing teams.
/// When navigated with a TeamSummary data parameter, it edits; otherwise it creates.
/// Uses the MVUX Command pattern (Given/When/Then) for save.
/// </summary>
public partial record TeamEditModel
{
    private readonly INavigator _navigator;
    private readonly ITeamService _service;
    private readonly IMessenger _messenger;
    private readonly bool _isNew;

    public TeamEditModel(
        INavigator navigator,
        ITeamService service,
        IMessenger messenger,
        TeamSummary? item = null)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
        _isNew = item is null;
        _editItem = item ?? new TeamSummary();
    }

    private readonly TeamSummary _editItem;

    /// <summary>Whether this is a new item (create) vs editing an existing one.</summary>
    public bool IsNew => _isNew;

    /// <summary>Page title — "New Team" or "Edit Team".</summary>
    public string PageTitle => _isNew ? "New Team" : "Edit Team";

    /// <summary>The item being edited — single state with two-way bound properties via MVUX proxy.</summary>
    public IState<TeamSummary> Item => State.Value(this, () => _editItem);

    /// <summary>Save command — reads current Item state via Given/When/Then pattern.</summary>
    public ICommand Save => Command.Create(b => b
        .Given(Item)
        .When(item => item is not null && !string.IsNullOrWhiteSpace(item.Name))
        .Then(DoSave));

    private async ValueTask DoSave(TeamSummary item, CancellationToken ct)
    {
        TeamSummary? result;
        if (_isNew)
        {
            item = item with { Id = Guid.NewGuid() };
            result = await _service.Create(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<TeamSummary>(EntityChange.Created, result));
        }
        else
        {
            result = await _service.Update(item, ct);
            if (result is not null)
                _messenger.Send(new EntityMessage<TeamSummary>(EntityChange.Updated, result));
        }

        await _navigator.NavigateBackAsync(this, cancellation: ct);
    }

    /// <summary>Cancel and go back.</summary>
    public async ValueTask Cancel(CancellationToken ct) =>
        await _navigator.NavigateBackAsync(this, cancellation: ct);
}
