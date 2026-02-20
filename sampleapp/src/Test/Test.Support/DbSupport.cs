// ═══════════════════════════════════════════════════════════════
// Pattern: DbSupport — generic DbContext swap utility for testing.
// Replaces production-registered DbContexts (pooled factory) with
// InMemory or direct SQL Server contexts for test isolation.
// Used by DbIntegrationTestBase and CustomApiFactory.
// ═══════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test.Support;

/// <summary>
/// Pattern: Generic DbContext swap — removes production registrations and replaces
/// with InMemory or SQL Server contexts for test use.
/// Supports two concrete contexts (Trxn + Query) for read/write split architectures.
/// </summary>
public static class DbSupport
{
    /// <summary>
    /// Swaps both TTrxn (write) and TQuery (read) DbContexts for testing.
    /// When <paramref name="dbConnectionString"/> is "UseInMemoryDatabase", uses the
    /// EF InMemory provider (fast, no SQL Server required).
    /// Otherwise, uses SQL Server with the given connection string (for TestContainers
    /// or existing SQL instances).
    /// </summary>
    /// <typeparam name="TTrxn">The transactional (write) DbContext type.</typeparam>
    /// <typeparam name="TQuery">The query (read-only) DbContext type.</typeparam>
    /// <param name="services">The service collection to modify.</param>
    /// <param name="dbConnectionString">
    /// "UseInMemoryDatabase" for InMemory, or a SQL Server connection string.
    /// Null/empty = no-op (keeps production registrations).
    /// </param>
    /// <param name="dbName">
    /// Database name for InMemory provider (each unique name = isolated DB).
    /// Ignored for SQL Server connections.
    /// </param>
    public static void ConfigureServicesTestDB<TTrxn, TQuery>(
        IServiceCollection services, string? dbConnectionString, string dbName = "TestDB")
        where TTrxn : DbContext
        where TQuery : DbContext
    {
        if (string.IsNullOrEmpty(dbConnectionString)) return;

        // Pattern: Remove ALL existing registrations for both contexts.
        // This includes DbContextOptions, the context itself, and any pooled factory.
        services.RemoveAll<DbContextOptions<TTrxn>>();
        services.RemoveAll<TTrxn>();
        services.RemoveAll<DbContextOptions<TQuery>>();
        services.RemoveAll<TQuery>();

        // Pattern: Also remove pooled factory registrations from Bootstrapper.
        services.RemoveAll<IDbContextFactory<TTrxn>>();
        services.RemoveAll<IDbContextFactory<TQuery>>();

        if (dbConnectionString == "UseInMemoryDatabase")
        {
            // Pattern: InMemory provider — fast, no SQL Server needed.
            // Singleton lifetime ensures both Trxn and Query share the same in-memory DB.
            services.AddDbContext<TTrxn>(opt => opt.UseInMemoryDatabase(dbName),
                ServiceLifetime.Singleton, ServiceLifetime.Singleton);
            services.AddDbContext<TQuery>(opt => opt.UseInMemoryDatabase(dbName),
                ServiceLifetime.Singleton, ServiceLifetime.Singleton);
        }
        else
        {
            // Pattern: Real SQL Server — for TestContainers or existing instance.
            // Still Singleton so DbIntegrationTestBase can manage the lifecycle.
            services.AddDbContext<TTrxn>(opt =>
                opt.UseSqlServer(dbConnectionString, sql =>
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)),
                ServiceLifetime.Singleton, ServiceLifetime.Singleton);

            services.AddDbContext<TQuery>(opt =>
            {
                // Pattern: Query context uses ApplicationIntent=ReadOnly for read replicas.
                opt.UseSqlServer(dbConnectionString + ";ApplicationIntent=ReadOnly", sql =>
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null));
                opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);
        }
    }
}
