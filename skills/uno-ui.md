# Uno Platform UI

## Overview

The UI project uses **Uno Platform** to build a cross-platform C#/XAML application that targets Web (WASM), Android, iOS, macOS, Windows, and Linux — all from a single codebase. The app authenticates to and consumes the **YARP gateway** defined in the backend solution. It follows the **MVUX** (Model-View-Update-eXtended) pattern for state management with reactive feeds and states.

## Uno Scaffolding Profiles

Use `unoProfile` from [domain-inputs.schema.md](../domain-inputs.schema.md) to right-size initial UI generation.

| Profile | Includes | When to Use |
|---------|----------|-------------|
| `starter` (default) | App host wiring, auth + HTTP setup, `ViewMap`/`RouteMap`, list/detail/settings pages, MVUX models, business services | First iterations where backend slices are still evolving |
| `full` | Everything in `starter` + richer shell/navigation regions, dialog/flyout route patterns, broader page set, stronger mock/live switching patterns | Productized UI tracks with stable core workflows |

Start at `starter`; move to `full` after navigation flows, auth, and core entity slices are stable.

## Architecture

```
┌─────────────────────────────────────────────────┐
│                   Uno Platform App               │
│                                                   │
│  Views/  ←──binds──  Presentation/  ←──calls──  Business/  ←──HTTP──  Gateway (YARP)
│  (XAML Pages)         (MVUX Models)               (Services + Models)      │
│                                                                             │
│  Styles/  Converters/  Assets/  Infrastructure/  Client/                   │
└─────────────────────────────────────────────────┘
                                                         │
                                                    ┌────▼────┐
                                                    │ {Host}.Api │
                                                    └─────────┘
```

The Uno UI calls the **Gateway** (not the API directly). The Gateway handles user-facing auth (Entra External), CORS, token relay, and forwards requests to the API.

## Project Structure

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/` for the full project layout with 44 files across Business, Client, Converters, Infrastructure, Presentation, Strings, Styles, and Views.

```
{Project}.UI/
├── App.xaml                          # Application resources, theme merging
├── App.xaml.cs                       # Application entry point
├── App.xaml.host.cs                  # IApplicationBuilder — host configuration, DI, routing
├── GlobalUsings.cs                   # Shared using statements
├── {Project}.UI.csproj               # Multi-target: net10.0-android;ios;maccatalyst;windows;browserwasm;desktop
├── appsettings.json                  # Gateway base URL, feature flags
├── appsettings.development.json      # Dev overrides
├── Package.appxmanifest              # Windows app manifest
├── Assets/                           # App icons, splash, images (SVG via Uno.Resizetizer)
├── Business/
│   ├── Models/                       # Client-side record models (not domain entities)
│   │   ├── {Entity}.cs
│   │   └── ...
│   └── Services/                     # Service interfaces + implementations
│       ├── {Feature}/
│       │   ├── I{Feature}Service.cs
│       │   └── {Feature}Service.cs
│       └── Clients/                  # Kiota-generated API client wrapper
├── Client/                           # Kiota-generated HTTP client (from Gateway OpenAPI spec)
│   ├── {Project}ApiClient.cs
│   ├── Api/                          # Generated request builders
│   ├── Models/                       # Generated DTOs (wire format)
│   └── kiota-lock.json
├── Converters/                       # IValueConverter implementations
│   ├── Converters.xaml               # ResourceDictionary registering converters as resources
│   ├── StringFormatter.cs
│   ├── BoolInverter.cs
│   └── ...
├── Infrastructure/                   # Platform-specific helpers
├── Platforms/                        # Per-platform entry points (Android Activity, iOS AppDelegate, etc.)
├── Presentation/                     # MVUX Models (the "ViewModel" layer)
│   ├── {Page}Model.cs
│   ├── Extensions/                   # Feed/State extension methods
│   └── Messages/                     # CommunityToolkit.Mvvm.Messaging messages
├── Properties/
│   └── launchSettings.json
├── Strings/                          # Localization resources (en, fr, es, pt-BR)
│   └── en/Resources.resw
├── Styles/                           # XAML resource dictionaries
│   ├── ColorPaletteOverride.xaml     # Material Design color overrides
│   ├── CustomFonts.xaml
│   ├── Button.xaml                   # Control style overrides
│   ├── NavigationBar.xaml
│   ├── FeedView.xaml
│   └── ...
└── Views/                            # XAML Pages
    ├── {Page}Page.xaml
    ├── {Page}Page.xaml.cs            # Minimal code-behind
    ├── Controls/                     # Reusable custom controls
    ├── Dialogs/                      # Dialog content
    ├── Flyouts/                      # Flyout presenters
    └── Templates/                    # Shared DataTemplates
