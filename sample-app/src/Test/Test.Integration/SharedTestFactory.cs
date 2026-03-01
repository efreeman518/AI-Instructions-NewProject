using Test.Support;

namespace Test.Integration;

/// <summary>
/// Shared factory singleton for integration tests. Thin wrapper over
/// <see cref="SharedTestFactoryBase{TEntryPoint, TFactory}"/> parameterized
/// with <see cref="CustomApiFactory"/>, the integration-specific config file
/// (<c>appsettings.json</c>), and DB name.
/// </summary>
internal static class SharedTestFactory
{
    /// <summary>
    /// Fixed tenant ID for all integration tests. Tests MUST use this when creating
    /// entities so the global tenant query filter includes them in query results.
    /// </summary>
    public static readonly Guid TestTenantId =
        SharedTestFactoryBase<Program, CustomApiFactory>.TestTenantId;

    /// <summary>
    /// Singleton base instance that manages container lifecycle, schema init, and env vars.
    /// </summary>
    private static readonly SharedTestFactoryBase<Program, CustomApiFactory> _base = new(
        configFileName: "appsettings.json",
        defaultDbName: "Test.Integration.TestDB",
        factoryCreator: connStr => new CustomApiFactory(connStr));

    /// <summary>
    /// One-time async initialization — starts TestContainer if configured.
    /// Must be called from [AssemblyInitialize] before any tests run.
    /// </summary>
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => _base.InitializeAsync(cancellationToken);

    /// <summary>
    /// Gets or creates the shared WebApplicationFactory. Thread-safe.
    /// </summary>
    public static CustomApiFactory GetFactory()
        => _base.GetFactory();

    /// <summary>
    /// Creates a new HttpClient from the shared factory.
    /// Caller is responsible for disposing the client.
    /// </summary>
    public static HttpClient CreateClient()
        => _base.CreateClient();

    /// <summary>
    /// Whether the factory is running against a real database (not InMemory).
    /// </summary>
    public static bool IsRealDatabase => _base.IsRealDatabase;
}
