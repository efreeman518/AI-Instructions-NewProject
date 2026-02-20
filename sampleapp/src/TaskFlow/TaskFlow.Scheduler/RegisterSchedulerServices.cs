// ═══════════════════════════════════════════════════════════════
// Pattern: Scheduler-specific DI + TickerQ configuration.
// Registers job handlers, TickerQ adapter jobs, telemetry, and health.
// Also provides AddTickerQConfig + ConfigureTickerQDatabase extensions.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.Scheduler.Handlers;
using TaskFlow.Scheduler.Infrastructure;
using TaskFlow.Scheduler.Jobs;
using TaskFlow.Scheduler.Telemetry;
using Microsoft.EntityFrameworkCore;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Utilities.Entities;

namespace TaskFlow.Scheduler;

internal static partial class IServiceCollectionExtensions
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: RegisterSchedulerServices — scheduler-only DI.
    // Job handlers (scoped), job adapters (scoped), telemetry (singleton).
    // ═══════════════════════════════════════════════════════════════

    public static IServiceCollection RegisterSchedulerServices(this IServiceCollection services, IConfiguration config)
    {
        // Pattern: Job handlers — scoped because they resolve scoped services.
        services.AddScoped<ProcessDueRemindersHandler>();
        services.AddScoped<DatabaseMaintenanceHandler>();

        // Pattern: TickerQ job adapters — scoped to match handler lifetime.
        services.AddScoped<ReminderJobs>();
        services.AddScoped<MaintenanceJobs>();

        // Pattern: Telemetry — singleton for metrics aggregation.
        services.AddSingleton<SchedulingMetrics>();

        // Pattern: OpenAPI + health.
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddCheck<SchedulerHealthCheck>("scheduler");

        return services;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: AddTickerQConfig — persistence, dashboard, scheduler settings.
    // Uses AddTickerQ<TimeTickerEntity, CronTickerEntity> for both
    // time-based (one-off) and cron-based (recurring) job types.
    // ═══════════════════════════════════════════════════════════════

    public static void AddTickerQConfig(this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var services = builder.Services;

        var connectionString = config.GetConnectionString("SchedulerDbContext");
        var usePersistence = config.GetValue<bool>("Scheduling:UsePersistence", true);
        var enableDashboard = config.GetValue<bool>("Scheduling:EnableDashboard", true);
        var pollIntervalSeconds = config.GetValue<int>("Scheduling:PollIntervalSeconds", 120);

        services.AddTickerQ<TimeTickerEntity, CronTickerEntity>(options =>
        {
            // Pattern: Global exception handler with metrics + human-readable job names.
            options.SetExceptionHandler<TaskFlowSchedulerExceptionHandler>();

            // Pattern: Scheduler configuration — MaxConcurrency = CPU count for optimal throughput.
            options.ConfigureScheduler(scheduler =>
            {
                scheduler.MaxConcurrency = Environment.ProcessorCount;
                scheduler.SchedulerTimeZone = TimeZoneInfo.Utc;
                scheduler.IdleWorkerTimeOut = TimeSpan.FromMinutes(2);
                scheduler.FallbackIntervalChecker = TimeSpan.FromSeconds(pollIntervalSeconds);
                scheduler.NodeIdentifier = Environment.MachineName;
            });

            // Pattern: EF Core persistence — dedicated SchedulerDbContext with [Scheduler] schema.
            // TickerQ manages its own schema — does NOT use EF Core migrations.
            if (usePersistence && !string.IsNullOrEmpty(connectionString))
            {
                options.AddOperationalStore(efOptions =>
                {
                    efOptions.UseTickerQDbContext<TickerQDbContext>(optionsBuilder =>
                    {
                        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                        {
                            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "Scheduler");
                            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                        });
                    }, schema: "Scheduler");
                });
            }

            // Pattern: Dashboard — basic auth, separate basepath.
            // Never use default credentials in production — configure via Key Vault.
            if (enableDashboard)
            {
                options.AddDashboard(dashboardOptions =>
                {
                    dashboardOptions.SetBasePath("/scheduler");
                    var user = config["Scheduling:Dashboard:Username"] ?? "admin";
                    var pass = config["Scheduling:Dashboard:Password"] ?? "change-me-in-production";
                    dashboardOptions.WithBasicAuth(user, pass);
                    dashboardOptions.WithSessionTimeout(60);
                });
            }
        });

        // Pattern: Health check for TickerQ database connectivity.
        if (usePersistence && !string.IsNullOrEmpty(connectionString))
        {
            services.AddHealthChecks()
                .AddDbContextCheck<TickerQDbContext>("scheduler-db");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Verify TickerQ database on startup.
    // TickerQ manages its own schema — this just checks connectivity.
    // ═══════════════════════════════════════════════════════════════

    internal static async Task ConfigureTickerQDatabase(this WebApplication app, IConfiguration config, ILogger logger)
    {
        var connectionString = config.GetConnectionString("SchedulerDbContext");
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogWarning("SchedulerDbContext connection string not configured — skipping DB check");
            return;
        }

        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TickerQDbContext>();
            await dbContext.Database.CanConnectAsync();
            logger.LogInformation("TickerQ database connection verified");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to TickerQ database");
            throw;
        }
    }
}
