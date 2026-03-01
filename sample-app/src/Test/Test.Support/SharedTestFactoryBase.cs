using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Test.Support;

/// <summary>
/// Shared base class for WebApplicationFactory-based test infrastructure.
/// Manages TestContainer lifecycle, database schema initialization, environment
/// variable configuration, and HttpClient creation.
///
/// Each test project creates a thin static <c>SharedTestFactory</c> that wraps a
/// singleton instance, parameterized with the project's specific factory type,
/// config file name, and database name.
///
/// <para><b>Supported DB modes</b> (via <c>TestSettings:DBSource</c> in config):</para>
/// <list type="bullet">
///   <item><c>"TestContainer"</c> — Testcontainers.MsSql spins up SQL Server in Docker/WSL2</item>
///   <item><c>"UseInMemoryDatabase"</c> — EF InMemory provider, no Docker required</item>
///   <item><c>&lt;connection string&gt;</c> — Uses an existing SQL Server instance</item>
/// </list>
///
/// <para><b>Usage (in each test project):</b></para>
/// <code>
/// internal static class SharedTestFactory
/// {
///     public static readonly Guid TestTenantId = SharedTestFactoryBase&lt;Program, MyFactory&gt;.TestTenantId;
///     private static readonly SharedTestFactoryBase&lt;Program, MyFactory&gt; _base = new(
///         "appsettings-test.json", "Test.MyProject.TestDB", cs => new MyFactory(cs));
///
///     public static Task InitializeAsync(CancellationToken ct = default) => _base.InitializeAsync(ct);
///     public static MyFactory GetFactory() => _base.GetFactory();
///     public static HttpClient CreateClient() => _base.CreateClient();
///     public static bool IsRealDatabase => _base.IsRealDatabase;
/// }
/// </code>
/// </summary>
/// <typeparam name="TEntryPoint">The API entry-point class (typically <c>Program</c>).</typeparam>
/// <typeparam name="TFactory">The <see cref="WebApplicationFactory{TEntryPoint}"/> subclass for this test project.</typeparam>
public class SharedTestFactoryBase<TEntryPoint, TFactory>
    where TEntryPoint : class
    where TFactory : WebApplicationFactory<TEntryPoint>
{
    /// <summary>
    /// Fixed tenant ID for all tests. Tests MUST use this when creating entities
    /// so the global tenant query filter includes them in query results.
    /// </summary>
    public static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private TFactory? _factory;
    private readonly Lock _lock = new();
    private bool _envVarsSet;
    private string _dbConnectionString = null!;

    private readonly string _configFileName;
    private readonly string _defaultDbName;
    private readonly Func<string, TFactory> _factoryCreator;
    private readonly IConfigurationRoot _config;

    /// <summary>
    /// Creates a new <see cref="SharedTestFactoryBase{TEntryPoint, TFactory}"/> with
    /// project-specific parameters.
    /// </summary>
    /// <param name="configFileName">
    /// The appsettings JSON file name relative to the test project output directory
    /// (e.g., <c>"appsettings-test.json"</c>).
    /// </param>
    /// <param name="defaultDbName">
    /// Default database name used when <c>TestSettings:DBName</c> is not in config
    /// (e.g., <c>"Test.Endpoints.TestDB"</c>).
    /// </param>
    /// <param name="factoryCreator">
    /// Factory function that creates a <typeparamref name="TFactory"/> given a DB connection
    /// string (or <c>"UseInMemoryDatabase"</c> for InMemory mode).
    /// </param>
    public SharedTestFactoryBase(string configFileName, string defaultDbName, Func<string, TFactory> factoryCreator)
    {
        _configFileName = configFileName;
        _defaultDbName = defaultDbName;
        _factoryCreator = factoryCreator;
        _config = Utility.BuildConfiguration(configFileName).Build();
    }

    /// <summary>
    /// One-time async initialization — reads <c>TestSettings:DBSource</c> from config,
    /// starts a TestContainer if configured, and creates the database schema for
    /// non-InMemory modes using <c>EnsureDeletedAsync</c> + <c>EnsureCreatedAsync</c>.
    /// Must be called from <c>[AssemblyInitialize]</c> before any tests run.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _dbConnectionString = _config.GetSection("TestSettings")
            .GetValue("DBSource", "UseInMemoryDatabase")!;

        if (_dbConnectionString == "TestContainer")
        {
            string dbName = _config.GetValue("TestSettings:DBName", _defaultDbName)!;
            var (_, connStr) = await DbTestLifecycle.StartDbContainerAsync(
                dbName, cancellationToken: cancellationToken);
            _dbConnectionString = connStr;
        }

        // Create database schema for non-InMemory modes.
        // EnsureDeletedAsync → EnsureCreatedAsync forces a clean schema from the EF model.
        if (_dbConnectionString is not "UseInMemoryDatabase")
        {
            var options = new DbContextOptionsBuilder<TaskFlowDbContextTrxn>()
                .UseSqlServer(_dbConnectionString)
                .Options;
            await using var dbContext = new TaskFlowDbContextTrxn(options) { AuditId = "TestSetup" };
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Gets or creates a shared <typeparamref name="TFactory"/> backed by the configured
    /// DB mode. Thread-safe via lock. The factory is created lazily on first access.
    /// </summary>
    public TFactory GetFactory()
    {
        if (_factory != null) return _factory;
        lock (_lock)
        {
            if (_factory != null) return _factory;

            // Default to InMemory if InitializeAsync was not called
            _dbConnectionString ??= "UseInMemoryDatabase";

            EnsureEnvVars();

            _factory = _factoryCreator(_dbConnectionString);
            return _factory;
        }
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> from the shared factory.
    /// Caller is responsible for disposing the client.
    /// </summary>
    public HttpClient CreateClient()
    {
        return GetFactory().CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    /// <summary>
    /// Whether the factory is running against a real database (TestContainer or SQL Server),
    /// as opposed to EF InMemory provider.
    /// </summary>
    public bool IsRealDatabase =>
        _dbConnectionString is not null and not "UseInMemoryDatabase";

    /// <summary>
    /// Sets environment variables for connection strings that the bootstrapper validates
    /// at startup. Called automatically by <see cref="GetFactory"/>.
    /// For InMemory mode, sets dummy connection strings to pass bootstrapper validation.
    /// For TestContainer/SQL mode, sets the real connection string.
    /// </summary>
    private void EnsureEnvVars()
    {
        if (_envVarsSet) return;

        if (_dbConnectionString is "UseInMemoryDatabase")
        {
            // Bootstrapper validates connection strings at startup — provide dummy
            // values since InMemory mode replaces the actual DB providers.
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextTrxn",
                "Server=(localdb)\\test;Database=TaskFlow_Test;Trusted_Connection=True");
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextQuery",
                "Server=(localdb)\\test;Database=TaskFlow_Test;Trusted_Connection=True");
        }
        else
        {
            // TestContainer/SQL mode: set real connection strings for bootstrapper
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextTrxn",
                _dbConnectionString);
            Environment.SetEnvironmentVariable("ConnectionStrings__TaskFlowDbContextQuery",
                _dbConnectionString);
        }

        _envVarsSet = true;
    }
}