```

## Key Dependencies (NuGet)

```xml
<!-- Uno Platform core -->
<PackageReference Include="Uno.WinUI" />
<PackageReference Include="Uno.Sdk.Private" />

<!-- Uno Extensions (hosting, nav, reactive, config, auth, HTTP, localization) -->
<PackageReference Include="Uno.Extensions.Hosting.WinUI" />
<PackageReference Include="Uno.Extensions.Navigation.WinUI" />
<PackageReference Include="Uno.Extensions.Navigation.Toolkit.WinUI" />
<PackageReference Include="Uno.Extensions.Reactive.WinUI" />
<PackageReference Include="Uno.Extensions.Configuration" />
<PackageReference Include="Uno.Extensions.Authentication.WinUI" />
<PackageReference Include="Uno.Extensions.Http.WinUI" />
<PackageReference Include="Uno.Extensions.Http.Kiota" />
<PackageReference Include="Uno.Extensions.Localization.WinUI" />
<PackageReference Include="Uno.Extensions.Logging.WinUI" />
<PackageReference Include="Uno.Extensions.Serialization" />
<PackageReference Include="Uno.Extensions.Storage" />

<!-- Uno Toolkit (AutoLayout, NavigationBar, TabBar, Chips, etc.) -->
<PackageReference Include="Uno.Toolkit.WinUI" />
<PackageReference Include="Uno.Toolkit.WinUI.Material" />

<!-- Uno Material Theme -->
<PackageReference Include="Uno.Material.WinUI" />

<!-- Messaging -->
<PackageReference Include="CommunityToolkit.Mvvm" />

<!-- Kiota HTTP client generation -->
<PackageReference Include="Microsoft.Kiota.Abstractions" />
<PackageReference Include="Microsoft.Kiota.Http.HttpClientLibrary" />
<PackageReference Include="Microsoft.Kiota.Serialization.Json" />
```

## App Host Configuration (App.xaml.host.cs)

This is the central setup file. It configures authentication, HTTP client, navigation, DI, serialization, and logging — all via `IApplicationBuilder`.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/App.xaml.host.cs` for the full host configuration including auth, HTTP (Kiota with mock/live switch), logging, config, localization, serialization, DI registration, and navigation route mapping.

```csharp
namespace {Project}.UI;

public partial class App : Application
{
    private void ConfigureAppBuilder(IApplicationBuilder builder)
    {
        builder
            .UseToolkitNavigation()
            .Configure(host => host
                // Authentication — Entra External via Gateway
                .UseAuthentication(auth =>
                    auth.AddCustom(custom =>
                    {
                        custom.Login(async (sp, dispatcher, credentials, ct) =>
                            await ProcessCredentials(credentials));
                    }, name: "CustomAuth")
                )
                // HTTP — Kiota client pointing at Gateway
                .UseHttp((context, services) =>
                {
                    services.AddKiotaClient<{Project}ApiClient>(
                        context,
                        options: new EndpointOptions
                        {
                            Url = context.Configuration["GatewayBaseUrl"]
                                  ?? "https://localhost:7200"
                        }
                    );
                })
                .UseEnvironment(Environments.Development)
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder.SetMinimumLevel(
                        context.HostingEnvironment.IsDevelopment()
                            ? LogLevel.Information
                            : LogLevel.Warning);
                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<Credentials>()
                )
                .UseLocalization()
                .UseSerialization(configure: ConfigureSerialization)
                // Register business services
                .ConfigureServices((context, services) =>
                {
                    services
                        .AddSingleton<I{Entity}Service, {Entity}Service>()
                        .AddSingleton<IMessenger, WeakReferenceMessenger>();
                })
                // Navigation — MVUX model ↔ page mapping
                .UseNavigation(
                    ReactiveViewModelMappings.ViewModelMappings,
                    RegisterRoutes
                ));
    }
}
```

