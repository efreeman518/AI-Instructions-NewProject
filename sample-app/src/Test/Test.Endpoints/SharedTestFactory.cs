using Test.Support;

namespace Test.Endpoints;

/// <summary>
/// Shared factory singleton for endpoint tests. Thin wrapper over
/// <see cref="SharedTestFactoryBase{TEntryPoint, TFactory}"/> parameterized
/// with <see cref="CustomEndpointApiFactory{TProgram}"/>, the endpoint-specific
/// config file (<c>appsettings-test.json</c>), and DB name.
/// </summary>
internal static class SharedTestFactory
{
    /// <summary>
    /// Fixed tenant ID for all endpoint tests. Tests MUST use this when creating entities
    /// so the global tenant query filter includes them in search results.
    /// </summary>
    public static readonly Guid TestTenantId =
        SharedTestFactoryBase<Program, CustomEndpointApiFactory<Program>>.TestTenantId;

    /// <summary>
    /// Singleton base instance that manages container lifecycle, schema init, and env vars.
    /// </summary>
    private static readonly SharedTestFactoryBase<Program, CustomEndpointApiFactory<Program>> _base = new(
        configFileName: "appsettings-test.json",
        defaultDbName: "Test.Endpoints.TestDB",
        factoryCreator: connStr => new CustomEndpointApiFactory<Program>(connStr));

    /// <summary>
    /// One-time async initialization — starts TestContainer if configured.
    /// Must be called from [AssemblyInitialize] before any tests run.
    /// </summary>
    public static Task InitializeAsync(CancellationToken cancellationToken = default)
        => _base.InitializeAsync(cancellationToken);

    /// <summary>
    /// Gets or creates the shared WebApplicationFactory. Thread-safe.
    /// </summary>
    public static CustomEndpointApiFactory<Program> GetFactory()
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
