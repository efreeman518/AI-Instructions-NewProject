namespace TaskFlow.UI.Presentation;

/// <summary>
/// Settings model — app settings, preferences, and logout.
/// </summary>
public partial record SettingsModel
{
    private readonly INavigator _navigator;
    private readonly IOptions<AppConfig> _appConfig;
    private readonly Uno.Extensions.Authentication.IAuthenticationService _authService;
    private readonly IDispatcher _dispatcher;

    public SettingsModel(
        INavigator navigator,
        IOptions<AppConfig> appConfig,
        Uno.Extensions.Authentication.IAuthenticationService authService,
        IDispatcher dispatcher)
    {
        _navigator = navigator;
        _appConfig = appConfig;
        _authService = authService;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Application title from configuration.
    /// </summary>
    public IFeed<string> AppTitle => Feed.Async<string>(async ct => _appConfig.Value.Title);

    /// <summary>
    /// App version display.
    /// </summary>
    public IFeed<string> Version => Feed.Async<string>(async ct => "1.0.0");

    /// <summary>
    /// Sign out and return to login screen.
    /// </summary>
    public async ValueTask Logout(CancellationToken ct)
    {
        await _authService.LogoutAsync(_dispatcher, ct);
        await _navigator.NavigateRouteAsync(this, route: "Login", cancellation: ct);
    }

    /// <summary>
    /// Navigate back.
    /// </summary>
    public async ValueTask GoBack(CancellationToken ct) =>
        await _navigator.NavigateBackAsync(this, cancellation: ct);
}
