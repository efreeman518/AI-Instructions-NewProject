// ═══════════════════════════════════════════════════════════════
// Pattern: Startup Task — apply pending EF Core migrations on startup.
// Uses IDbContextFactory to create a transient DbContext for migration.
// Only runs pending migrations; safe to call on already-migrated databases.
// ═══════════════════════════════════════════════════════════════

using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Startup task that applies EF migrations before the app starts accepting requests.
/// Creates a short-lived DbContext from the factory, applies migrations, then disposes.
/// </summary>
public class ApplyEFMigrationsStartup(
    IDbContextFactory<TaskFlowDbContextTrxn> factory,
    ILogger<ApplyEFMigrationsStartup> logger) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying EF Core migrations...");

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pendingList = pending.ToList();

        if (pendingList.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pendingList.Count, string.Join(", ", pendingList));
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations.");
        }
    }
}
