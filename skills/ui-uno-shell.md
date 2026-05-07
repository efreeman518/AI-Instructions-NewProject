# Uno Platform UI — Shell, Project Setup, App Hosting

App-shell scaffolding and project-file rules for an Uno application. Loaded during Phase 5c when an Uno UI project is in scope.

Companion files:
- [ui-uno.md](ui-uno.md) — index + decision table
- [ui-uno-mvux.md](ui-uno-mvux.md) — MVUX models, routing, XAML, business services, auth
- [ui-uno-platforms.md](ui-uno-platforms.md) — WASM debugging, Android, CI requirements

---

## Packages (Minimum)

> **Important:** With Uno.Sdk, you do NOT list individual Uno packages. The SDK resolves them automatically from `<UnoFeatures>`. The list below is for reference only — to understand what `UnoFeatures` maps to. Do NOT add these `PackageReference` entries to the csproj.

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
    <TargetFrameworks>$(LatestStableTfm)-browserwasm</TargetFrameworks>
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

1. **SDK version**: Use the latest stable `Uno.Sdk` line that supports the project's target .NET TFM. Older 6.0.x SDKs bundle `Uno.Wasm.Bootstrap 8.0.x` which does NOT support .NET 9+; check Uno release notes when bumping the TFM.
2. **TargetFramework clearing**: When `Directory.Build.props` sets a singular `<TargetFramework>` for non-Uno projects, the Uno csproj MUST add `<TargetFramework />` before `<TargetFrameworks>` to clear the inherited value. Otherwise MSBuild merges both, causing `NETSDK1005`.
3. **Entry point**: Uno SDK may not auto-generate `Program.Main` on the latest TFMs. Always include a manual `Program.cs`:

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

Extract `Business/` (Models, Services) and `Client/` into a separate `{Project}.Uno.Core` class library targeting plain single-TFM (the same TFM the rest of the solution targets). This allows unit testing without the Uno SDK.

```text
{Project}.Uno.Core/          <- single-TFM class lib (testable)
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