### Mock vs Live API Mode

Use a dual-mode pattern: register a mock handler and switch routing based on configuration (`Features:UseMocks`) or compile profile.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Infrastructure/MockHttpMessageHandler.cs` for the contrived JSON mock handler, and `sampleapp/src/TaskFlow/TaskFlow.UI/appsettings.json` for the `Features:UseMocks` toggle.

```csharp
.UseHttp((context, services) =>
{
    services.AddTransient<MockHttpMessageHandler>();

    services.AddKiotaClient<{Project}ApiClient>(
        context,
        options: new EndpointOptions
        {
            Url = context.Configuration["GatewayBaseUrl"] ?? "https://localhost:7200"
        },
        configure: (builder, endpoint) =>
        {
            var useMocks = context.Configuration.GetValue<bool>("Features:UseMocks");
            if (useMocks)
            {
                builder.ConfigurePrimaryAndInnerHttpMessageHandler<MockHttpMessageHandler>();
            }
        });
})
```

`starter` should scaffold both paths (live + mock) even if mocks are initially disabled.

### Gateway Base URL (appsettings.json)

```json
{
  "GatewayBaseUrl": "https://localhost:7200",
  "Features": {
    "UseMocks": false
  }
}
```

The `GatewayBaseUrl` points to the YARP Gateway. In production, this is the public-facing gateway URL. The Kiota-generated client sends all requests here — the Gateway handles routing to the backend API.

## MVUX Presentation Models

MVUX models replace traditional ViewModels. They are **partial records** that expose reactive `IFeed<T>`, `IListFeed<T>`, `IState<T>`, and `IListState<T>` properties. The MVUX source generator creates the bindable proxy.

### Key Concepts

| Concept | Type | Description |
|---------|------|-------------|
| Feed | `IFeed<T>`, `IListFeed<T>` | Read-only reactive data source. Refreshes automatically. |
| State | `IState<T>`, `IListState<T>` | Read-write reactive data. Supports `SetAsync`, `UpdateAsync`, `AddAsync`, `RemoveAllAsync`. |
| Commands | `ValueTask` methods | Public async methods on the model are auto-bound as commands. |
| Navigation | `INavigator` | Injected for route-based navigation (`NavigateRouteAsync`, `NavigateViewModelAsync`). |
| Messaging | `IMessenger` | CommunityToolkit `WeakReferenceMessenger` for cross-model communication (entity updates, etc.). |
| Observe | `.Observe(_messenger, ...)` | Re-syncs a Feed/State when EntityMessage is received. |

### List Page Model Example

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Presentation/TodoItemListModel.cs` (list with search, `.Observe()`, navigation commands) and `sampleapp/src/TaskFlow/TaskFlow.UI/Presentation/CreateTodoItemModel.cs` (form with `IState` fields and category picker).

```csharp
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

    // Read-only feed — auto-refreshes on messenger updates
    public IListFeed<{Entity}> Items =>
        ListFeed.Async(_{entity}Service.GetAll);

    // Mutable state — for search, filters, etc.
    public IState<string> SearchTerm =>
        State<string>.Value(this, () => string.Empty);

    // Filtered results combining state + feed
    public IListState<{Entity}> FilteredItems => ListState
        .FromFeed(this, Feed
            .Combine(SearchTerm, Items.AsFeed())
            .SelectAsync(Search)
            .AsListFeed())
        .Observe(_messenger, r => r.Id);

    // Commands — auto-bound in XAML
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

### Detail Page Model Example

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Presentation/TodoItemDetailModel.cs` (DataViewMap injection, `IState<bool>` for completion toggle, Delete/GoBack commands).

