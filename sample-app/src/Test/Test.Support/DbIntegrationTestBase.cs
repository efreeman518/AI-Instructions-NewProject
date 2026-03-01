using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Respawn;
using System.Data.Common;
using TaskFlow.Bootstrapper;
using Testcontainers.MsSql;

namespace Test.Support;

/// <summary>
/// Base class for integration tests that need a real database (SQL Server via TestContainers)
/// or InMemory database. Tests Domain, Application, and Infrastructure services/logic — not HTTP endpoints.
///
/// Supports 3 database modes via TestSettings:DBSource config:
///   - "UseInMemoryDatabase" (default) — fast, no Docker required
///   - "TestContainer" — real SQL Server in Docker container (requires Docker/WSL2)
///   - Connection string — uses existing SQL Server instance
///
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/DbIntegrationTestBase.cs
/// </summary>
public abstract class DbIntegrationTestBase : IntegrationTestBase
{
    protected static string _testContextName = null!;
    protected static TaskFlowDbContextBase DbContext => _dbContext;
    private static TaskFlowDbContextBase _dbContext = null!;

    // https://testcontainers.com/guides/testing-an-aspnet-core-web-app/
    private static MsSqlContainer _dbContainer = null!;
    private static string _dbConnectionString = null!;

    // https://github.com/jbogard/Respawn
    private static Respawner _respawner = null!;
    private static DbConnection _dbConnection = null!;

    /// <summary>
    /// Configure the test class; runs once before any test class [MSTest:ClassInitialize].
    /// </summary>
    protected static async Task ConfigureTestInstanceAsync(string testContextName,
        CancellationToken cancellationToken = default)
    {
        _testContextName = $"IntegrationTest-{testContextName}";

        _dbConnectionString = TestConfigSection.GetValue("DBSource", "UseInMemoryDatabase")!;

        if (_dbConnectionString == "TestContainer")
        {
            _dbConnectionString = await StartDbContainerAsync(cancellationToken);
        }

        // Services for DI — register bootstrapper services
        ConfigureServices(_testContextName);

        // Modify the services collection — swap registered DB for test DB
        string dbName = TestConfigSection.GetValue<string>("DBName") ?? "Test.Integration.TestDB";
        DbSupport.ConfigureServicesTestDB<TaskFlowDbContextTrxn, TaskFlowDbContextQuery>(
            ServicesCollection, _dbConnectionString, dbName);

        // Remove IStartupTask registrations (ApplyEFMigrationsStartup, WarmupDependencies)
        // that depend on IDbContextFactory which was removed in the DB swap above.
        var startupTaskDescriptors = ServicesCollection
            .Where(d => d.ServiceType == typeof(IStartupTask))
            .ToList();
        foreach (var descriptor in startupTaskDescriptors)
            ServicesCollection.Remove(descriptor);

        // Rebuild service collection and grab the DbContext
        Services = ServicesCollection.BuildServiceProvider();

        // Create init scope to run migrations/ensure-created
        using (var initScope = Services.CreateScope())
        {
            _dbContext = initScope.ServiceProvider.GetRequiredService<TaskFlowDbContextTrxn>();
            await DbTestLifecycle.EnsureInitializedAsync(_dbContext, cancellationToken);
        }

        if (!_dbConnectionString.Equals("UseInMemoryDatabase", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(_dbConnectionString))
        {
            (_dbConnection, _respawner) = await DbTestLifecycle.OpenRespawnerAsync(
                _dbConnectionString, cancellationToken);
        }

        // Create the long-lived scope for test service resolution
        ServiceScope = Services.CreateScope();
        _dbContext = ServiceScope.ServiceProvider.GetRequiredService<TaskFlowDbContextTrxn>();
        Logger.LogInformation("{TestContextName} ConfigureTestInstanceAsync (DB swap) complete.", testContextName);
    }

    /// <summary>
    /// Start a SQL Server TestContainer. Requires Docker on WSL2.
    /// </summary>
    private static async Task<string> StartDbContainerAsync(CancellationToken cancellationToken = default)
    {
        string dbName = Config.GetValue("TestSettings:DBName", "TestDB")!;
        (_dbContainer, string dbConnectionString) = await DbTestLifecycle.StartDbContainerAsync(
            dbName, cancellationToken: cancellationToken);
        return dbConnectionString;
    }

    /// <summary>
    /// Configure the database for the test; runs before each test [MSTest:TestInitialize].
    /// </summary>
    /// <param name="respawn">Clear all data to schema only using Respawner</param>
    /// <param name="seedFactories">Methods that will run against DbContext to create data</param>
    /// <param name="cancellationToken"></param>
    protected static async Task ResetDatabaseAsync(bool respawn = false,
        List<string>? seedPaths = null, List<Action>? seedFactories = null,
        CancellationToken cancellationToken = default)
    {
        await DbTestLifecycle.ResetDatabaseAsync(
            DbContext,
            Logger,
            _dbConnectionString,
            _respawner,
            _dbConnection,
            respawn,
            dbSnapshotName: null,
            seedPaths,
            seedFactories,
            cancellationToken);
    }

    /// <summary>
    /// Cleanup; runs once after all tests in the class [MSTest:ClassCleanup].
    /// </summary>
    protected static async Task BaseClassCleanup()
    {
        ServiceScope?.Dispose();
        if (_dbConnection != null)
        {
            await _dbConnection.CloseAsync();
            await _dbConnection.DisposeAsync();
        }

        if (_dbContainer != null)
        {
            await _dbContainer.DisposeAsync();
        }
    }
}
