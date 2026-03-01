using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Bootstrapper.StartupTasks;

public class WarmupDependencies(
    IDbContextFactory<TaskFlowDbContextTrxn> contextFactoryTrxn,
    IDbContextFactory<TaskFlowDbContextQuery> contextFactoryQuery,
    ILogger<WarmupDependencies> logger) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await WarmupDbAsync(contextFactoryTrxn, "TaskFlowDbContextTrxn", cancellationToken);
        await WarmupDbAsync(contextFactoryQuery, "TaskFlowDbContextQuery", cancellationToken);
    }

    private async Task WarmupDbAsync<T>(IDbContextFactory<T> factory, string name, CancellationToken ct) where T : DbContext
    {
        logger.LogInformation("Warmup {DbContext} connection starting.", name);
        try
        {
            using var dbContext = factory.CreateDbContext();
            var canConnect = await dbContext.Database.CanConnectAsync(ct);
            if (canConnect)
                logger.LogInformation("Successfully connected to {DbContext} and warmed up the connection.", name);
            else
                logger.LogWarning("Could not connect to {DbContext} during startup warmup.", name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during {DbContext} database warmup.", name);
        }
    }
}
