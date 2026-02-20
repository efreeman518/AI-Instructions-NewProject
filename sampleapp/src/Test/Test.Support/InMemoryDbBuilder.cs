// ═══════════════════════════════════════════════════════════════
// Pattern: InMemoryDbBuilder — fluent builder for test databases.
// Supports InMemory (fast, limited SQL) and SQLite (full SQL support).
// SeedDefaultEntityData populates reference data for tests.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Test.Support;

/// <summary>
/// Pattern: Fluent builder for constructing test databases.
/// Use BuildInMemory for fast tests; BuildSQLite when SQL-specific behavior matters
/// (e.g., Like, Contains, computed columns).
/// </summary>
public class InMemoryDbBuilder
{
    private bool _seedDefaultData;
    private readonly List<Action<DbContext>> _seedActions = [];

    /// <summary>Seed default reference data (categories, tags, teams).</summary>
    public InMemoryDbBuilder SeedDefaultEntityData()
    {
        _seedDefaultData = true;
        return this;
    }

    /// <summary>Add custom seed data via lambda.</summary>
    public InMemoryDbBuilder UseEntityData(Action<DbContext> seedAction)
    {
        _seedActions.Add(seedAction);
        return this;
    }

    /// <summary>
    /// Pattern: InMemory provider — fast, no SQL overhead, limited SQL support.
    /// Each call with a different dbName creates an isolated database.
    /// </summary>
    public T BuildInMemory<T>(string? dbName = null) where T : DbContext
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase(dbName).Options;
        var dbContext = (Activator.CreateInstance(typeof(T), options) as T)!;

        if (_seedDefaultData)
            SeedDefaults(dbContext);

        foreach (var action in _seedActions)
            action(dbContext);

        dbContext.SaveChanges();
        return dbContext;
    }

    /// <summary>
    /// Pattern: SQLite in-memory — full SQL support (Like, Contains, etc.)
    /// but slightly slower than InMemory provider.
    /// Connection stays open for the lifetime of the test.
    /// </summary>
    public T BuildSQLite<T>() where T : DbContext
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<T>().UseSqlite(connection).Options;
        var dbContext = (Activator.CreateInstance(typeof(T), options) as T)!;
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        if (_seedDefaultData)
            SeedDefaults(dbContext);

        foreach (var action in _seedActions)
            action(dbContext);

        dbContext.SaveChanges();
        return dbContext;
    }

    /// <summary>
    /// Pattern: Default seed data — categories, tags, teams for test scenarios.
    /// Customize per project domain.
    /// </summary>
    private void SeedDefaults(DbContext dbContext)
    {
        // Pattern: Seed default reference entities for testing.
        // Uncomment and populate once entity types are finalized:
        //
        // var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        //
        // dbContext.Set<Category>().AddRange(
        //     Category.Create(tenantId, "Work").Value!,
        //     Category.Create(tenantId, "Personal").Value!
        // );
        //
        // dbContext.Set<Tag>().AddRange(
        //     Tag.Create("Urgent").Value!,
        //     Tag.Create("Low Priority").Value!
        // );
    }
}
