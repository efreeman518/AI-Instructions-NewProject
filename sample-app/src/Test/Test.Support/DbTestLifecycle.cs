using Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Respawn;
using Respawn.Graph;
using System.Data.Common;
using Testcontainers.MsSql;

namespace Test.Support;

/// <summary>
/// Manages database lifecycle for integration tests: TestContainers startup,
/// EnsureCreated/Migrate, Respawn reset, and snapshot create/restore.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/DbTestLifecycle.cs
/// </summary>
public static class DbTestLifecycle
{
    /// <summary>
    /// Ensures the database schema is initialized. Uses EnsureCreated for InMemory,
    /// MigrateAsync for real SQL databases.
    /// </summary>
    public static async Task EnsureInitializedAsync(TaskFlowDbContextBase dbContext, CancellationToken cancellationToken = default)
    {
        // Use EnsureCreatedAsync for all modes — TaskFlow has no EF Migrations,
        // so MigrateAsync would fail. EnsureCreated creates the schema from the
        // model, which is correct for both InMemory and TestContainer scenarios.
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// Start a SQL Server TestContainer and return the container + connection string.
    /// Requires Docker on WSL2.
    /// </summary>
    public static async Task<(MsSqlContainer Container, string ConnectionString)>
        StartDbContainerAsync(string dbName, string password = "YourStr0ngP@ssword!",
            bool createDatabase = true, CancellationToken cancellationToken = default)
    {
        var builder = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest");
        if (!string.IsNullOrWhiteSpace(password))
        {
            builder = builder.WithPassword(password);
        }

        var container = builder.Build();
        await container.StartAsync(cancellationToken);

        string masterConnectionString = container.GetConnectionString();
        if (createDatabase)
        {
            await CreateDatabaseIfNotExistsAsync(masterConnectionString, dbName, cancellationToken);
        }

        string dbConnectionString = masterConnectionString.Replace("master", dbName);
        return (container, dbConnectionString);
    }

    /// <summary>
    /// Opens a connection and creates a Respawner for resetting data between tests.
    /// https://github.com/jbogard/Respawn
    /// </summary>
    public static async Task<(DbConnection Connection, Respawner Respawner)>
        OpenRespawnerAsync(string dbConnectionString, CancellationToken cancellationToken = default)
    {
        var dbConnection = new SqlConnection(dbConnectionString);
        await dbConnection.OpenAsync(cancellationToken);

        var respawner = await Respawner.CreateAsync(dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["taskflow"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });

        return (dbConnection, respawner);
    }

    /// <summary>
    /// Reset the database between tests: respawn data, restore snapshots, run seed factories.
    /// </summary>
    public static async Task ResetDatabaseAsync(TaskFlowDbContextBase dbContext, ILogger logger,
        string dbConnectionString, Respawner? respawner, DbConnection? dbConnection,
        bool respawn = false, string? dbSnapshotName = null,
        List<string>? seedPaths = null, List<Action>? seedFactories = null,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsInMemory())
        {
            // InMemory: delete and recreate for a clean slate
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            if (respawn && respawner != null && dbConnection != null)
            {
                await respawner.ResetAsync(dbConnection);
            }

            if (!string.IsNullOrEmpty(dbSnapshotName))
            {
                if (dbConnection == null)
                {
                    throw new InvalidOperationException("Database snapshot restore requires an open database connection.");
                }

                logger.LogWarning("Snapshot restore not yet implemented for TaskFlow schema.");
            }
        }

        // Run seed factories if provided — note: seed factories must use the
        // DbContext's audited SaveChanges overload themselves, or call dbContext directly.
        if (seedFactories != null && seedFactories.Count > 0)
        {
            foreach (var action in seedFactories)
            {
                action();
            }
        }
    }

    /// <summary>
    /// Create a new database on the SQL Server container if it doesn't already exist.
    /// </summary>
    private static async Task CreateDatabaseIfNotExistsAsync(string masterConnectionString, string dbName,
        CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        using (var command = new SqlCommand($"SELECT DB_ID('{dbName}')", connection))
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result != DBNull.Value)
            {
                return; // DB already exists
            }
        }

        using var createCommand = new SqlCommand($"CREATE DATABASE [{dbName}]", connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
