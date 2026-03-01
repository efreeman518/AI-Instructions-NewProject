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

namespace Test.Integration;

/// <summary>
/// Custom WebApplicationFactory that supports InMemory or TestContainer database modes.
/// For TestContainer mode: overrides connection strings via AddInMemoryCollection so the
/// bootstrapper's full DB pipeline (PooledDbContextFactory + DbContextScopedFactory + AuditInterceptor) runs.
/// For InMemory mode: swaps DbContext registrations to EF InMemory provider.
/// Pattern from CustomEndpointApiFactory in Test.Endpoints.
/// </summary>
public class CustomApiFactory(string? dbConnectionString = null) : WebApplicationFactory<Program>
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
                configuration.AddJsonFile(Utility.ResolveJsonConfigPath("appsettings.json"), optional: true);

                if (!isInMemory)
                {
                    // For TestContainer/SQL mode: override connection strings so the
                    // bootstrapper's full DB pipeline (PooledDbContextFactory + DbContextScopedFactory
                    // + AuditInterceptor) runs with the test connection string.
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

                // Override IRequestContext with test tenant ID
                services.AddScoped<IRequestContext<string, Guid?>>(provider =>
                    new EF.Common.Contracts.RequestContext<string, Guid?>(
                        Guid.NewGuid().ToString(),
                        "Test.Integration",
                        SharedTestFactory.TestTenantId,
                        []));

                // Disable rate limiting for tests
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
                    string dbName = config.GetValue<string>("TestSettings:DBName") ?? "Test.Integration.TestDB";
                    DbSupport.ConfigureServicesTestDB<TaskFlowDbContextTrxn, TaskFlowDbContextQuery>(
                        services, "UseInMemoryDatabase", dbName);
                }
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
