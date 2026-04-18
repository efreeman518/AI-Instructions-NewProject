# Uno Platform UI

## Purpose

Scaffold a single Uno codebase (WASM + mobile + desktop) that calls the **Gateway** (YARP), not backend APIs directly.

- UI auth: `EntraExternal` (or configured UI auth provider)
- API auth: token relay through Gateway
- Pattern: Views (`XAML`) ↔ Presentation (`MVUX`) ↔ Business services ↔ Kiota client ↔ Gateway

References:
- [../ai/domain-specification-schema.md](../ai/domain-specification-schema.md)
- [../ai/resource-implementation-schema.md](../ai/resource-implementation-schema.md)
- See [../patterns/expected-output-index.md](../patterns/expected-output-index.md).
- [../ai/SKILL.md](../ai/SKILL.md)
- Reference app: [Uno Chefs](https://github.com/unoplatform/uno.chefs) — canonical Uno example for MVUX, navigation, Kiota HTTP, and page structure

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

> **Important:** With Uno.Sdk, you do NOT list individual Uno packages. The SDK resolves them
> automatically from `<UnoFeatures>`. The list below is for reference only — to understand what
> `UnoFeatures` maps to. Do NOT add these `PackageReference` entries to the csproj.

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

## Project File Rules (`.csproj`)

Uno Platform uses an **MSBuild SDK package** (`Uno.Sdk`), not a .NET workload. Never run `dotnet workload install uno-*`.

### Canonical csproj Structure

```xml
<Project Sdk="Uno.Sdk/{VERSION}">
  <PropertyGroup>
    <!-- Clear singular TargetFramework inherited from Directory.Build.props -->
    <TargetFramework />
    <TargetFrameworks>net10.0-browserwasm</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UnoSingleProject>true</UnoSingleProject>
    <ApplicationTitle>{AppName}</ApplicationTitle>
    <ApplicationId>com.{company}.{app}</ApplicationId>

    <UnoFeatures>
      Material;
      Hosting;
      Toolkit;
      Logging;
      MVUX;
      Configuration;
      HttpKiota;
      Serialization;
      Localization;
      Navigation;
      ThemeService;
      Authentication;
    </UnoFeatures>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="System.Collections.Immutable" />
  </ItemGroup>
</Project>
```

### Non-Negotiable csproj Rules

1. **SDK version**: Use `Uno.Sdk/6.5.x` or later for .NET 9/10 projects. The 6.0.x line bundles `Uno.Wasm.Bootstrap 8.0.x` which does NOT support .NET 9+.
2. **TargetFramework clearing**: When `Directory.Build.props` sets `<TargetFramework>net10.0</TargetFramework>` for non-Uno projects, the Uno csproj MUST add `<TargetFramework />` before `<TargetFrameworks>` to clear the inherited singular value. Otherwise MSBuild merges both, causing `NETSDK1005`.
3. **Entry point**: Uno SDK may not auto-generate `Program.Main` on .NET 10. Always include a manual `Program.cs`:

```csharp
namespace {Project}.Uno;

public class Program
{
    private static App? _app;
    static int Main(string[] args)
    {
        Microsoft.UI.Xaml.Application.Start(_ => _app = new App());
        return 0;
    }
}
```

4. **Global using for ImmutableList**: MVUX `IListFeed<T>` requires `IImmutableList<T>`. Add `<Using Include="System.Collections.Immutable" />` to the Uno csproj.
5. **Aspire AppHost reference**: The AppHost may fail to build if it has a direct `ProjectReference` to the Uno project (SDK resolution conflict). If this occurs, remove the reference and register the Uno project as an external executable.

### Testable Core Library

Extract `Business/` (Models, Services) and `Client/` into a separate `{Project}.Uno.Core` class library targeting plain `net10.0`. This allows unit testing without the Uno SDK.

```text
{Project}.Uno.Core/          <- net10.0 class lib (testable)
  Business/Models/
  Business/Services/
  Client/
{Project}.Uno/               <- Uno.Sdk (WASM)
  App.xaml, App.xaml.cs, App.xaml.host.cs
  Presentation/              <- MVUX models
  Views/                     <- XAML pages
  references {Project}.Uno.Core
```

After extracting, **delete** the original files from the Uno project — do not leave duplicates.

## App.xaml Rules

The `App.xaml` base class is `Application`, NOT `utu:App` (which doesn't exist):

```xml
<Application x:Class="{Namespace}.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
                <MaterialToolkitTheme xmlns="using:Uno.Toolkit.UI.Material" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### App.xaml Non-Negotiables

- Base element: `<Application>` — never `<utu:App>` or `<toolkit:App>`
- `MaterialToolkitTheme` namespace: `using:Uno.Toolkit.UI.Material` — NOT `using:Uno.Material`
- Do NOT add `<ToolkitResources xmlns="using:Uno.Toolkit.UI" />` as a separate merged dictionary (included via `MaterialToolkitTheme`)

### App.xaml.cs Pattern

Follow the Uno Chefs reference app pattern:

```csharp
using Microsoft.Extensions.Hosting;
using {Project}.Uno.Views;

namespace {Project}.Uno;

public partial class App : Application
{
    public static Window? MainWindow;
    public static IHost? Host { get; private set; }

    public App() => this.InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args);
        ConfigureAppBuilder(builder);
        MainWindow = builder.Window;
        Host = await builder.NavigateAsync<Shell>();
    }
}
```

### Shell Control

Create a `Shell.xaml` UserControl with `ExtendedSplashScreen` as the loading container. The `Content` property MUST hold a `<Frame />` — this is what Uno Extensions Navigation writes page content into. The `LoadingContentTemplate` shows the spinner while the host boots. Both are required.

```xml
<UserControl x:Class="{Namespace}.Views.Shell"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:utu="using:Uno.Toolkit.UI">
    <utu:ExtendedSplashScreen x:Name="Splash"
                              HorizontalAlignment="Stretch"
                              VerticalAlignment="Stretch"
                              HorizontalContentAlignment="Stretch"
                              VerticalContentAlignment="Stretch">
        <utu:ExtendedSplashScreen.LoadingContentTemplate>
            <DataTemplate>
                <Grid>
                    <ProgressRing IsActive="True"
                                  VerticalAlignment="Center"
                                  HorizontalAlignment="Center"
                                  Height="60" Width="60" />
                </Grid>
            </DataTemplate>
        </utu:ExtendedSplashScreen.LoadingContentTemplate>

        <utu:ExtendedSplashScreen.Content>
            <Frame />
        </utu:ExtendedSplashScreen.Content>
    </utu:ExtendedSplashScreen>
