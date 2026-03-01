using Uno.Extensions.Authentication;

namespace TaskFlow.UI.Presentation;

/// <summary>
/// Login model — handles authentication credentials and login flow.
/// Uses Uno.Extensions.Authentication custom provider (configured in App.xaml.host.cs).
/// Production upgrade: Replace AddCustom with AddMsal for Entra External ID (CIAM).
/// </summary>
public partial record LoginModel(IDispatcher Dispatcher, INavigator Navigator, IAuthenticationService Auth)
{
    public IState<Credentials> UserCredentials => State<Credentials>.Value(this, () => new Credentials());

    public ICommand Login => Command.Create(b => b
        .Given(UserCredentials)
        .When(CanLogin)
        .Then(DoLogin));

    private static bool CanLogin(Credentials? creds) =>
        !string.IsNullOrWhiteSpace(creds?.Username) && !string.IsNullOrWhiteSpace(creds?.Password);

    private async ValueTask DoLogin(Credentials creds, CancellationToken ct)
    {
        var loggedIn = await Auth.LoginAsync(Dispatcher, new Dictionary<string, string>
        {
            { "Username", creds.Username! },
            { "Password", creds.Password! },
        }, provider: null, ct);

        if (loggedIn)
        {
            await Navigator.NavigateRouteAsync(this, route: "Main", cancellation: ct);
        }
    }
}
