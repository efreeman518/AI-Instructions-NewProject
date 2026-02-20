// ═══════════════════════════════════════════════════════════════
// Pattern: EndpointTestBase — combines DbIntegrationTestBase with CustomApiFactory.
// Provides a lazy HttpClient factory for endpoint tests.
// All endpoint test classes inherit from this base.
//
// Flow: DbIntegrationTestBase sets up DB lifecycle (InMemory/TestContainer/SQL)
//       → EndpointTestBase adds HTTP client via WebApplicationFactory
//       → Endpoint tests use the client to call API endpoints
// ═══════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Mvc.Testing;
using Test.Support;

namespace Test.Integration;

/// <summary>
/// Pattern: Endpoint test base — lazy factory initialization.
/// The factory is created once per test class (via [ClassInitialize])
/// and reused across all test methods in that class.
/// Uses CustomApiFactory which swaps DB and removes background services.
/// </summary>
public abstract class EndpointTestBase : DbIntegrationTestBase
{
    private static CustomApiFactory<Program>? _factory;

    /// <summary>
    /// Pattern: Lazy HttpClient — factory is created on first call and reused.
    /// Supports optional DelegatingHandler chain for auth, logging, etc.
    /// Default: no auth handler (anonymous requests) — suitable for tests
    /// that bypass auth or when auth is disabled in test config.
    /// </summary>
    /// <param name="handlers">
    /// Optional DelegatingHandlers (e.g., AuthMessageHandler for injecting test tokens).
    /// </param>
    protected static async Task<HttpClient> GetHttpClient(
        params DelegatingHandler[] handlers)
    {
        // Pattern: Lazy factory creation — ConfigureTestInstanceAsync must be called first.
        _factory ??= new CustomApiFactory<Program>(
            TestConfigSection.GetValue("DBSource", "UseInMemoryDatabase"));

        return handlers.Length > 0
            ? _factory.CreateDefaultClient(new Uri("https://localhost"), handlers)
            : _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    /// <summary>
    /// Pattern: Factory cleanup — disposes the factory and underlying test server.
    /// Call from [ClassCleanup] in derived test classes.
    /// </summary>
    protected static async Task FactoryCleanup()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
        await BaseClassCleanup();
    }
}
