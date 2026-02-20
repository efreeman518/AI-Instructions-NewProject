// ═══════════════════════════════════════════════════════════════
// Pattern: DbIntegrationTestBase — 3-mode DB support with Respawn.
// Modes: InMemory (fast), TestContainer (real SQL), existing SQL Server.
// Respawn resets DB state between tests for non-InMemory modes.
// ═══════════════════════════════════════════════════════════════

using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using TaskFlow.Infrastructure.Repositories;
using Testcontainers.MsSql;

namespace Test.Support;

/// <summary>
/// Pattern: 3-mode DB integration test base.
/// 1. InMemory — fast, no SQL Server needed, limited SQL support.
/// 2. TestContainer — real SQL Server in Docker, full SQL support.
/// 3. Existing SQL — connect to a pre-existing SQL Server instance.
/// Mode is selected via TestSettings:DBSource in appsettings.json.
/// Respawn resets DB between tests for modes 2 and 3.
/// </summary>
public abstract class DbIntegrationTestBase : IntegrationTestBase
{
    protected static TaskFlowDbContextTrxn DbContext = null!;
    private static MsSqlContainer? _dbContainer;
    private static string _dbConnectionString = null!;
    private static Respawner? _respawner;
    private static DbConnection? _dbConnection;

    /// <summary>
    /// Pattern: Configure test DB based on mode selected in config.
    /// Call from [ClassInitialize].
    /// </summary>
    protected static async Task ConfigureTestInstanceAsync(
        string testContextName, CancellationToken cancellationToken = default)
    {
        _dbConnectionString = TestConfigSection
            .GetValue("DBSource", "UseInMemoryDatabase")!;

        // Pattern: Start TestContainer if configured.
        if (_dbConnectionString == "TestContainer")
            _dbConnectionString = await StartDbContainerAsync(cancellationToken);

        ConfigureServices(testContextName);

        // Pattern: Swap registered DbContexts for test DB.
        var dbName = TestConfigSection.GetValue<string>("DBName") ?? "TestDB";
        DbSupport.ConfigureServicesTestDB<TaskFlowDbContextTrxn, TaskFlowDbContextQuery>(
            ServicesCollection, _dbConnectionString, dbName);

        Services = ServicesCollection.BuildServiceProvider();
        DbContext = Services.GetRequiredService<TaskFlowDbContextTrxn>();
        await DbContext.Database.EnsureCreatedAsync(cancellationToken);

        // Pattern: Initialize Respawn for non-InMemory databases.
        if (!DbContext.Database.IsInMemory())
        {
            _dbConnection = new SqlConnection(_dbConnectionString);
            await _dbConnection.OpenAsync(cancellationToken);
            _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
            {
                TablesToIgnore = ["__EFMigrationsHistory"]
            });
        }
    }

    private static async Task<string> StartDbContainerAsync(CancellationToken ct)
    {
        _dbContainer = new MsSqlBuilder().Build();
        await _dbContainer.StartAsync(ct);
        var dbName = TestConfigSection.GetValue("DBName", "TestDB");
        return _dbContainer.GetConnectionString().Replace("master", dbName);
    }

    /// <summary>
    /// Pattern: Reset DB state between tests.
    /// Respawn for real SQL, seed factories for all modes.
    /// </summary>
    protected static async Task ResetDatabaseAsync(
        bool respawn = false,
        List<string>? seedPaths = null,
        List<Action>? seedFactories = null,
        CancellationToken cancellationToken = default)
    {
        if (!DbContext.Database.IsInMemory() && respawn && _respawner is not null)
            await _respawner.ResetAsync(_dbConnection!);

        if (seedFactories is { Count: > 0 })
            foreach (var factory in seedFactories) factory();

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Pattern: Cleanup container and connections.</summary>
    protected static async Task BaseClassCleanup()
    {
        if (_dbConnection is not null)
            await _dbConnection.DisposeAsync();
        if (_dbContainer is not null)
            await _dbContainer.DisposeAsync();
    }
}
