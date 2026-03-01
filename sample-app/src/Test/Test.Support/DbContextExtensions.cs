using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Test.Support;

/// <summary>
/// Extension methods for seeding and managing test database data.
/// Pattern from https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.Support/DbContextExtensions.cs
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Seed the database with data from seed factories and/or SQL files.
    /// InMemoryDatabase has limited functionality and does not support SQL execution.
    /// Caller must SaveChangesAsync to persist the seed data.
    /// </summary>
    public static async Task SeedDatabaseAsync(this DbContext dbContext, ILogger logger,
        List<string>? seedPaths = null, List<Action>? seedFactories = null,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsInMemory()) seedPaths = null;

        seedFactories ??= [];
        seedPaths ??= [];

        await dbContext.SeedAsync(logger, [.. seedPaths], "*.sql", [.. seedFactories], cancellationToken);
    }

    /// <summary>
    /// Seed the database with data from seed files and/or factories.
    /// </summary>
    public static async Task SeedAsync(this DbContext dbContext, ILogger logger,
        string[]? seedPaths = null, string searchPattern = "*.sql",
        Action[]? seedFactories = null, CancellationToken cancellationToken = default)
    {
        // Run seed scripts first since they could affect db structure
        if (!dbContext.Database.IsInMemory() && seedPaths?.Length > 0)
        {
            await dbContext.SeedRawSqlFilesAsync(logger, [.. seedPaths], searchPattern, cancellationToken);
        }

        if (seedFactories != null)
        {
            foreach (var action in seedFactories)
            {
                action();
            }
        }
    }

    /// <summary>
    /// Executes SQL seed files in the specified paths, in alphabetical order.
    /// </summary>
    public static async Task SeedRawSqlFilesAsync(this DbContext db, ILogger logger,
        List<string> relativePaths, string searchPattern, CancellationToken cancellationToken = default)
    {
        foreach (var path in relativePaths)
        {
            string[] files = [..
                Directory.GetFiles(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path), searchPattern)
                .OrderBy(f => f)];

            foreach (var filePath in files)
            {
                await db.SeedRawSqlFileAsync(logger, filePath, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Execute a single SQL seed file against the database.
    /// </summary>
    public static async Task SeedRawSqlFileAsync(this DbContext db, ILogger logger,
        string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Seeding test database from file: {FilePath}", filePath);
            var sql = await File.ReadAllTextAsync(filePath, cancellationToken);
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred seeding the database from file {FilePath}", filePath);
        }
    }
}
