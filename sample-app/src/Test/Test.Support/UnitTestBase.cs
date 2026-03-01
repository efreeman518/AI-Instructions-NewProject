using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Test.Support;

/// <summary>
/// Base class for unit tests providing common test infrastructure.
/// </summary>
public abstract class UnitTestBase
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected ILogger Logger { get; private set; } = null!;

    [TestInitialize]
    public virtual void TestInitialize()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();
        Logger = ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
    }

    [TestCleanup]
    public virtual void TestCleanup()
    {
        if (ServiceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Override to register additional services for the test.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services) { }
}
