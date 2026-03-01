namespace TaskFlow.UI.Presentation;

/// <summary>
/// Team list model — displays all teams with MVUX reactive feeds.
/// </summary>
public partial record TeamListModel
{
    private readonly INavigator _navigator;
    private readonly ITeamService _service;
    private readonly IMessenger _messenger;

    public TeamListModel(INavigator navigator, ITeamService service, IMessenger messenger)
    {
        _navigator = navigator;
        _service = service;
        _messenger = messenger;
    }

    /// <summary>
    /// All teams — observed for cross-model refresh.
    /// </summary>
    public IListState<TeamSummary> Items => ListState
        .Async(this, _service.GetAll)
        .Observe(_messenger, x => x.Id);

    /// <summary>
    /// Delete a team.
    /// </summary>
    public async ValueTask DeleteItem(TeamSummary item, CancellationToken ct)
    {
        await _service.Delete(item.Id, ct);
        _messenger.Send(new EntityMessage<TeamSummary>(EntityChange.Deleted, item));
    }

    /// <summary>
    /// Navigate to create a new team.
    /// </summary>
    public async ValueTask CreateItem(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TeamEdit", cancellation: ct);

    /// <summary>
    /// Navigate to edit an existing team.
    /// </summary>
    public async ValueTask EditItem(TeamSummary item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "TeamEdit", data: item, cancellation: ct);
}
