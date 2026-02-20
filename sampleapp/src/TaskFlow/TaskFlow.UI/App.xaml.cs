// ═══════════════════════════════════════════════════════════════
// Pattern: App.xaml.cs — application entry point.
// Minimal code — delegates to ConfigureAppBuilder for DI, auth, HTTP, nav.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI;

public partial class App : Application
{
    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
            );

        ConfigureAppBuilder(builder);

        MainWindow = builder.Window;
        Host = builder.Build();
    }

    internal IHost? Host { get; private set; }
}