</UserControl>
```

`Shell.xaml.cs` MUST implement `IContentControlProvider` — this is how `NavigateAsync<Shell>()` locates the `Frame` for page navigation. Without it, the app starts but never renders any page content.

```csharp
using Microsoft.UI.Xaml.Controls;
using Uno.Toolkit.UI;
using Uno.UI.Extensions;

namespace {Namespace}.Views;

public sealed partial class Shell : UserControl, IContentControlProvider
{
    public ExtendedSplashScreen SplashScreen => Splash;
    public ContentControl ContentControl => Splash;
    public Frame? RootFrame => Splash.FindFirstDescendant<Frame>();

    public Shell() => this.InitializeComponent();
}
```

`ShellModel` MUST navigate to the first route on startup. Without this the Frame sits empty after the splash screen clears.

```csharp
using Uno.Extensions.Navigation;

namespace {Namespace}.Presentation;

public partial record ShellModel
{
    private readonly INavigator _navigator;

    public ShellModel(INavigator navigator)
    {
        _navigator = navigator;
        _ = Start();
    }

    public async Task Start() => await _navigator.NavigateRouteAsync(this, "Main/{FirstPage}");
}
```

### Shell Non-Negotiables

- `ExtendedSplashScreen.Content` contains `<Frame />` — **never omit this**. The app will appear to load (spinner disappears) but show a blank screen because there is no navigation target.
- Shell code-behind implements `IContentControlProvider` — required by `NavigateAsync<Shell>()` to bind the `Frame`.
- `ShellModel` calls `NavigateRouteAsync` in the constructor (fire-and-forget via `_ = Start()`) — this triggers the first navigation immediately after the host finishes loading.

Configure everything through `IApplicationBuilder`:

```csharp
builder
    .UseToolkitNavigation()
    .Configure(host => host
        .UseAuthentication(auth => auth.AddCustom(..., name: "CustomAuth"))
        .UseHttp((context, services) =>
        {
            var gatewayUrl = context.Configuration["GatewayBaseUrl"] ?? "https://localhost:7200";

            // If the client was generated by Kiota (takes IRequestAdapter), use AddKiotaClient:
            //   services.AddKiotaClient<{Project}ApiClient>(context, options: new EndpointOptions { Url = gatewayUrl });
            //
            // If the client is a hand-written stub (takes HttpClient directly), use AddHttpClient:
            services
                .AddHttpClient<{Project}ApiClient>(client => client.BaseAddress = new Uri(gatewayUrl))
#if USE_MOCKS
                .ConfigurePrimaryHttpMessageHandler<MockHttpMessageHandler>()
#endif
            ;
        })
        .UseConfiguration(cfg => cfg.EmbeddedSource<App>())
        .UseLocalization()
        .UseSerialization(configure: ConfigureSerialization)
        .ConfigureServices((context, services) =>
        {
            // Use StrongReferenceMessenger — MVUX partial-record recipients registered
            // via Messenger.Register<TRecipient, TMessage> are not held alive elsewhere
            // and get GC'd under WeakReferenceMessenger. Cross-model refresh then stops
            // firing with no error.
            services.AddSingleton<IMessenger, StrongReferenceMessenger>();
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

### Feed Refresh After Mutations (Version Counter Pattern)

MVUX feeds are pull-based — they do not re-evaluate automatically when the underlying data changes. After a create/update/delete operation, the UI stays stale unless the feed is explicitly invalidated.

**Pattern:** Add an `IState<int>` version counter. Increment it after every mutation. Make the feed depend on the counter so it re-evaluates.

```csharp
public partial record CategoryTreeModel(
    INavigator Navigator,
    ICategoryApiService CategoryService)
{
    // Version counter — increment after any mutation
    public IState<int> CategoriesVersion => State<int>.Value(this, () => 0);

    // Feed depends on version — re-evaluates when version changes
    public IListFeed<CategoryModel> Categories => ListFeed.Async(async ct =>
    {
        _ = await CategoriesVersion;  // creates dependency
        return (await CategoryService.SearchAsync(ct: ct)).ToImmutableList();
    });

    public async ValueTask SaveCategory(CancellationToken ct)
    {
        await CategoryService.CreateAsync(/* ... */, ct);
        await CategoriesVersion.Update(v => v + 1, ct);  // triggers feed refresh
    }
}
```

**Rules:**
- One version counter per independent feed (e.g., `CommentsVersion`, `ChecklistVersion`, `AttachmentsVersion` on a detail model).
- `_ = await CategoriesVersion;` inside the feed lambda creates the dependency. Without it, incrementing the counter does nothing.
- Increment the counter as the **last step** of every mutation method (`Create`, `Update`, `Delete`, `Toggle`).
- For cross-model refresh (e.g., creating a task in `TaskFormModel` should refresh `TaskListModel`), use `IMessenger` with `.Observe(...)` instead of version counters.

### Cross-Model Refresh via Messenger

When a mutation in model A (e.g. Task Save) must refresh model B (e.g. Task List) and B is already constructed on a visible page, `IListFeed` + version counter doesn't help — the receiving model needs a push. Pattern:

1. Define a marker record message: `public sealed record TaskItemsChangedMessage(bool ResetToFirstPage = false);`
2. Receiver registers in its constructor (explicit ctor required — positional-record ctor has no body):
   ```csharp
   Messenger.Register<TaskListModel, TaskItemsChangedMessage>(this, static (recipient, msg) =>
   {
       if (msg.ResetToFirstPage) _ = recipient.LoadPageAsync(1);
       else                      _ = recipient.RefreshAsync();
   });
   ```
3. Sender sends after save: `Messenger.Send(new TaskItemsChangedMessage(ResetToFirstPage: true));`

Non-negotiables:
- Registered with **StrongReferenceMessenger** (see host wiring above) — weak references let records get collected.
- Receive handler is `static` and takes `recipient` — closing over `this` defeats weak-reference safety and produces analyzer warnings.
- Parameter name in the lambda must not be `_` (conflicts with discard in some generator paths). Use `msg` / `message`.

### Buffered Child Items in Create Mode

When a form can add/check child items (checklist, attachments, comments) before the parent entity has been saved, give each buffered item a client-generated `Guid.NewGuid()` Id so the UI can match items by Id immediately. On Save:

1. Persist the parent and receive its server-assigned Id.
2. For each buffered child, send a Create call with `item with { Id = null, ParentId = newParentId }` so the server assigns its own Id.
3. Mutations on buffered items (e.g. toggle checked) must only hit the server if the parent is already persisted. Gate with `if (Entity?.Id is not null)` — otherwise the server returns 404 on a non-existent parent and the UI rolls back.

```csharp
public async ValueTask ToggleChecklistItem(ChecklistItemModel item, CancellationToken ct)
{
    var updated = item with { IsCompleted = !item.IsCompleted };

    // Update UI state first — this is the source of truth while buffered.
    await ChecklistItems.UpdateAsync((IImmutableList<ChecklistItemModel>? list) =>
    {
        var src = list ?? ImmutableList<ChecklistItemModel>.Empty;
        var idx = FindById(src, item.Id);
        return idx < 0 ? src : src.SetItem(idx, updated);
    }, CancellationToken.None);

    // Only persist if the parent already exists server-side.
    if (Entity?.Id is not null)
    {
        try { await ChecklistItemService.UpdateAsync(updated, ct); }
        catch { /* state rollback not required — buffered flow survives */ }
    }
}
```

### Menu Navigation: Always Land On Top Page (Absolute Routes)

A persistent side-nav / bottom-tab menu must land on the **top** page regardless of any sub-page stacked in the content region. `uen:Navigation.Request="TaskList"` and `NavigateViewModelAsync<T>()` both respect the current navigation stack — clicking "Dashboard" from a stacked `TaskItem` detail can silently fall through or unwind instead of returning to Dashboard.

**Pattern**: Menu buttons use a `Click` handler that invokes an **absolute route** with the `/` root qualifier via code-behind. Sub-page-to-sub-page navigation (e.g., list row → detail) can still use `uen:Navigation.Request` — the absolute menu route overrides any stack state.

```xml
<!-- MainPage.xaml -->
<Button Click="NavigateTopClick" Tag="Dashboard" ...>
<Button Click="NavigateTopClick" Tag="TaskList" ...>
<Button Click="NavigateTopClick" Tag="Settings" ...>
```

```csharp
// MainPage.xaml.cs
private async void NavigateTopClick(object sender, RoutedEventArgs e)
{
    if (sender is not FrameworkElement element ||
        element.Tag is not string tag) return;
    var navigator = element.Navigator() ?? this.Navigator();
    if (navigator is null) return;

    // Leading "/" = root qualifier → bypasses any sub-page stack
    var route = tag switch
    {
        "Dashboard" => "/Main/Dashboard",
        "TaskList"  => "/Main/TaskList",
        "Settings"  => "/Settings",
        _ => null,
    };
    if (route is null) return;
    await navigator.NavigateRouteAsync(element, route);
}
```

Route qualifiers: `/` = root (absolute), `-/` = back-then-forward, `!` = dialog, `./` = current scope. Only `/` reliably escapes a stacked content region.

### URL Sync: Disable AddressBarUpdateEnabled (Chefs Pattern)

The default address-bar update pipeline can lag or lock on a prior route after nested navigation, leaving the browser URL stuck at e.g. `/Main/Categories` while the content has moved on. Match the Chefs reference app by disabling it:

```csharp
.UseNavigation(
    ReactiveViewModelMappings.ViewModelMappings,
    RegisterRoutes,
    configure: navConfig => navConfig with { AddressBarUpdateEnabled = false });
```

The app is still fully navigable via the in-app menu; only the browser URL stops reflecting internal route changes. This is the documented trade-off in Chefs.

### Navigation: Prefer NavigateBackAsync Over Hardcoded Routes

Save/Cancel actions should use `Navigator.NavigateBackAsync(this)` instead of `Navigator.NavigateRouteAsync(this, "Dashboard")`. Hardcoded route names break when the user arrives from a different page (e.g., navigating to TaskForm from TaskList vs Dashboard).

```csharp
// ✅ Correct — returns to wherever the user came from
await Navigator.NavigateBackAsync(this, cancellation: ct);

// ❌ Wrong — always goes to Dashboard even if user came from TaskList
await Navigator.NavigateRouteAsync(this, "Dashboard", cancellation: ct);
```

### ListView Item Click Navigation

To make ListView items navigable (e.g., clicking a task row opens its detail page), wrap the item template content in a `Button` with `uen:Navigation.Request` and `uen:Navigation.Data`:

```xml
<ListView ItemsSource="{Binding Data.RecentActivity}" SelectionMode="None">
    <ListView.ItemTemplate>
        <DataTemplate>
            <Button uen:Navigation.Request="{Entity}Item"
                    uen:Navigation.Data="{Binding}"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Stretch"
                    Background="Transparent"
                    BorderThickness="0"
                    Padding="0" Margin="0,3">
                <!-- Item visual content here -->
            </Button>
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```

**Do not** rely on `ListView.SelectionChanged` or `ItemClick` for navigation — these are less reliable with MVUX data binding than the declarative `uen:Navigation.Request` approach.

### MVUX Pitfalls

- **`Feed.Async` type inference**: `Feed.Async(service.GetAsync)` may fail with CS0411/CS0453 when the return type is a reference type or the delegate signature is ambiguous. Always use an explicit lambda: `Feed.Async(async ct => await service.GetAsync(ct))`.
- **`IListFeed` return type**: `ListFeed.Async(...)` callbacks must return `IImmutableList<T>`. Call `.ToImmutableList()` on results. Requires `using System.Collections.Immutable;` (add as global using in csproj, see Project File Rules).
- **Nullable state**: `IState<T?>` with `State.UpdateAsync` produces CS8714 warnings. Suppress with `#nullable disable` in the record or accept the warning — it's cosmetic.
- **No optional parameters on command methods**: The MVUX Bindable generator emits a compile error like `CS0103: The name 'False' does not exist` when a command method has an optional parameter (e.g. `public ValueTask Refresh(bool hard = false, CancellationToken ct = default)`). Split into two methods (`Refresh(ct)` + `RefreshHard(ct)`) or take the parameter as state instead. CancellationToken default is fine.
- **`IListState<T>.UpdateAsync` overload ambiguity**: There are two overloads — one updates a single item, one updates the whole list. When you pass a bare lambda the compiler picks the single-item one and you get `'IImmutableList<T>' does not contain 'FindIndex'` or `Operator '??' cannot be applied...`. Explicitly type the parameter to disambiguate:
  ```csharp
  await Items.UpdateAsync((IImmutableList<ChildModel>? list) =>
  {
      var src = list ?? ImmutableList<ChildModel>.Empty;
      var idx = /* find */;
      return idx < 0 ? src : src.SetItem(idx, updated);
  }, CancellationToken.None);
  ```
- **Don't cancel state writes with the command's CT**: MVUX commands flip `IsEnabled` bindings during execution, which can cascade back and cancel the command's own `CancellationToken` mid-state-update, leaving state half-written. For `UpdateAsync` calls that must complete, pass `CancellationToken.None`. Only use the command CT on the outbound HTTP / service call.
- **Command-CT cancels mid-HTTP**: Same mechanism cancels the HTTP call to the server. If a state update during the request flips an IsEnabled binding, the command CT is cancelled and the fetch aborts. For navigation-triggering commands (Save/Update), pass `CancellationToken.None` to the service call too.
- **Prefer individual `IState<T>` fields over a wrapped result object**: Binding a XAML pager/grid to `Data.PageNumber`, `Data.HasNextPage`, etc. via a single `IState<PageResult>` breaks: the generated bindable surfaces `Data` as a snapshot and child paths don't re-evaluate on partial updates. Declare one `IState<int> PageNumber`, `IState<bool> HasNextPage`, `IListState<T> Items`, etc. and update each explicitly in the load method.

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
- Keep route names stable (`Home`, `{Entity}List`, `{Entity}Item`, `Settings`).
- **One route per entity** for add/edit: `{Entity}Item` receives optional entity data (null = create, non-null = edit).
- Pass selected entity as route data for the entity page.

### Route Architecture (Shell vs MainPage)

Shell and MainPage serve **distinct roles** in the navigation hierarchy. Mixing them breaks the navigation bootstrap.

| Layer | View | Contains | Route binding |
|-------|------|----------|---------------|
| Root | `Shell` | `ExtendedSplashScreen` + `Frame` only | Viewless `ShellModel` at root `""` |
| Chrome | `MainPage` | Header bar, sidebar/bottom `TabBar`, content `Frame` or region | `ViewMap<MainPage, MainModel>` nested under root, `IsDefault: true` |
| Content | Page views | Page-specific UI | Nested under `Main` route |

**Critical**: Shell must NOT contain app chrome (headers, tabs, navigation bars). The `ExtendedSplashScreen` manages the navigation bootstrap chain — placing chrome in Shell means it either gets covered by the splash screen or, when the splash is removed, the navigation Frame stops receiving page content.

**Why chrome cannot live inside `Splash.Content`**: Uno's navigation bootstrap replaces the entire `ExtendedSplashScreen.Content` subtree with a `FrameView` at runtime once the host finishes loading. Any Grid/Border/StackPanel you author inside `Splash.Content` is thrown away — observable symptom is a header or nav bar that renders before the splash clears and then vanishes. All persistent chrome belongs in **MainPage**, never in Shell.

### Route Registration Pattern (Chefs-Aligned)

```csharp
private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
{
    views.Register(
        // Viewless root — Shell resolves via NavigateAsync<Shell>()
        new ViewMap(ViewModel: typeof(ShellModel)),
        // MainPage is the app chrome shell (header, tabs, content frame)
        new ViewMap<MainPage, MainModel>(),
        new ViewMap<DashboardPage, DashboardModel>(),
        new ViewMap<{Entity}ListPage, {Entity}ListModel>(),
        // Single entity page — Entity? data: null=create, non-null=edit
        new ViewMap<{Entity}Page, {Entity}PageModel>(Data: new DataMap<{Entity}Model>()),
        new ViewMap<SettingsPage, SettingsModel>()
    );

    routes.Register(
        new RouteMap("", View: views.FindByViewModel<ShellModel>(),
            Nested:
            [
                new RouteMap("Main", View: views.FindByViewModel<MainModel>(), IsDefault: true,
                    Nested:
                    [
                        new RouteMap("Dashboard", View: views.FindByViewModel<DashboardModel>(), IsDefault: true),
                        new RouteMap("{Entity}List", View: views.FindByViewModel<{Entity}ListModel>()),
                        new RouteMap("{Entity}Item", View: views.FindByViewModel<{Entity}PageModel>()),
                    ]
                ),
                // Routes outside Main (no tab bar chrome)
                new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>()),
            ]
        )
    );
}
```

> **Naming collision avoidance**: If the entity model is `{Entity}Model` (e.g., `TaskItemModel`) and the presentation model would also be `{Entity}Model`, append `PageModel` to the presentation class (e.g., `TaskItemPageModel`). Use the full namespace qualifier in `ViewMap`/`RouteMap` if needed.

### Route Non-Negotiables

1. **Root ViewMap is viewless**: `new ViewMap(ViewModel: typeof(ShellModel))` — no `<TView>` type parameter. Shell is resolved by `NavigateAsync<Shell>()`, not by the route map.
2. **MainPage is a nested route with `IsDefault: true`**: The navigation system navigates Shell → Main → Dashboard. Without the `Main` intermediate route, page content renders directly in Shell's Frame with no chrome.
3. **Tab routes nest under Main**: Dashboard, entity lists, and other tab-navigable pages must be children of the `Main` route so they render inside MainPage's content region (which has `uen:Region.Attached="True"`).
4. **Chrome-free routes nest under root**: Settings, login, profile — routes that should NOT show the tab bar — go as siblings of `Main`, not children of it.

## XAML Rules

- Code-behind stays minimal (initialize + thin UI-only behaviors).
- Put visual tokens and style overrides in `Styles/` dictionaries.
- Use converters from `Converters/` for presentation-only formatting.
- Keep reusable UI in `Views/Controls` and shared templates in `Views/Templates`.

### XAML Pitfalls

- **`TreeViewItemTemplateSelector`** does not exist. Use `<DataTemplate>` directly inside `<TreeView.ItemTemplate>`.
- **`uen:NavigationBar`** (`Uno.Extensions.Navigation.UI.NavigationBar`) does not exist. For top bars use `utu:NavigationBar` from `Uno.Toolkit.UI`, or omit and rely on `NavigationView` header.
- **`uen:ContentControl`** doesn't exist. For navigation content regions, use `<Frame />` inside a `<Grid uen:Region.Attached="true">`.
- **`NavigationView` content area**: Place a `<Grid uen:Region.Attached="true"><Frame /></Grid>` as the `NavigationView` content for region-based navigation.
- **`utu:AutoLayout.PrimaryAlignment`** valid values are `Stretch`, `Center`, `End` — there is no `Start`. Using `Start` produces a XAML parse error with no useful message. Default to `Stretch` for full-width content.

### Responsive Menu Pattern (Side-Nav Wide / Bottom-Tabs Narrow)

Use `utu:Responsive` with `Normal` (mobile/narrow) and `Wide` (desktop) breakpoints to toggle visibility of two parallel menu trees inside MainPage. Keep a single content region between them.

```xml
<!-- Desktop side nav -->
<Border Grid.Column="0"
        Visibility="{utu:Responsive Normal=Collapsed, Wide=Visible}"
        Width="160">
    <StackPanel Orientation="Vertical" ...>
        <Button Click="NavigateTopClick" Tag="Dashboard" .../>
        <!-- ... -->
    </StackPanel>
</Border>

<!-- Mobile bottom tab bar -->
<Grid Grid.Row="1" Grid.Column="1"
      Visibility="{utu:Responsive Normal=Visible, Wide=Collapsed}">
    <!-- 4 Column buttons, all with Click="NavigateTopClick" -->
</Grid>
```

Both menus call the same `NavigateTopClick` code-behind handler (see Menu Navigation section). Do not try to use a single `utu:TabBar` for both layouts — its `OnClickBehaviors` default to stack-aware navigation, which conflicts with the "always-to-top" requirement.

### Grid Paging — Fixed Header & Pager, Scrollable Body

When a list spans more rows than the viewport, the header and pager must stay visible while only the rows scroll. Use a three-row `Grid` with `Auto / * / Auto` and put the `ScrollViewer` around only the items region. Do NOT put the outer page content inside a `ScrollViewer` — the inner list will never get a scrollbar because the outer one absorbs all the height.

```xml
<Grid RowDefinitions="Auto,*,Auto">
    <!-- Row 0: toolbar / filters -->
    <Border Grid.Row="0" .../>

    <!-- Row 1: scrollable list body -->
    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
        <ItemsControl ItemsSource="{Binding Items}" .../>
    </ScrollViewer>

    <!-- Row 2: pager (First / Prev / pages / Next / Last) -->
    <Border Grid.Row="2" .../>
</Grid>
```

### ItemsControl vs FeedView for Child Collections

For **independently-editable in-page child lists** (checklist items, attachments, buffered-before-save child rows), bind `ItemsControl.ItemsSource` directly to an `IListState<T>` and skip `uer:FeedView`. `FeedView` adds a ValueTemplate/ProgressTemplate wrapper that sizes to content-undefined during initial empty state, giving you a tall empty region, and the progress/value template swap can suppress the `ItemsSource` change notification that toggles a `CheckBox` visually.

Use `FeedView` for top-level page data that has distinct None/Error/Progress UX. Use bare `ItemsControl` for inline children whose UI just needs to track a list state.

## Business Service Rules

In `Business/Services`:
- Define `I{Feature}Service` interfaces.
- Implement via Kiota client wrapper.
- Convert transport DTOs to UI models at service boundary.
- Surface `Result`/failure states usable by MVUX models.

### Client–API Contract Rules

The API wraps all CRUD payloads in `DefaultRequest<T>` (inbound) and `DefaultResponse<T>` (outbound). The Kiota client stub (or hand-written `TaskFlowApiClient`) must match this envelope.

**Request wrapping** — POST (create) and PUT (update) endpoints expect `{"item": {dto}}`, not the bare DTO:

```csharp
// ✅ CORRECT — wraps DTO in DefaultRequest envelope
var response = await _http.PostAsJsonAsync("/api/categories",
    new DefaultRequest<CategoryDto> { Item = dto }, ct);

// ❌ WRONG — sends bare DTO, API deserializes Item as null → NRE
var response = await _http.PostAsJsonAsync("/api/categories", dto, ct);
```

**Response unwrapping** — GET and mutating endpoints return `{"item": {dto}}`:

```csharp
// ✅ CORRECT — unwraps from DefaultResponse envelope
var wrapper = await _http.GetFromJsonAsync<DefaultResponse<CategoryDto>>(url, ct);
return wrapper?.Item;

// ❌ WRONG — reads bare DTO, all properties are null/default
return await _http.GetFromJsonAsync<CategoryDto>(url, ct);
```

**Search is different** — search endpoints accept `SearchRequest<TFilter>` directly (no wrapping) and return `PagedResponse<T>` with a `data` array (not `DefaultResponse`).

**Pagination contract — 1-based `pageIndex`**: The server API expects `pageIndex` (not `pageNumber`) and treats it as **1-based**. Never send `0`. The hand-written client stub must match that wire name and base exactly — using `pageNumber` or 0-based indexing silently returns page 1 for every request.

```csharp
public class SearchRequest<TFilter> where TFilter : class, new()
{
    [JsonPropertyName("filter")] public TFilter Filter { get; set; } = new();

    // Internal 1-based value; never exposed on the wire under this name.
    [JsonIgnore] public int PageNumber { get; set; } = 1;

    // What the server actually reads — same 1-based value, wire name "pageIndex".
    [JsonPropertyName("pageIndex")]
    public int PageIndex { get => PageNumber; set => PageNumber = value; }

    [JsonPropertyName("pageSize")] public int PageSize { get; set; } = 50;
}
```

When debugging paging that "always returns page 1": inspect the serialized request body in devtools. If you see `"pageNumber"` in the payload, or `"pageIndex": 0`, the contract is wrong — the server is silently coercing both to page 1.

Client-side wrapper classes:

```csharp
public class DefaultRequest<T>
{
    [JsonPropertyName("item")]
    public T Item { get; set; } = default!;
}

public class DefaultResponse<T>
{
    [JsonPropertyName("item")]
    public T? Item { get; set; }
}
```

## Auth Rules

- UI authenticates to Gateway identity provider.
- No direct API tokens in XAML or page code-behind.
- Keep auth/session handling in host/services.

### Dev-Mode Auth to Production MSAL Upgrade

Scaffold with `.AddCustom()` (no external identity provider required). When ready for production:

1. Register app in **Entra External ID** (CIAM) — get `ClientId` + `Authority`
2. Update `appsettings.json` `EntraExternal` section with real tenant values
3. Replace `.AddCustom(...)` with `.AddMsal()` in `App.xaml.host.cs`
4. Change `<UnoFeatures>` in `.csproj`: `AuthenticationCustom` to `AuthenticationMsal`
5. `AuthTokenHandler` (reads `ITokenCache`) works identically with both providers
6. Configure Gateway with `TaskFlowGateway_EntraID` config section (see `skills/identity-management.md`)

## Generation Checklist

- [ ] `includeUnoUI: true` set in domain inputs
- [ ] `GatewayBaseUrl` present in `appsettings*.json`
- [ ] Auth + HTTP + navigation configured in `App.xaml.host.cs`
- [ ] `Business/Models`, `Business/Services`, `Presentation`, `Views` scaffolded
- [ ] `Features:UseMocks` implemented (mock + live path)
- [ ] Core pages scaffolded: Home, {Entity}List, {Entity}Page (unified add/edit + children), Settings (+ Login when auth enabled)
- [ ] Each entity uses single page pattern: `{Entity}Page` with form fields + children sections
- [ ] Children sections visible only in edit mode (`Visibility="{Binding IsEditMode}"`)
- [ ] `FormTextBoxStyle` applied to all TextBox inputs for visible borders
- [ ] Route mappings and page-model bindings compile
- [ ] `Shell.xaml` has `ExtendedSplashScreen.Content` containing `<Frame />`
- [ ] `Shell.xaml.cs` implements `IContentControlProvider`
- [ ] `ShellModel` navigates to first route in constructor
- [ ] `Platforms/WebAssembly/WasmScripts/AppManifest.js` present
- [ ] `launchSettings.json` HTTP port not in Windows excluded range (use `55553`, not `55552`)
- [ ] Gateway `CorsSettings.AllowedOrigins` includes `https://localhost:55551` and `http://localhost:55553`
- [ ] UI uses Gateway endpoints only

## WASM Debugging Ladder

When a Uno WASM build or runtime failure occurs, follow this fixed validation order before applying broader hosting rewrites:

1. **Root document:** Does the WASM host page (`index.html`) load at all? Check for 404/500 on the base URL.
2. **Package/static assets:** Are CSS, images, and app-specific static files served? Check browser network tab for 404s.
3. **`/_framework` assets:** Do `dotnet.wasm`, `blazor.boot.json` / `uno-boot.json`, and framework DLLs load? Missing `/_framework` files indicate a build or publish issue, not a routing issue.
4. **Generated bootstrap/config:** Are `appsettings.json`, `AppManifest.js`, and generated host files present and correct? Do not rewrite these unless a specific file is confirmed missing or malformed.
5. **Browser console:** Check for JS errors, CORS failures, or WASM instantiation errors. These narrow the fault to runtime init vs asset serving.

Do not apply broad hosting or routing rewrites before completing this sequence.

## WASM Host Launch Requirements

These apply to `WasmAppHost` (the dev host launched by `dotnet run` in Uno WASM projects).

### AppManifest.js — Required Bootstrap File

`Uno.UI.js` does `define(["./AppManifest.js"])` via RequireJS at startup. If the file does not exist the splash screen never clears — no JS error is visible.

Every Uno WASM project MUST contain:

```
Platforms/WebAssembly/WasmScripts/AppManifest.js
```

Minimal content:

```js
var UnoAppManifest = {
    displayName: "{AppName}",
    splashScreenColor: "transparent"
};
```

Add this file during initial scaffold. Do not leave it absent and rely on the build to generate it — it is not generated automatically.

### Working Directory Sensitivity

`WasmAppHost` resolves the hashed `package_<hash>/` directory relative to CWD. It only produces the correct `index.html` and static-asset paths when run **from the Uno project directory**, not from the solution root or a parent directory.

Always run:

```powershell
Set-Location 'src\UI\{Project}.Uno'
dotnet run
```

Never use `dotnet run --project <path>` from an unrelated working directory — the static asset paths in the output will be wrong and all `package_<hash>/*` requests will 404.

### Port Exclusion on Windows (Hyper-V / WSL)

Windows reserves port ranges for Hyper-V and WSL (shown as PID 4 owning ports in `Listen` state). These ports cannot be bound by user-space processes — attempts fail silently or with error 10013.

Diagnose before changing launchSettings:

```powershell
netsh int ipv4 show excludedportrange protocol=tcp | Select-String '5555[0-9]'
```

If a port used in `launchSettings.json` is listed, change it to a port confirmed absent from the exclusion list.

**Known-bad port**: `55552` is routinely in the excluded range on Hyper-V/Docker Desktop hosts.
**Known-good ports**: `55551` (HTTPS) and `55553` (HTTP) are not excluded on a standard developer machine.

When scaffolding a new Uno project's `launchSettings.json`, use:

```json
"applicationUrl": "https://localhost:55551;http://localhost:55553"
```

Also update the Gateway `CorsSettings.AllowedOrigins` to include both `https://localhost:55551` and `http://localhost:55553`.

### Freeing a Stuck Dev Port on Windows (from bash/Git Bash)

When `dotnet run` fails with `AddressInUseException` because a previous `WasmAppHost` process is still holding the port (crash, orphaned debugger, terminated IDE), find and kill it. In Git Bash / MSYS bash, `taskkill` requires **double-slash** flags:

```bash
# Find PID holding the port
netstat -ano | grep :55551
# Kill (bash: // not /)
taskkill //F //PID <pid>
```

A `TIME_WAIT` entry on the client side is harmless — it's a closed socket awaiting TCP drain and will clear on its own. Only `LISTENING` entries block a new bind.

Do **not** change the launch port to work around a stuck process — find and kill it instead. Rotating ports invalidates CORS config, Playwright baseURL, and bookmark URLs.

### Post-Rebuild Browser Refresh

After any rebuild, `WasmAppHost` serves a new `package_<hash>/` directory. The old hash is instantly stale. Always open a **new browser tab** to the HTTPS origin — never reload an existing tab. Existing tabs will 404 all their `package_*` asset requests until a full address-bar navigation occurs.

---

## Generated Code Intervention Rule

For generator-driven stacks (Uno, Kiota, Resizetizer, and similar toolchains):

- **Preserve generated conventions by default.** Do not rewrite generated bootstrap, host plumbing, or build targets unless a specific symptom proves the generated assumption is wrong.
- **Patch minimally.** Fix only the smallest confirmed incompatibility. One targeted MSBuild property override or one config fixup — not a full rewrite of the generated file.
- **Document the justification.** Every patch to generated code must carry an inline comment citing the exact symptom (e.g., `<!-- Workaround: Resizetizer 1.12.1 manifest-path bug -->`).

If you cannot identify the specific failing assumption, do not modify generated code — escalate to the engineer.

## Environment Detection Rule

When distinguishing browser, Electron, desktop-webview, or similar runtime environments, prefer **capability or runtime-object checks** over raw user-agent string matching. User-agent strings are unreliable in embedded browsers, IDE preview panes, and WebView2 hosts.

Example: check for `window.__TAURI__` or `navigator.userAgentData` rather than parsing `navigator.userAgent`.

---

## .NET for Android — Build & Deploy Rules

These rules apply when targeting `net10.0-android` (or equivalent) via Uno, MAUI, or bare .NET for Android.

### Android SDK Discovery (Windows)

Before writing any `emulator`, `adb`, or SDK tool command, resolve the actual SDK root:

1. Check `ANDROID_HOME` / `ANDROID_SDK_ROOT` env vars.
2. If unset, check `C:\Program Files (x86)\Android\android-sdk` (Android Studio default) and `%LOCALAPPDATA%\Android\Sdk` (standalone SDK manager default).
3. Verify `emulator\emulator.exe` and `platform-tools\adb.exe` exist at the resolved path.
4. Set `ANDROID_HOME` explicitly in the shell session before invoking any SDK tools.

Do not assume SDK tools are on `PATH`.

### Embedded Assemblies for Sideloading

When building for manual ADB sideloading (`dotnet build` + `adb install`), always set in the Android TFM `PropertyGroup`:

```xml
<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>
```

The default Debug mode uses **Fast Deployment**, which expects the .NET tooling to push managed assemblies to the device separately after install. A bare APK installed without that push crashes immediately with _"No assemblies found … Assuming this is part of Fast Deployment"_. Lock this property into the project file permanently for any project that supports manual sideloading — do not rely on a command-line override.

### Emulator Host Networking

Apps running on the Android emulator that call local backend services must use `10.0.2.2` in place of `localhost` / `127.0.0.1`. Gate this with a compile-time check so WASM/desktop builds continue to use `localhost`:

```csharp
#if __ANDROID__
    const string LocalHost = "10.0.2.2";
#else
    const string LocalHost = "localhost";
#endif
```

Quick validation from emulator shell (no running service required):
```bash
adb shell "echo TEST | nc 10.0.2.2 <PORT>"
```

### Activity Class Name Discovery

.NET for Android generates a CRC-based Java class name for activities (e.g., `crc64<hash>.MainActivity`) that differs from the C# class name. Do not guess it from source.

When launching via `adb shell am start -n`, first discover the registered activity:

```bash
adb shell dumpsys package <package-id> | grep -A 3 "MAIN"
```

Use the class name from the output — the generated name cannot be predicted from C# source alone.

---

## Known Build Issues / Workarounds

### Resizetizer File Naming Rules

Uno.Resizetizer requires asset filenames to be **lowercase**, containing only alphanumeric characters or underscores, and starting/ending with a letter. Files like `SplashScreen.svg` or `my-icon.png` will fail the build.

### UnoSplashScreen WASM Build Failure (Resizetizer 1.12.1)

**Symptom:** Adding `<UnoSplashScreen Include="Assets\splashscreen.svg" />` causes `GenerateWasmSplashAssets` to fail silently on WASM. Even without `UnoSplashScreen`, ShellTask may crash with `DirectoryNotFoundException` on clean builds.

**Root cause:** Resizetizer line 529 constructs a fallback PWA manifest path using `GetFileName($(WasmPWAManifestFile))`. When `WasmPWAManifestFile` is unset, `GetFileName("")` returns empty, producing a bare directory path (`unoresizetizer\`). MSBuild `Exists()` returns true for directories, so `UnoResizetizerPwaManifest` gets set to a directory. ShellTask then calls `File.ReadAllText` on that directory and crashes.

**Workaround:** Add this target to the UI `.csproj`:

```xml
<!-- Workaround: Resizetizer 1.12.1 sets WasmPWAManifestFile to a directory
     path when no UnoSplashScreen is configured. Clear it so ShellTask doesn't
     call File.ReadAllText on a directory. -->
<Target Name="_FixWasmPwaManifestPath"
        BeforeTargets="GenerateUnoWasmAssets"
        AfterTargets="ProcessResizedImagesWasm"
        Condition="$(TargetFramework.Contains('browserwasm'))">
  <PropertyGroup>
    <WasmPWAManifestFile Condition="'$(WasmPWAManifestFile)' != '' AND !$([System.IO.File]::Exists('$(WasmPWAManifestFile)'))"></WasmPWAManifestFile>
  </PropertyGroup>
</Target>
```

**Important:** Do NOT place standalone splash screen asset files (`splashscreen.png`, `splashscreen.svg`) in Assets without a corresponding `<UnoSplashScreen>` item — the resizetizer will produce duplicate static web asset conflicts.

### ExtendedSplashScreen vs UnoSplashScreen

- `UnoSplashScreen` = native splash (Android/iOS) generated by Resizetizer. Broken on WASM in 1.12.1.
- `ExtendedSplashScreen` = Uno Toolkit XAML control in `ShellControl.xaml` for the in-app loading screen. This is the recommended WASM splash approach.
- The "Didn't find UnoSplashScreen" warning from Resizetizer is **harmless** — it just means no native splash is configured.

## CI Requirements

When the solution includes a Uno WASM project, CI workflows must install the `wasm-tools` workload before build:

```yaml
- name: Install required workloads
  run: dotnet workload install wasm-tools
```

Without this, the build fails with `UNOWA0001: Native WebAssembly assets were detected, but the wasm-tools workload could not be located.`

Add this step after `actions/setup-dotnet@v4` and before `dotnet restore`. See [cicd.md](cicd.md) for the full CI template.

## Related Skills

- Solution layout: [solution-structure.md](solution-structure.md)
- Gateway integration: [gateway.md](gateway.md)
- Auth setup: [identity-management.md](identity-management.md)
- Testing strategy: [testing.md](testing.md)
- App configuration: [configuration-secrets.md](configuration-secrets.md)