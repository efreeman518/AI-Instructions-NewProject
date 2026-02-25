# Uno Platform UI

## Purpose

Scaffold a single Uno codebase (WASM + mobile + desktop) that calls the **Gateway** (YARP), not backend APIs directly.

- UI auth: `EntraExternal` (or configured UI auth provider)
- API auth: token relay through Gateway
- Pattern: Views (`XAML`) ↔ Presentation (`MVUX`) ↔ Business services ↔ Kiota client ↔ Gateway

References:
- [../domain-definition-schema.md](../domain-definition-schema.md)
- [../resource-implementation-schema.md](../resource-implementation-schema.md)
- `sampleapp/src/TaskFlow/TaskFlow.UI/`
- [../SKILL.md](../SKILL.md)

## Profiles

| Profile | Scope |
|---|---|
| `starter` | Host wiring, auth/http, route maps, list/detail/settings pages, MVUX models, service layer, mock/live switch |
| `full` | `starter` + richer shell, dialog/flyout routes, expanded page set and UX flows |

Prefer `starter` until core vertical slices stabilize.

## Required Structure

```text
{Project}.UI/
  App.xaml
  App.xaml.cs
  App.xaml.host.cs
  appsettings.json
  Business/
    Models/
    Services/
  Client/                 # Kiota-generated client
  Presentation/           # MVUX models
  Views/
  Styles/
  Strings/
  Converters/
```

## Packages (Minimum)

```xml
<PackageReference Include="Uno.WinUI" />
<PackageReference Include="Uno.Sdk.Private" />
<PackageReference Include="Uno.Extensions.Hosting.WinUI" />
<PackageReference Include="Uno.Extensions.Navigation.WinUI" />
<PackageReference Include="Uno.Extensions.Navigation.Toolkit.WinUI" />
<PackageReference Include="Uno.Extensions.Reactive.WinUI" />
<PackageReference Include="Uno.Extensions.Authentication.WinUI" />
<PackageReference Include="Uno.Extensions.Http.WinUI" />
<PackageReference Include="Uno.Extensions.Http.Kiota" />
<PackageReference Include="Uno.Extensions.Localization.WinUI" />
<PackageReference Include="Uno.Extensions.Logging.WinUI" />
<PackageReference Include="Uno.Extensions.Serialization" />
<PackageReference Include="Uno.Toolkit.WinUI" />
<PackageReference Include="Uno.Toolkit.WinUI.Material" />
<PackageReference Include="Uno.Material.WinUI" />
<PackageReference Include="CommunityToolkit.Mvvm" />
<PackageReference Include="Microsoft.Kiota.Abstractions" />
<PackageReference Include="Microsoft.Kiota.Http.HttpClientLibrary" />
<PackageReference Include="Microsoft.Kiota.Serialization.Json" />
```

Use central package management in `Directory.Packages.props`.

## App Host Rules (`App.xaml.host.cs`)

Configure everything through `IApplicationBuilder`:

```csharp
builder
    .UseToolkitNavigation()
    .Configure(host => host
        .UseAuthentication(auth => auth.AddCustom(..., name: "CustomAuth"))
        .UseHttp((context, services) =>
        {
            services.AddKiotaClient<{Project}ApiClient>(
                context,
                options: new EndpointOptions
                {
                    Url = context.Configuration["GatewayBaseUrl"] ?? "https://localhost:7200"
                });
        })
        .UseConfiguration(cfg => cfg.EmbeddedSource<App>())
        .UseLocalization()
        .UseSerialization(configure: ConfigureSerialization)
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<IMessenger, WeakReferenceMessenger>();
            services.AddSingleton<I{Entity}Service, {Entity}Service>();
        })
        .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes));
```

Non-negotiables:
- Always call Gateway URL from config (`GatewayBaseUrl`).
- Register auth + HTTP + navigation in one place.
- Keep service registration in host config, not in views/models.

## Mock vs Live API

Support both at scaffold time (`Features:UseMocks`).

```json
{
  "GatewayBaseUrl": "https://localhost:7200",
  "Features": { "UseMocks": false }
}
```

If mocks enabled, use a custom `HttpMessageHandler`; otherwise call live Gateway.

## MVUX Model Rules

Use partial records in `Presentation/`:
- Read: `IFeed<T>` / `IListFeed<T>`
- Mutable UI state: `IState<T>` / `IListState<T>`
- Commands: public `ValueTask` methods
- Navigation: `INavigator`
- Cross-model refresh: `IMessenger` + `.Observe(...)`

Example pattern:

```csharp
public partial record TodoItemListModel(
    INavigator Navigator,
    ITodoItemService Service,
    IMessenger Messenger)
{
    public IListFeed<TodoItemModel> Items => ListFeed.Async(Service.GetAll);

    public IState<string> SearchTerm => State<string>.Value(this, () => string.Empty);

    public IListState<TodoItemModel> FilteredItems => ListState
        .FromFeed(this, Feed.Combine(SearchTerm, Items.AsFeed())
            .SelectAsync(Filter)
            .AsListFeed())
        .Observe(Messenger, x => x.Id);

    public ValueTask OpenDetail(TodoItemModel item, CancellationToken ct) =>
        Navigator.NavigateRouteAsync(this, "TodoItemDetail", data: item, cancellation: ct);
}
```

## Routing + Mapping

- Define `ViewMap`/`DataViewMap` in one route registration method.
- Keep route names stable (`Home`, `{Entity}List`, `{Entity}Detail`, `Settings`).
- Pass selected entity as route data for detail/edit pages.

## XAML Rules

- Code-behind stays minimal (initialize + thin UI-only behaviors).
- Put visual tokens and style overrides in `Styles/` dictionaries.
- Use converters from `Converters/` for presentation-only formatting.
- Keep reusable UI in `Views/Controls` and shared templates in `Views/Templates`.

## Business Service Rules

In `Business/Services`:
- Define `I{Feature}Service` interfaces.
- Implement via Kiota client wrapper.
- Convert transport DTOs to UI models at service boundary.
- Surface `Result`/failure states usable by MVUX models.

## Auth Rules

- UI authenticates to Gateway identity provider.
- No direct API tokens in XAML or page code-behind.
- Keep auth/session handling in host/services.

## Generation Checklist

- [ ] `includeUnoUI: true` set in domain inputs
- [ ] `GatewayBaseUrl` present in `appsettings*.json`
- [ ] Auth + HTTP + navigation configured in `App.xaml.host.cs`
- [ ] `Business/Models`, `Business/Services`, `Presentation`, `Views` scaffolded
- [ ] `Features:UseMocks` implemented (mock + live path)
- [ ] Core pages scaffolded: Home, List, Detail, Settings (+ Login when auth enabled)
- [ ] Route mappings and page-model bindings compile
- [ ] UI uses Gateway endpoints only

## Related Skills

- Solution layout: [solution-structure.md](solution-structure.md)
- Gateway integration: [gateway.md](gateway.md)
- Auth setup: [identity-management.md](identity-management.md)
- Testing strategy: [testing.md](testing.md)
- App configuration: [configuration.md](configuration.md)