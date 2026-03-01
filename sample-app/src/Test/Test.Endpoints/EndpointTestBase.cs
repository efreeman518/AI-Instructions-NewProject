using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Respawn;
using System.Data.Common;
using Test.Support;
using Testcontainers.MsSql;

namespace Test.Endpoints;

/// <summary>
/// Base class for endpoint tests — manages WebApplicationFactory lifecycle, TestContainers,
/// and database reset between tests.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Endpoints/EndpointTestBase.cs
/// </summary>
public abstract class EndpointTestBase
{
    private static string _testContextName = null!;
    private static MsSqlContainer _dbContainer = null!;
    private static string _dbConnectionString = null!;
    private static DbConnection _dbConnection = null!;
    private static Respawner _respawner = null!;
    private static TaskFlowDbContextBase _dbContext = null!;

    protected static TaskFlowDbContextBase DbContext => _dbContext;
    protected static readonly IConfigurationRoot Config =
        Utility.BuildConfiguration("appsettings-test.json").AddUserSecrets<EndpointTestBase>().Build();
    protected static readonly IConfigurationSection TestConfigSection =
        Config.GetSection("TestSettings");

    protected static CustomEndpointApiFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    /// <summary>
    /// Configure the test class — sets up TestContainers or InMemory DB, creates factory.
    /// </summary>
    public static async Task ConfigureTestInstanceAsync(string testContextName,
        CancellationToken cancellationToken = default)
    {
        _testContextName = $"EndpointTest-{testContextName}";

        _dbConnectionString = TestConfigSection.GetValue("DBSource", "UseInMemoryDatabase")!;

        if (_dbConnectionString == "TestContainer")
        {
            await StartDbContainerAsync(cancellationToken);
        }

        // Set env vars for bootstrapper connection string validation
        if (_dbConnectionString != "UseInMemoryDatabase")
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextTrxn", _dbConnectionString);
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextQuery", _dbConnectionString);
        }
        else
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextTrxn",
                "Server=(localdb)\\test;Database=TaskFlow_Test;Trusted_Connection=True");
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextQuery",
                "Server=(localdb)\\test;Database=TaskFlow_Test;Trusted_Connection=True");
        }

        _dbContext = NewTodoDbContextTrxn(_dbConnectionString);

        await DbTestLifecycle.EnsureInitializedAsync(_dbContext, cancellationToken);

        if (!_dbContext.Database.IsInMemory())
        {
            (_dbConnection, _respawner) = await DbTestLifecycle.OpenRespawnerAsync(
                _dbConnectionString, cancellationToken);
        }

        // Create the WebApplicationFactory with the DB connection string
        Factory = new CustomEndpointApiFactory<Program>(_dbConnectionString);
    }

    /// <summary>
    /// Start a SQL Server TestContainer (requires Docker on WSL2).
    /// </summary>
    protected static async Task StartDbContainerAsync(CancellationToken cancellationToken = default)
    {
        string dbName = Config.GetValue("TestSettings:DBName", "TestDB")!;
        (_dbContainer, _dbConnectionString) = await DbTestLifecycle.StartDbContainerAsync(
            dbName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a fresh HttpClient from the factory.
    /// </summary>
    protected void InitializeClient()
    {
        Client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    /// <summary>
    /// Reset database between tests using Respawn (for real DBs).
    /// </summary>
    protected static async Task ResetDatabaseAsync(bool respawn = false,
        List<string>? seedPaths = null, List<Action>? seedFactories = null,
        CancellationToken cancellationToken = default)
    {
        await DbTestLifecycle.ResetDatabaseAsync(
            DbContext,
            NullLogger.Instance,
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
    /// Cleanup — dispose factory, DB connection, and container.
    /// </summary>
    public static async Task BaseClassCleanup()
    {
        Factory?.Dispose();

        // Clean up env vars
        Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextTrxn", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextQuery", null);

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

    /// <summary>
    /// Dispose the HttpClient.
    /// </summary>
    protected void CleanupClient()
    {
        Client?.Dispose();
    }

    private static TaskFlowDbContextTrxn NewTodoDbContextTrxn(string dbSource, string? dbName = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TaskFlowDbContextTrxn>();
        if (dbSource == "UseInMemoryDatabase")
        {
            optionsBuilder.UseInMemoryDatabase(dbName ?? "InMemoryDatabase");
        }
        else
        {
            optionsBuilder.UseSqlServer(dbSource);
        }
        return new TaskFlowDbContextTrxn(optionsBuilder.Options) { AuditId = "EndpointTests" };
    }
}
