using Domain.Model;
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Test.Support;

/// <summary>
/// Builds in-memory or SQLite TaskFlowDbContext instances for unit/integration testing.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/InMemoryDbBuilder.cs
/// </summary>
public class InMemoryDbBuilder
{
    private bool _seedDefaultEntityData = false;
    private Guid _tenantId = Guid.NewGuid();
    private List<TodoItem>? _entityData;

    /// <summary>
    /// Creates a new TaskFlowDbContextTrxn backed by EF Core InMemory provider (static method).
    /// Each call uses a unique database name for test isolation.
    /// </summary>
    public static TaskFlowDbContextTrxn CreateTrxnContext(string? databaseName = null)
    {
        databaseName ??= $"TaskFlow_Test_{Guid.NewGuid():N}";

        var options = new DbContextOptionsBuilder<TaskFlowDbContextTrxn>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var ctx = new TaskFlowDbContextTrxn(options) { AuditId = "test-user" };
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>
    /// Creates a new TaskFlowDbContextQuery backed by EF Core InMemory provider (static method).
    /// </summary>
    public static TaskFlowDbContextQuery CreateQueryContext(string? databaseName = null)
    {
        databaseName ??= $"TaskFlow_Test_{Guid.NewGuid():N}";

        var options = new DbContextOptionsBuilder<TaskFlowDbContextQuery>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var ctx = new TaskFlowDbContextQuery(options) { AuditId = "test-user" };
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>
    /// Build an InMemory-backed DbContext of type T (fluent API).
    /// </summary>
    public T BuildInMemory<T>(string? dbName = null) where T : DbContext
    {
        dbName ??= Guid.NewGuid().ToString();
        DbContextOptions<T> options = new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase(dbName).Options;
        T dbContext = (Activator.CreateInstance(typeof(T), options) as T)!;

        if (dbContext is TaskFlowDbContextBase db)
        {
            db.AuditId = "test-user";
            SetupContext(db);
        }

        dbContext.SaveChanges();
        return dbContext;
    }

    /// <summary>
    /// Build a SQLite in-memory DbContext of type T (fluent API).
    /// </summary>
    public T BuildSQLite<T>() where T : DbContext
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<T>().UseSqlite(connection).Options;
        T dbContext = (Activator.CreateInstance(typeof(T), options) as T)!;

        if (dbContext != null)
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }

        if (dbContext is TaskFlowDbContextBase db)
        {
            db.AuditId = "test-user";
            SetupContext(db);
        }

        dbContext!.SaveChanges();
        return dbContext;
    }

    private void SetupContext(TaskFlowDbContextBase db)
    {
        if (_seedDefaultEntityData)
        {
            db.TodoItems.RemoveRange(db.TodoItems);
            db.TodoItems.AddRange(TaskFlowDbContextSupport.TodoItemListFactory(_tenantId));
        }

        if (_entityData != null)
        {
            db.TodoItems.AddRange(_entityData);
        }
    }

    /// <summary>
    /// Fluent: seed default entity data on build.
    /// </summary>
    public InMemoryDbBuilder SeedDefaultEntityData(Guid? tenantId = null)
    {
        _seedDefaultEntityData = true;
        _tenantId = tenantId ?? Guid.NewGuid();
        return this;
    }

    /// <summary>
    /// Fluent: provide custom entity data on build.
    /// </summary>
    public InMemoryDbBuilder UseEntityData(Action<List<TodoItem>> action)
    {
        _entityData = [];
        action(_entityData);
        return this;
    }
}
