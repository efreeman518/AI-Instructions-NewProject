// ═══════════════════════════════════════════════════════════════
// Pattern: IntegrationTestBase — real Bootstrapper DI with test overrides.
// Uses the same service registrations as production, replacing only
// IRequestContext with a test identity.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EF.Common;
using TaskFlow.Bootstrapper;

namespace Test.Support;

/// <summary>
/// Pattern: Integration test base — real DI from Bootstrapper.
/// Provides full pipeline: Bootstrapper → DI → Service → Repository → DbContext.
/// Only IRequestContext is replaced with a test identity.
/// </summary>
public abstract class IntegrationTestBase
{
    protected static readonly IConfigurationRoot Config =
        Utility.BuildConfiguration()
            .AddUserSecrets<IntegrationTestBase>()
            .Build();

    protected static readonly IConfigurationSection TestConfigSection =
        Config.GetSection("TestSettings");

    protected static IServiceProvider Services = null!;
    protected static IServiceScope ServiceScope = null!;
    protected static ILogger<IntegrationTestBase> Logger = null!;
    protected static ServiceCollection ServicesCollection = [];

    /// <summary>
    /// Pattern: ConfigureServices — Bootstrapper chain + test overrides.
    /// Call this from [ClassInitialize] or [TestInitialize].
    /// </summary>
    protected static void ConfigureServices(string testContextName)
    {
        // Pattern: Real Bootstrapper registrations — same DI as production.
        ServicesCollection
            .RegisterInfrastructureServices(Config)
            .RegisterBackgroundServices(Config)
            .RegisterDomainServices(Config)
            .RegisterApplicationServices(Config);

        // Pattern: Testing overrides.
        ServicesCollection.AddLogging(configure =>
            configure.ClearProviders().AddConsole().AddDebug());

        // Pattern: Replace IRequestContext with test identity.
        // Uses the test context name for traceability in logs.
        ServicesCollection.AddTransient<IRequestContext>(provider =>
        {
            var correlationId = Guid.NewGuid().ToString();
            return new RequestContext(
                correlationId,
                $"Test-{testContextName}-{correlationId}",
                null, []);
        });

        Services = ServicesCollection.BuildServiceProvider(validateScopes: true);
        ServiceScope = Services.CreateScope();
        Logger = Services.GetRequiredService<ILogger<IntegrationTestBase>>();
    }
}