```csharp
namespace {Project}.UI.Presentation;

public partial record {Entity}DetailModel
{
    private readonly INavigator _navigator;
    private readonly I{Entity}Service _{entity}Service;
    private readonly IMessenger _messenger;

    public {Entity}DetailModel(
        {Entity} {entity},         // Injected via navigation data
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

    // Child collections as feeds
    public IListFeed<{ChildEntity}> {ChildEntity}s =>
        ListFeed.Async(async ct => await _{entity}Service.Get{ChildEntity}s({Entity}.Id, ct));

    // Mutable state for user interactions
    public IState<bool> IsFavorited =>
        State.Value(this, () => {Entity}.IsFavorite);

    public async ValueTask ToggleFavorite(CancellationToken ct)
    {
        await _{entity}Service.Favorite({Entity}, ct);
        await IsFavorited.UpdateAsync(s => !s);
    }

    public async ValueTask GoBack(CancellationToken ct) =>
        await _navigator.GoBack(this);
}
```

## Business Models (Client-Side Records)

Client-side models are **records** that wrap the Kiota-generated wire DTOs. They provide clean property access and conversion methods.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Business/Models/TodoItem.cs` (record + `TodoItemData` wire DTO, computed properties like `DisplayPriority`, `IsOverdue`) and `sampleapp/src/TaskFlow/TaskFlow.UI/Business/Models/Category.cs`.

```csharp
namespace {Project}.UI.Business.Models;

public partial record {Entity} : IEntityBase
{
    // Constructor from Kiota-generated DTO
    internal {Entity}({Entity}Data data)
    {
        Id = data.Id ?? Guid.Empty;
        Name = data.Name;
        // ... map all properties
    }

    public Guid Id { get; init; }
    public string? Name { get; init; }
    // ... properties

    // Convert back to wire DTO for POST/PUT
    internal {Entity}Data ToData() => new()
    {
        Id = Id,
        Name = Name,
        // ... map all properties
    };
}
```

The `IEntityBase` interface is a shared marker for entities that have `Guid Id`.

## Business Services

Services are the data access layer for the UI. They call the Kiota-generated API client (which hits the Gateway), map wire DTOs to client models, and return `IImmutableList<T>`.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Business/Services/TodoItems/TodoItemService.cs` (mock data with EntityMessage broadcasting) and `sampleapp/src/TaskFlow/TaskFlow.UI/Business/Services/TodoItems/ITodoItemService.cs` for the interface pattern.

```csharp
namespace {Project}.UI.Business.Services.{Feature};

public interface I{Entity}Service
{
    ValueTask<IImmutableList<{Entity}>> GetAll(CancellationToken ct);
    ValueTask<{Entity}> GetById(Guid id, CancellationToken ct);
    ValueTask Create({Entity} entity, CancellationToken ct);
    ValueTask Update({Entity} entity, CancellationToken ct);
    ValueTask Delete(Guid id, CancellationToken ct);
    ValueTask<IImmutableList<{ChildEntity}>> Get{ChildEntity}s(Guid {entity}Id, CancellationToken ct);
}
```

```csharp
namespace {Project}.UI.Business.Services.{Feature};

public class {Entity}Service({Project}ApiClient api, IMessenger messenger)
    : I{Entity}Service
{
    public async ValueTask<IImmutableList<{Entity}>> GetAll(CancellationToken ct)
    {
        var data = await api.Api.{Entity}.GetAsync(cancellationToken: ct);
        return data?.Select(d => new {Entity}(d)).ToImmutableList()
            ?? ImmutableList<{Entity}>.Empty;
    }

    public async ValueTask<{Entity}> GetById(Guid id, CancellationToken ct)
    {
        var data = await api.Api.{Entity}[id].GetAsync(cancellationToken: ct);
        return new {Entity}(data);
    }

    public async ValueTask Create({Entity} entity, CancellationToken ct)
    {
        await api.Api.{Entity}.PostAsync(entity.ToData(), cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Created, entity));
    }

    public async ValueTask Update({Entity} entity, CancellationToken ct)
    {
        await api.Api.{Entity}[entity.Id].PutAsync(entity.ToData(), cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Updated, entity));
    }

    public async ValueTask Delete(Guid id, CancellationToken ct)
    {
        await api.Api.{Entity}[id].DeleteAsync(cancellationToken: ct);
        messenger.Send(new EntityMessage<{Entity}>(EntityChange.Deleted, new {Entity} { Id = id }));
    }

    public async ValueTask<IImmutableList<{ChildEntity}>> Get{ChildEntity}s(
        Guid {entity}Id, CancellationToken ct)
    {
        var data = await api.Api.{Entity}[{entity}Id].{ChildEntity}s
            .GetAsync(cancellationToken: ct);
        return data?.Select(d => new {ChildEntity}(d)).ToImmutableList()
            ?? ImmutableList<{ChildEntity}>.Empty;
    }
}
```

