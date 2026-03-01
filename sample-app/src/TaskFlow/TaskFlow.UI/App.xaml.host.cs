using Microsoft.Extensions.Configuration;
using TaskFlow.UI.Infrastructure;
using Uno.Extensions.Authentication;

namespace TaskFlow.UI;

/// <summary>
/// App host configuration — Uno Extensions builder.
/// Auth pattern: Entra External ID (CIAM) via Portal pattern.
/// API calls: Gateway only (never direct API).
/// </summary>
public partial class App : Application
{
    private void ConfigureAppBuilder(IApplicationBuilder builder)
    {
        builder
            .UseToolkitNavigation()
            .Configure(host => host
                // ─── AUTHENTICATION ────────────────────────────────────────────
                // Current: Custom dev-mode provider (accepts any non-empty credentials).
                //
                // To upgrade to Entra External ID (CIAM) for production:
                //
                //   1. Register an app in Entra External ID (portal.azure.com → External Identities)
                //      - Set redirect URI: "taskflow://auth" (mobile) + "http://localhost" (WASM)
                //      - Note the ClientId and Authority URL
                //
                //   2. Update appsettings.json "EntraExternal" section with real values:
                //        "Authority": "https://<tenant>.ciamlogin.com/<tenant>.onmicrosoft.com"
                //        "ClientId":  "<your-app-client-id>"
                //
                //   3. Replace .AddCustom() below with .AddMsal():
                //        auth.AddMsal(msal => msal
                //            .WithClientId(config["EntraExternal:ClientId"]!)
                //            .WithAuthority(config["EntraExternal:Authority"]!)
                //            .WithScopes(config.GetSection("Gateway:Scopes").Get<string[]>()!))
                //
                //   4. In csproj, add 'AuthenticationMsal' to <UnoFeatures> (replaces 'Authentication')
                //
                //   5. Update AuthTokenHandler — no changes needed, it already reads from ITokenCache
                //
                //   6. Configure Gateway's "TaskFlowGateway_EntraID" section in Gateway appsettings.json
                //      (see Gateway/RegisterGatewayServices.cs for the expected config shape)
                // ───────────────────────────────────────────────────────────────────
                .UseAuthentication(auth =>
                    auth.AddCustom(custom =>
                        custom.Login(
                            async (sp, dispatcher, tokenCache, credentials, cancellationToken) =>
                            {
                                var hasUser = credentials.TryGetValue("Username", out var username);
                                var hasPass = credentials.TryGetValue("Password", out var password);

                                if (!hasUser || string.IsNullOrWhiteSpace(username))
                                    return default;

                                // DEV MODE: Accept any non-empty credentials and issue a fake token.
                                // Replace this entire .AddCustom() block with .AddMsal() for production
                                // (see upgrade steps above).
                                var accessToken = $"dev-token-{username}-{Guid.NewGuid():N}";
                                credentials["AccessToken"] = accessToken;
                                return credentials;
                            }))
                )
                .UseHttp((context, services) =>
                {
                    services.AddTransient<AuthTokenHandler>();
                    services.AddTransient<MockHttpMessageHandler>();

                    // HttpClient for Gateway — all API calls go through the Gateway
                    services.AddHttpClient("TaskFlowGateway", client =>
                    {
                        var gatewayUrl = context.Configuration["Gateway:BaseUrl"] ?? "https://localhost:7200";
                        client.BaseAddress = new Uri(gatewayUrl);
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                    })
#if USE_MOCKS
                    .ConfigurePrimaryHttpMessageHandler<MockHttpMessageHandler>();
#else
                    .AddHttpMessageHandler<AuthTokenHandler>();
#endif
                })
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder.SetMinimumLevel(
                        context.HostingEnvironment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning);
                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                        .Section<Credentials>()
                )
                .UseLocalization()
                .UseSerialization(configure: ConfigureSerialization)
                .ConfigureServices((context, services) =>
                {
                    services
                        .AddSingleton<IMessenger, WeakReferenceMessenger>()
                        .AddSingleton<ITodoItemService, TodoItemService>()
                        .AddSingleton<ICategoryService, CategoryService>()
                        .AddSingleton<ITagService, TagService>()
                        .AddSingleton<ITeamService, TeamService>();
                })
                .ConfigureAppConfiguration(config =>
                {
                    var defaults = new Dictionary<string, string?>
                    {
                        { HostingConstants.LaunchUrlKey, "" }
                    };
                    config.AddInMemoryCollection(defaults);
                })
                .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes));
    }

    private void ConfigureSerialization(HostBuilderContext context, IServiceCollection services)
    {
        services.AddJsonTypeInfo(AppConfigContext.Default.String);
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellModel)),
            new ViewMap<MainPage, MainModel>(),
            new ViewMap<LoginPage, LoginModel>(ResultData: typeof(Credentials)),
            new ViewMap<HomePage, HomeModel>(),
            new ViewMap<TodoItemListPage, TodoItemListModel>(),
            new ViewMap<TodoItemDetailPage, TodoItemDetailModel>(Data: new DataMap<TodoItemSummary>()),
            new ViewMap<TodoItemEditPage, TodoItemEditModel>(Data: new DataMap<TodoItemSummary>()),
            new ViewMap<CategoryListPage, CategoryListModel>(),
            new ViewMap<CategoryEditPage, CategoryEditModel>(Data: new DataMap<CategorySummary>()),
            new ViewMap<TagListPage, TagListModel>(),
            new ViewMap<TagEditPage, TagEditModel>(Data: new DataMap<TagSummary>()),
            new ViewMap<TeamListPage, TeamListModel>(),
            new ViewMap<TeamEditPage, TeamEditModel>(Data: new DataMap<TeamSummary>()),
            new ViewMap<SettingsPage, SettingsModel>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                Nested:
                [
                    new RouteMap("Login", View: views.FindByViewModel<LoginModel>()),
                    new RouteMap("Main", View: views.FindByViewModel<MainModel>(), Nested:
                    [
                        new RouteMap("Home", View: views.FindByViewModel<HomeModel>(), IsDefault: true),
                        new RouteMap("TodoItems", View: views.FindByViewModel<TodoItemListModel>()),
                        new RouteMap("TodoItemDetail", View: views.FindByViewModel<TodoItemDetailModel>()),
                        new RouteMap("TodoItemEdit", View: views.FindByViewModel<TodoItemEditModel>()),
                        new RouteMap("Categories", View: views.FindByViewModel<CategoryListModel>()),
                        new RouteMap("CategoryEdit", View: views.FindByViewModel<CategoryEditModel>()),
                        new RouteMap("Tags", View: views.FindByViewModel<TagListModel>()),
                        new RouteMap("TagEdit", View: views.FindByViewModel<TagEditModel>()),
                        new RouteMap("Teams", View: views.FindByViewModel<TeamListModel>()),
                        new RouteMap("TeamEdit", View: views.FindByViewModel<TeamEditModel>()),
                    ]),
                    new RouteMap("Settings", View: views.FindByViewModel<SettingsModel>()),
                ]
            )
        );
    }
}
