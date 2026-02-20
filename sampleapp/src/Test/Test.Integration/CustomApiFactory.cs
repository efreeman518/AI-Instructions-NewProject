// ═══════════════════════════════════════════════════════════════
// Pattern: CustomApiFactory — WebApplicationFactory<TProgram> for integration tests.
// Overrides the host to use test DB, remove IHostedService registrations,
// and apply appsettings-test.json configuration.
// Reuses DbSupport.ConfigureServicesTestDB for DB swap (InMemory or SQL Server).
// ═══════════════════════════════════════════════════════════════

using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Test.Support;

namespace Test.Integration;

/// <summary>
/// Pattern: Custom WebApplicationFactory — overrides the real API host for testing.
/// - Loads appsettings-test.json for test-specific configuration
/// - Removes all IHostedService (BackgroundService) registrations to prevent
///   background processing from interfering with endpoint tests
/// - Swaps production DB registrations with InMemory or SQL Server test DB
/// - Uses "Development" environment for consistent pipeline behavior
/// </summary>
/// <typeparam name="TProgram">
/// The API's entry-point class (typically <c>Program</c> from TaskFlow.Api).
/// WebApplicationFactory uses this type to discover the application's startup configuration.
/// </typeparam>
/// <param name="dbConnectionString">
/// Pass "UseInMemoryDatabase" for fast in-process tests (default from appsettings-test.json),
/// or a SQL Server connection string for TestContainers / existing SQL instance.
/// Null/empty = no DB swap (keeps production registrations).
/// </param>
public class CustomApiFactory<TProgram>(string? dbConnectionString = null)
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IConfiguration config = null!;

        builder
            // Pattern: Use Development environment — matches local dev pipeline configuration.
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) =>
            {
                // Pattern: Layer test config on top of base appsettings.
                // appsettings-test.json provides TestSettings:DBSource, DBName, etc.
                configuration.AddJsonFile(
                    Path.Combine(Directory.GetCurrentDirectory(), "appsettings-test.json"),
                    optional: false);
                config = configuration.Build();
            })
            .ConfigureServices(services =>
            {
                // Pattern: Remove ALL IHostedService registrations.
                // BackgroundServices (QueuedBackgroundService, etc.) would interfere
                // with endpoint tests by processing items concurrently.
                services.RemoveAll<IHostedService>();

                // Pattern: DB swap — replace production DbContexts with test instances.
                // Uses the same generic ConfigureServicesTestDB from Test.Support.DbSupport.
                var dbName = config.GetValue<string>("TestSettings:DBName")
                    ?? "Test.Integration.TestDB";
                DbSupport.ConfigureServicesTestDB<TaskFlowDbContextTrxn, TaskFlowDbContextQuery>(
                    services, dbConnectionString, dbName);
            });
    }
}
