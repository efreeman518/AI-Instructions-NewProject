using UIKit;
using Uno.UI.Hosting;

namespace TaskFlow.UI.iOS;

public class EntryPoint
{
    public static void Main(string[] args)
    {
        App.InitializeLogging();
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAppleUIKit()
            .Build();
        host.Run();
    }
}
