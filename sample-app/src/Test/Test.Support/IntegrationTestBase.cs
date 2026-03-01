using EF.Common.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskFlow.Bootstrapper;

namespace Test.Support;

/// <summary>
/// Base class for integration tests that test Domain, Application, and Infrastructure services/logic
/// (not HTTP endpoints — use EndpointTestBase for that).
/// Wires up the full bootstrapper service registrations with test overrides.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/IntegrationTestBase.cs
/// </summary>
public abstract class IntegrationTestBase
{
    protected static readonly IConfigurationRoot Config =
        Utility.BuildConfiguration().AddUserSecrets<IntegrationTestBase>().Build();

    protected static readonly IConfigurationSection TestConfigSection =
        Config.GetSection("TestSettings");

    // MSTest requires static ClassInitialize/ClassCleanup methods
#pragma warning disable S2223
#pragma warning disable CA2211
    protected static IServiceProvider Services = null!;
    protected static IServiceScope ServiceScope = null!;
    protected static ILogger<IntegrationTestBase> Logger = null!;
    protected static ServiceCollection ServicesCollection = [];
#pragma warning restore CA2211
#pragma warning restore S2223

    /// <summary>
    /// Configure the test class; runs once before any test via [ClassInitialize].
    /// Registers all bootstrapper services (infrastructure, domain, application, background).
    /// </summary>
    protected static void ConfigureServices(string testContextName)
    {
        // Bootstrapper service registrations — infrastructure, domain, application
        ServicesCollection
            .RegisterInfrastructureServices(Config)
            .RegisterBackgroundServices(Config)
            .RegisterDomainServices(Config)
            .RegisterApplicationServices(Config);

        // Register services for testing not already registered in the bootstrapper
        ServicesCollection.AddLogging(configure =>
            configure
                .ClearProviders()
                .AddConsole()
                .AddDebug());

        // IRequestContext — replace the bootstrapper-registered non-HTTP 'BackgroundService' registration;
        // injected into repositories for audit/tenant context
        ServicesCollection.AddTransient<IRequestContext<string, Guid?>>(provider =>
        {
            var correlationId = Guid.NewGuid().ToString();
            return new RequestContext<string, Guid?>(
                correlationId,
                $"Test.Support.IntegrationTestBase-{correlationId}",
                null, // TenantId — null for tests unless explicitly set
                []);
        });

        // Build IServiceProvider for subsequent use finding/injecting services
        Services = ServicesCollection.BuildServiceProvider(validateScopes: true);
        ServiceScope = Services.CreateScope();
        Logger = Services.GetRequiredService<ILogger<IntegrationTestBase>>();
        Logger.LogInformation("{TestContextName} Base ConfigureServices complete.", testContextName);
    }
}

/// <summary>
/// Simple IRequestContext implementation for tests that don't have HTTP context.
/// </summary>
public class RequestContext<TAudit, TTenant>(
    string correlationId,
    TAudit auditId,
    TTenant tenantId,
    List<string> roles) : IRequestContext<TAudit, TTenant>
{
    public string CorrelationId { get; } = correlationId;
    public TAudit AuditId { get; } = auditId;
    public TTenant TenantId { get; } = tenantId;
    public List<string> Roles { get; } = roles;
    public bool RoleExists(string role) => Roles.Contains(role);
}
