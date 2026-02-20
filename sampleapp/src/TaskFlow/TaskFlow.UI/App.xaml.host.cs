// ═══════════════════════════════════════════════════════════════
// Pattern: App.xaml.host.cs — IApplicationBuilder configuration.
// Central setup for authentication, HTTP (Kiota), navigation, DI,
// serialization, and logging.
//
// Key architectural decisions:
// 1. Authentication flows through the Gateway (Entra External / CIAM)
// 2. Kiota-generated client is configured with GatewayBaseUrl
// 3. Mock/live API mode switchable via Features:UseMocks config
// 4. All business services registered in ConfigureServices
// 5. Route-based navigation with MVUX model mapping
// ═══════════════════════════════════════════════════════════════

using TaskFlow.UI.Business.Services.TodoItems;
using TaskFlow.UI.Business.Services.Categories;
using TaskFlow.UI.Client;
using TaskFlow.UI.Presentation;
using TaskFlow.UI.Views;
using Uno.Extensions.Http;

namespace TaskFlow.UI;

public partial class App : Application
{
    /// <summary>
    /// Pattern: Central host configuration — called from OnLaunched.
    /// Configures auth, HTTP, navigation, DI, and logging in a single fluent chain.
    /// </summary>
    private void ConfigureAppBuilder(IApplicationBuilder builder)
    {
        builder
            .UseToolkitNavigation()
            .Configure(host => host
                // ── Authentication — Entra External via Gateway ──────
                .UseAuthentication(auth =>
                    auth.AddCustom(custom =>
                    {
                        // Pattern: Custom auth provider — Gateway handles Entra External flow.
                        // The UI sends credentials to the Gateway's /auth/login endpoint.
                        custom.Login(async (sp, dispatcher, credentials, ct) =>
                            await ProcessCredentials(credentials));
                    }, name: "CustomAuth")
                )
                // ── HTTP — Kiota client pointing at Gateway ──────────
                .UseHttp((context, services) =>
                {
                    // Pattern: Register mock handler for testing without a running Gateway.
                    services.AddTransient<MockHttpMessageHandler>();

                    services.AddKiotaClient<TaskFlowApiClient>(
                        context,
                        options: new EndpointOptions
                        {
                            Url = context.Configuration["GatewayBaseUrl"]
                                  ?? "https://localhost:7200"
                        },
                        configure: (clientBuilder, endpoint) =>
                        {
                            // Pattern: Mock/live switch — controlled by config.
                            var useMocks = context.Configuration.GetValue<bool>("Features:UseMocks");
                            if (useMocks)
                            {
                                clientBuilder.ConfigurePrimaryAndInnerHttpMessageHandler<MockHttpMessageHandler>();
                            }
                        });
                })
                // ── Logging ──────────────────────────────────────────
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder.SetMinimumLevel(
                        context.HostingEnvironment.IsDevelopment()
                            ? LogLevel.Information
                            : LogLevel.Warning);
                }, enableUnoLogging: true)
                // ── Configuration ────────────────────────────────────
                .UseConfiguration(configure: configBuilder =>
                    configBuilder.EmbeddedSource<App>()
                )
                // ── Localization ─────────────────────────────────────
                .UseLocalization()
                // ── Serialization ────────────────────────────────────
                .UseSerialization()
                // ── DI — Register business services ──────────────────
                .ConfigureServices((context, services) =>
                {
                    // Pattern: All business services as singletons — shared across pages.
                    services
                        .AddSingleton<ITodoItemService, TodoItemService>()
                        .AddSingleton<ICategoryService, CategoryService>()
                        .AddSingleton<IMessenger, WeakReferenceMessenger>();
                })
                // ── Navigation — MVUX model ↔ page mapping ──────────
                .UseNavigation(
                    ReactiveViewModelMappings.ViewModelMappings,
                    RegisterRoutes
                ));
    }

    /// <summary>
    /// Pattern: Route registration — maps MVUX models to XAML pages.
    /// Uses ViewMap for simple pages, DataViewMap when navigation data is passed.
    /// </summary>
    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellModel)),
            new ViewMap<MainPage, MainModel>(),
            new ViewMap<TodoItemListPage, TodoItemListModel>(),
            new DataViewMap<TodoItemDetailPage, TodoItemDetailModel, TodoItem>(),
            new ViewMap<CreateTodoItemPage, CreateTodoItemModel>(),
            new ViewMap<CategoryListPage, CategoryListModel>(),
            new ViewMap<SettingsPage, SettingsModel>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellModel>(),
                Nested:
                [
                    new RouteMap("Main", View: views.FindByViewModel<MainModel>(),
                        Nested:
                        [
                            new RouteMap("TodoItemList",
                                View: views.FindByViewModel<TodoItemListModel>(), IsDefault: true),
                            new RouteMap("TodoItemDetail",
                                View: views.FindByViewModel<TodoItemDetailModel>()),
                            new RouteMap("CreateTodoItem",
                                View: views.FindByViewModel<CreateTodoItemModel>()),
                            new RouteMap("CategoryList",
                                View: views.FindByViewModel<CategoryListModel>()),
                        ]),
                    new RouteMap("Settings",
                        View: views.FindByViewModel<SettingsModel>()),
                ]
            )
        );
    }

    /// <summary>
    /// Pattern: Process login credentials — send to Gateway /auth/login endpoint.
    /// Returns token dictionary on success, empty on failure.
    /// </summary>
    private static async ValueTask<IDictionary<string, string>> ProcessCredentials(
        IDictionary<string, string> credentials)
    {
        // Pattern: In a real app, this calls the Gateway auth endpoint.
        // For the sample, return contrived tokens for pattern demonstration.
        return new Dictionary<string, string>
        {
            ["AccessToken"] = "sample-access-token",
            ["RefreshToken"] = "sample-refresh-token"
        };
    }
}