## Messaging for Cross-Model Updates

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Presentation/Messages/EntityMessage.cs` for the `EntityChange` enum and generic `EntityMessage<T>` record.

```csharp
namespace {Project}.UI.Presentation.Messages;

public enum EntityChange { Created, Updated, Deleted }

public record EntityMessage<T>(EntityChange Change, T Entity);
```

When a service mutates data, it sends an `EntityMessage<T>`. Any MVUX model using `.Observe(_messenger, ...)` on a related Feed/State automatically refreshes.

## XAML Views

### Page Pattern

XAML pages use **Uno Toolkit** controls (`utu:AutoLayout`, `utu:NavigationBar`, `utu:CardContentControl`), **Uno Extensions** navigation (`uen:Navigation.Request`), and **FeedView** (`uer:FeedView`) to bind to MVUX feeds.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Views/TodoItemListPage.xaml` (FeedView + ListView + SearchBox), `sampleapp/src/TaskFlow/TaskFlow.UI/Views/TodoItemDetailPage.xaml` (Card layout, detail fields), and `sampleapp/src/TaskFlow/TaskFlow.UI/Views/CreateTodoItemPage.xaml` (form with FeedView-wrapped ComboBox).

```xml
<Page x:Class="{Project}.UI.Views.{Entity}ListPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:muxc="using:Microsoft.UI.Xaml.Controls"
      xmlns:uer="using:Uno.Extensions.Reactive.UI"
      xmlns:utu="using:Uno.Toolkit.UI"
      xmlns:uen="using:Uno.Extensions.Navigation.UI"
      xmlns:ut="using:Uno.Themes"
      Background="{ThemeResource BackgroundBrush}">

    <utu:AutoLayout utu:AutoLayout.PrimaryAlignment="Stretch">
        <!-- Navigation bar -->
        <utu:NavigationBar Style="{StaticResource AppNavigationBarStyle}">
            <utu:NavigationBar.Content>
                <TextBlock Text="{Entity}s" Style="{StaticResource TitleLarge}" />
            </utu:NavigationBar.Content>
        </utu:NavigationBar>

        <!-- List content bound to MVUX Feed -->
        <uer:FeedView Source="{Binding Items}">
            <DataTemplate>
                <muxc:ItemsRepeater ItemsSource="{Binding Data}"
                                    uen:Navigation.Request="{Entity}Detail"
                                    uen:Navigation.Data="{Binding Data}">
                    <muxc:ItemsRepeater.Layout>
                        <muxc:StackLayout Spacing="8" />
                    </muxc:ItemsRepeater.Layout>
                    <muxc:ItemsRepeater.ItemTemplate>
                        <DataTemplate>
                            <utu:CardContentControl Style="{StaticResource FilledCardContentControlStyle}">
                                <utu:AutoLayout Padding="16" Spacing="8">
                                    <TextBlock Text="{Binding Name}"
                                               Style="{StaticResource TitleSmall}" />
                                </utu:AutoLayout>
                            </utu:CardContentControl>
                        </DataTemplate>
                    </muxc:ItemsRepeater.ItemTemplate>
                </muxc:ItemsRepeater>
            </DataTemplate>
        </uer:FeedView>
    </utu:AutoLayout>
</Page>
```

### Code-Behind — Minimal

```csharp
namespace {Project}.UI.Views;

public sealed partial class {Entity}ListPage : Page
{
    public {Entity}ListPage()
    {
        this.InitializeComponent();
    }
}
```

