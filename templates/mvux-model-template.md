# MVUX Presentation Model Template

| | |
|---|---|
| **Files** | `Presentation/{Entity}ListModel.cs`, `{Entity}DetailModel.cs`, `Create{Entity}Model.cs` |
| **Depends on** | [ui-model-template](ui-model-template.md), [ui-service-template](ui-service-template.md) |
| **Referenced by** | [xaml-page-template](xaml-page-template.md), [uno-ui.md](../skills/uno-ui.md) |

## List Model

```csharp
using {Project}.UI.Business.Models;
using {Project}.UI.Business.Services.{Feature};

namespace {Project}.UI.Presentation;

public partial record {Entity}ListModel
{
    private readonly INavigator _navigator;
    private readonly I{Entity}Service _{entity}Service;
    private readonly IMessenger _messenger;

    public {Entity}ListModel(
        INavigator navigator,
        I{Entity}Service {entity}Service,
        IMessenger messenger)
    {
        _navigator = navigator;
        _{entity}Service = {entity}Service;
        _messenger = messenger;
    }

    // Read-only list feed — auto-refreshes on messenger updates
    public IListFeed<{Entity}> Items =>
        ListFeed.Async(_{entity}Service.GetAll);

    // Mutable search state
    public IState<string> SearchTerm =>
        State<string>.Value(this, () => string.Empty);

    // Filtered results combining search term + items
    public IListState<{Entity}> FilteredItems => ListState
        .FromFeed(this, Feed
            .Combine(SearchTerm, Items.AsFeed())
            .SelectAsync(Search)
            .AsListFeed())
        .Observe(_messenger, item => item.Id);

    // Navigation commands — auto-bound as commands in XAML
    public async ValueTask NavigateToDetail({Entity} item, CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "{Entity}Detail", data: item, cancellation: ct);

    public async ValueTask Create(CancellationToken ct) =>
        await _navigator.NavigateRouteAsync(this, "Create{Entity}", cancellation: ct);

    private async ValueTask<IImmutableList<{Entity}>> Search(
        (string term, IImmutableList<{Entity}> items) inputs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inputs.term))
            return inputs.items;

        return inputs.items
            .Where(x => x.Name?.Contains(inputs.term, StringComparison.OrdinalIgnoreCase) == true)
            .ToImmutableList();
    }
}
```

## Detail Model

```csharp
using {Project}.UI.Business.Models;
using {Project}.UI.Business.Services.{Feature};

namespace {Project}.UI.Presentation;

public partial record {Entity}DetailModel
{
    private readonly INavigator _navigator;
    private readonly I{Entity}Service _{entity}Service;
    private readonly IMessenger _messenger;

    public {Entity}DetailModel(
        {Entity} {entity},             // Injected via navigation data
        INavigator navigator,
        I{Entity}Service {entity}Service,
        IMessenger messenger)
    {
        _navigator = navigator;
        _{entity}Service = {entity}Service;
        _messenger = messenger;
        {Entity} = {entity};
    }

    public {Entity} {Entity} { get; }

    // Child collection feeds
    public IListFeed<{ChildEntity}> {ChildEntity}Items =>
        ListFeed.Async(async ct =>
            await _{entity}Service.Get{ChildEntity}s({Entity}.Id, ct));

    // Mutable state for UI toggle
    public IState<bool> IsFavorited =>
        State.Value(this, () => {Entity}.IsFavorite);

    // Commands
    public async ValueTask ToggleFavorite(CancellationToken ct)
    {
        await _{entity}Service.Favorite({Entity}, ct);
        await IsFavorited.UpdateAsync(s => !s);
    }

    public async ValueTask Delete(CancellationToken ct)
    {
        await _{entity}Service.Delete({Entity}.Id, ct);
        await _navigator.GoBack(this);
    }
}
```

## Create/Edit Model

```csharp
using {Project}.UI.Business.Models;
using {Project}.UI.Business.Services.{Feature};

namespace {Project}.UI.Presentation;

public partial record Create{Entity}Model
{
    private readonly INavigator _navigator;
    private readonly I{Entity}Service _{entity}Service;
    private readonly IMessenger _messenger;

    public Create{Entity}Model(
        {Entity}? {entity},            // null = create mode, non-null = edit mode
        INavigator navigator,
        I{Entity}Service {entity}Service,
        IMessenger messenger)
    {
        _navigator = navigator;
        _{entity}Service = {entity}Service;
        _messenger = messenger;
        IsEditMode = {entity} is not null;
    }

    public bool IsEditMode { get; }

    // Form fields as mutable state
    public IState<string> Name => State<string>.Value(this, () => string.Empty);
    // ... add IState<T> per editable property

    public async ValueTask Save(CancellationToken ct)
    {
        var name = await Name;
        // Build entity from form state
        var entity = new {Entity} { Name = name /* ... */ };

        if (IsEditMode)
            await _{entity}Service.Update(entity, ct);
        else
            await _{entity}Service.Create(entity, ct);

        await _navigator.GoBack(this);
    }

    public async ValueTask Cancel(CancellationToken ct) =>
        await _navigator.GoBack(this);
}
```

## Rules

- Always use `partial record` — the MVUX source generator needs it
- `{Feature}` = **plural entity name** matching the service folder (e.g., `TodoItems`, `Categories`)
- Constructor parameters are injected via DI (services, `INavigator`, `IMessenger`) or navigation data (the entity for detail pages)
- Use `IFeed`/`IListFeed` for read-only data, `IState`/`IListState` for mutable data
- Public `ValueTask` methods become bindable commands automatically
- Use `.Observe(_messenger, keySelector)` to auto-refresh when entity messages arrive
- Accept `CancellationToken ct` as the last parameter on all async methods
- Keep models focused — one model per page/dialog
