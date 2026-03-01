using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Bootstrapper.StartupTasks;

public class ApplyEFMigrationsStartup(
    IHostEnvironment environment,
    IDbContextFactory<TaskFlowDbContextTrxn> contextFactory,
    ILogger<ApplyEFMigrationsStartup> logger,
    IConfiguration configuration) : IStartupTask
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        bool runMigrations = environment.IsDevelopment() && configuration.GetValue("RunStartupEFMigrations", false)
            && (Environment.GetEnvironmentVariable("DOTNET_ASPIRE") == "true"
                || (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
                    Environment.GetEnvironmentVariable("ASPNETCORE_URLS")!.Contains("http://localhost:")));

        if (runMigrations)
        {
            logger.LogInformation("Development Aspire environment with migrations enabled - applying EF migrations automatically");
            try
            {
                using var context = contextFactory.CreateDbContext();
                await context.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying database migrations");
            }
        }
        else
        {
            logger.LogInformation("Not running in Aspire development environment - skipping automatic migrations");
        }
    }
}