### Navigation

Navigation uses **route-based** URIs declared in `RegisterRoutes()`:

```csharp
private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
{
    views.Register(
        new ViewMap(ViewModel: typeof(ShellModel)),
        new ViewMap<MainPage, MainModel>(),
        new ViewMap<{Entity}ListPage, {Entity}ListModel>(),
        new DataViewMap<{Entity}DetailPage, {Entity}DetailModel, {Entity}>(),
        new ViewMap<LoginPage, LoginModel>(ResultData: typeof(Credentials)),
        new ViewMap<SettingsPage, SettingsModel>()
    );

    routes.Register(
        new RouteMap("", View: views.FindByViewModel<ShellModel>(),
            Nested:
            [
                new RouteMap("Login", View: views.FindByViewModel<LoginModel>()),
                new RouteMap("Main", View: views.FindByViewModel<MainModel>(),
                    Nested:
                    [
                        new RouteMap("Home", View: views.FindByViewModel<HomeModel>(), IsDefault: true),
                        new RouteMap("{Entity}List", View: views.FindByViewModel<{Entity}ListModel>()),
                        new RouteMap("{Entity}Detail", View: views.FindByViewModel<{Entity}DetailModel>()),
                    ]),
                new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>()),
            ]
        )
    );
}
```

Use `DataViewMap<...>` whenever the destination model requires navigation data (entity details, dialogs, filter models).

### Navigation Request Qualifiers (XAML)

Use Uno navigation request qualifiers consistently:

```xml
<!-- Relative route -->
<AppBarButton uen:Navigation.Request="{Entity}Detail" />

<!-- Navigate to top-level login from nested flow -->
<Button uen:Navigation.Request="-/Login" />

<!-- Navigate to region/flyout route -->
<AppBarButton uen:Navigation.Request="!Profile" />

<!-- Back navigation -->
<AppBarButton uen:Navigation.Request="!back" />
```

Prefer XAML navigation requests for straightforward UI actions; reserve code navigation (`INavigator`) for workflows that require conditional logic or returned values.

## Shell + Region Pattern

For apps with multiple tabs, build a shell with `uen:Region.Attached` and keep primary navigation declarative:

```xml
<Grid uen:Region.Attached="True">
    <utu:TabBar uen:Region.Attached="True">
        <utu:TabBarItem Content="Home" uen:Region.Name="Home" IsSelected="True" />
        <utu:TabBarItem Content="Search" uen:Region.Name="Search" />
        <utu:TabBarItem Content="Favorites" uen:Region.Name="Favorite" />
    </utu:TabBar>
</Grid>
```

Map tabs as nested routes under `Main` in `RouteMap`; use `IsDefault: true` for the landing tab.

## Theme + Persistent Settings Pattern

A practical pattern for user settings:
- expose theme/settings via MVUX `IState<T>`
- apply theme using `IThemeService`
- persist user choices with configuration options (`IWritableOptions<T>`)
- broadcast significant UI-wide changes with `IMessenger`

This keeps settings reactive, cross-platform, and consistent with the rest of the MVUX model layer.

## Theming — Material Design

Use **Uno.Material** with color palette overrides.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Styles/ColorPaletteOverride.xaml` for Material Design 3 color palette, `sampleapp/src/TaskFlow/TaskFlow.UI/Styles/FeedView.xaml` for FeedView style templates, and `sampleapp/src/TaskFlow/TaskFlow.UI/App.xaml` for theme resource merging.

```xml
<!-- Styles/ColorPaletteOverride.xaml -->
<ResourceDictionary>
    <Color x:Key="PrimaryColor">#6750A4</Color>
    <Color x:Key="OnPrimaryColor">#FFFFFF</Color>
    <Color x:Key="SecondaryColor">#625B71</Color>
    <Color x:Key="BackgroundColor">#FFFBFE</Color>
    <Color x:Key="SurfaceColor">#FFFBFE</Color>
    <Color x:Key="ErrorColor">#B3261E</Color>
