using Infrastructure.Data;
using EF.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.RateLimiting;
using TaskFlow.Bootstrapper;
using Test.Support;

namespace Test.Endpoints;

/// <summary>
/// Custom WebApplicationFactory that swaps the real database for InMemory or TestContainer
/// and disables background hosted services for deterministic endpoint testing.
///
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Endpoints/CustomApiFactory.cs
/// See also: https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests
/// </summary>
/// <typeparam name="TProgram">The API entry-point class (typically Program).</typeparam>
public class CustomEndpointApiFactory<TProgram>(string? dbConnectionString = null)
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IConfiguration config = null!;
        bool isInMemory = dbConnectionString is null or "UseInMemoryDatabase";

        string env = builder.GetSetting("ASPNETCORE_ENVIRONMENT") ?? "Development";

        builder
            .UseEnvironment(env)
            .ConfigureAppConfiguration((hostingContext, configuration) =>
            {
                // Override API settings with test settings
                configuration.AddJsonFile(Utility.ResolveJsonConfigPath("appsettings-test.json"));

                if (!isInMemory)
                {
                    // For TestContainer/SQL mode: override connection strings so the
                    // bootstrapper's full DB pipeline (PooledDbContextFactory + DbContextScopedFactory
                    // + AuditInterceptor) runs with the test connection string. This preserves
                    // the production audit/save path, fixing "SaveChangesAsync throws NotImplementedException".
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:TaskFlowDbContextTrxn"] = dbConnectionString,
                        ["ConnectionStrings:TaskFlowDbContextQuery"] = dbConnectionString
                    });
                }

                config = configuration.Build();
            })
            .ConfigureTestServices(services =>
            {
                if (config.GetValue("TestSettings:DisableHostedServices", true))
                {
                    RemoveKnownHostedServices(services);
                }

                // Override IRequestContext with a test context that has a known TenantId
                // so the global tenant query filter matches entities created by tests.
                services.AddScoped<IRequestContext<string, Guid?>>(provider =>
                    new EF.Common.Contracts.RequestContext<string, Guid?>(
                        Guid.NewGuid().ToString(),
                        "Test.Endpoints",
                        SharedTestFactory.TestTenantId,
                        []));

                // Disable rate limiting for tests to prevent 429 TooManyRequests
                // when running many sequential requests (e.g., FullStateMachine test).
                services.AddRateLimiter(options =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
                        _ => RateLimitPartition.GetNoLimiter("test"));
                });

#if DEBUG
                // Include exception details in ProblemDetails responses for test diagnostics.
                // Only enabled in DEBUG to avoid leaking stack traces in CI/release builds.
                services.AddProblemDetails(options =>
                {
                    options.CustomizeProblemDetails = context =>
                    {
                        var exFeature = context.HttpContext.Features
                            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                        if (exFeature?.Error != null)
                        {
                            context.ProblemDetails.Detail = exFeature.Error.ToString();
                        }
                    };
                });
#endif

                if (isInMemory)
                {
                    // InMemory mode: swap out DbContexts entirely (read-only tests).
                    // Write operations will go Inconclusive since the audit pipeline is not wired.
                    string dbName = config.GetValue<string>("TestSettings:DBName") ?? "Test.Endpoints.TestDB";
                    DbSupport.ConfigureServicesTestDB<TaskFlowDbContextTrxn, TaskFlowDbContextQuery>(
                        services, "UseInMemoryDatabase", dbName);
                }
                // else: TestContainer/SQL mode — the bootstrapper already wired the full
                // DB pipeline with the overridden connection strings. No swap needed.
            });
    }

    private static void RemoveKnownHostedServices(IServiceCollection services)
    {
        var descriptorsToRemove = services
            .Where(descriptor =>
                (descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType != null)
                || descriptor.ServiceType == typeof(IStartupTask))
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }
    }
}