</ResourceDictionary>
```

Merge in `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <MaterialTheme ColorOverrideSource="ms-appx:///Styles/ColorPaletteOverride.xaml"
                           FontOverrideSource="ms-appx:///Styles/CustomFonts.xaml" />
            <ToolkitResources />
            <MaterialToolkitResources />
            <ResourceDictionary Source="ms-appx:///Converters/Converters.xaml" />
            <ResourceDictionary Source="ms-appx:///Styles/Button.xaml" />
            <ResourceDictionary Source="ms-appx:///Styles/NavigationBar.xaml" />
            <ResourceDictionary Source="ms-appx:///Styles/FeedView.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

## Responsive Layout

Use `utu:Responsive` for adaptive layouts:

```xml
<utu:AutoLayout Orientation="{utu:Responsive Normal=Vertical, Wide=Horizontal}">
    <!-- Content adapts to screen width -->
</utu:AutoLayout>

<TextBlock TextAlignment="{utu:Responsive Normal=Center, Wide=Start}" />
```

## Authentication Flow (Gateway Integration)

1. User enters credentials on `LoginPage`
2. `LoginModel` calls the authentication provider configured in `App.xaml.host.cs`
3. On success, tokens are stored via `TokenCacheExtensions`
4. The Kiota HTTP client (`{Project}ApiClient`) automatically attaches the Bearer token to all requests
5. All API calls go through the **Gateway** (`GatewayBaseUrl`), which:
   - Validates the user token (Entra External)
   - Acquires a service-to-service token (token relay)
   - Forwards the request + original user claims to the backend API

## Kiota Client Generation

Generate the HTTP client from the Gateway's OpenAPI spec:

```bash
# Generate or regenerate the Kiota client from the Gateway OpenAPI spec
kiota generate \
  --openapi https://localhost:7200/swagger/v1/swagger.json \
  --language CSharp \
  --output ./Client \
  --class-name {Project}ApiClient \
  --namespace-name {Project}.UI.Client
```

This produces typed request builders that map 1:1 to Gateway endpoints:

```csharp
// Usage in a service
var items = await api.Api.{Entity}.GetAsync(cancellationToken: ct);
var item = await api.Api.{Entity}[id].GetAsync(cancellationToken: ct);
await api.Api.{Entity}.PostAsync(data, cancellationToken: ct);
await api.Api.{Entity}[id].PutAsync(data, cancellationToken: ct);
await api.Api.{Entity}[id].DeleteAsync(cancellationToken: ct);
```

## Value Converters

Common converters registered in `Converters/Converters.xaml`.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.UI/Converters/Converters.xaml` (ResourceDictionary), `sampleapp/src/TaskFlow/TaskFlow.UI/Converters/StringFormatConverter.cs`, and `sampleapp/src/TaskFlow/TaskFlow.UI/Converters/BoolInverterConverter.cs`.

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:converters="using:{Project}.UI.Converters">
    <converters:StringFormatter x:Key="StringFormatter" />
    <converters:BoolInverter x:Key="BoolInverter" />
    <converters:FromNullToVisibilityConverter x:Key="NullToVisibility" />
</ResourceDictionary>
```

Usage in XAML:

```xml
<TextBlock Text="{Binding Count, Converter={StaticResource StringFormatter},
                  ConverterParameter='{}{0} items'}" />
```

## Localization

String resources live in `Strings/{locale}/Resources.resw`. Use `x:Uid` for XAML or `IStringLocalizer` in code.

```xml
<TextBlock x:Uid="WelcomeTitle" />
```

## Key Patterns Summary

| Pattern | Convention |
|---------|-----------|
| Presentation layer | MVUX `partial record` models — not ViewModels |
| Data binding | `IFeed<T>` / `IListFeed<T>` for read-only, `IState<T>` / `IListState<T>` for mutable |
| Commands | Public `ValueTask` methods on model — auto-generated as commands |
| Navigation | Route-based via `INavigator.NavigateRouteAsync()` |
| Navigation requests | Prefer `uen:Navigation.Request` (+ qualifiers like `-/`, `!`, `!back`) for direct UI navigation |
| HTTP client | Kiota-generated, configured to point at the YARP Gateway |
| API mode | Scaffold mock + live HTTP paths; use config switch (`Features:UseMocks`) |
| Authentication | Entra External via Gateway — token relay for API calls |
| Theming | Material Design via `Uno.Material` + color palette overrides |
| Layout | `utu:AutoLayout` + `utu:Responsive` for adaptive UI |
| Lists | `uer:FeedView` + `muxc:ItemsRepeater` |
| Cross-model sync | `CommunityToolkit.Mvvm.Messaging` + `.Observe()` |
| Code-behind | Minimal — constructor + `InitializeComponent()` only |
| Models | Client-side records wrapping Kiota DTOs |
| Services | Interface + implementation, injected via `ConfigureServices` |

---

## Uno Key-Equality Analyzer Notes

Records with an `Id` property can trigger the Uno key-equality source generator diagnostic **KE0001**. This happens because Uno's MVUX generator looks for key properties on `partial record` types to support reactive collection identity.

### Practical Guidance

- **Use `partial record`** for types intended for MVUX presentation models or client-side business models where Uno-generated key equality is desired.
- **Use `class`** for transport DTOs, internal DTO shells, or wire-format types where Uno source generation is not needed (e.g., Kiota-generated models).
- Keep model intent explicit: if a type exists solely as a data carrier and won't participate in MVUX feeds/states, declare it as a `class` to avoid KE0001 churn during scaffolding.
- If you see KE0001 on a record type that genuinely needs to be a `partial record`, ensure it follows the Uno conventions (Guid `Id` property, `IEntityBase` marker) and the diagnostic will resolve.

### Quick Decision

| Model intent | Use | KE0001 risk |
|-------------|-----|-------------|
| MVUX presentation model | `partial record` | Expected — Uno generates key equality |
| Client-side business model with `Id` | `partial record` | Expected — Uno generates key equality |
| Wire-format / Kiota DTO | `class` | None — no source generation |
| Internal scaffolding shell | `class` | None — no source generation |

---

## Live/Mock Service Toggle Pattern

The mock/live switch should be a **single, config-driven decision** made at startup — not a runtime probe that tries multiple URL prefixes.

### Required Pattern

1. **Single config toggle:** `Features:UseMocks` in `appsettings.json` (or environment override).
2. **Deterministic fallback:** If the toggle is missing or unreadable, default to live mode and log a warning — never silently fall back to mocks in production.
3. **No dual-prefix probing:** Once the `GatewayApiBasePath` is established (see [gateway.md](gateway.md) Path Prefix Normalization), use that single path. Do not try `/api/v1/...` then fall back to `/v1/...` in production logic.
4. **Logging:** Log which mode (mock vs. live) was selected at startup so troubleshooting is straightforward.

```csharp
// Startup decision — one branch, no probing
var useMocks = context.Configuration.GetValue<bool>("Features:UseMocks");
if (useMocks)
{
    builder.ConfigurePrimaryAndInnerHttpMessageHandler<MockHttpMessageHandler>();
    logger.LogInformation("HTTP client configured with mock handler");
}
else
{
    logger.LogInformation("HTTP client configured for live Gateway at {Url}",
        context.Configuration["GatewayBaseUrl"]);
}
```

---

## Verification

After generating Uno UI code, confirm:

- [ ] Project uses `Uno.Sdk` with MVUX pattern (not MVVM)
- [ ] Models are `partial record` with `IFeed<T>` / `IListFeed<T>` properties
- [ ] XAML pages use `FeedView` for async data binding with loading/error states
- [ ] Navigation uses `INavigator` from `Uno.Extensions.Navigation`
- [ ] Services are registered in `ConfigureServices` and injected via constructor
- [ ] Client-side DTOs wrap Kiota-generated types (not shared with API project)
- [ ] `utu:AutoLayout` and `utu:Responsive` used for adaptive layouts
- [ ] Authentication flows through Gateway token relay (not direct Entra calls from UI)
- [ ] Cross-references: API endpoints match Kiota client generation, Gateway CORS allows UI origin per [gateway.md](gateway.md)
