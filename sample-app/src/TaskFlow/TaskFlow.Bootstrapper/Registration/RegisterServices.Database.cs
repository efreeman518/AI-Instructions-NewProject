using Application.Contracts.Repositories;
using EntityFramework.Exceptions.SqlServer;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EF.Common.Contracts;
using EF.Data;
using EF.Data.Interceptors;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddDatabaseServices(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ITodoItemRepositoryQuery, TodoItemRepositoryQuery>();
        services.AddScoped<ITodoItemRepositoryTrxn, TodoItemRepositoryTrxn>();
        services.AddScoped<ICategoryRepositoryQuery, CategoryRepositoryQuery>();
        services.AddScoped<ICategoryRepositoryTrxn, CategoryRepositoryTrxn>();
        services.AddScoped<ITagRepositoryQuery, TagRepositoryQuery>();
        services.AddScoped<ITagRepositoryTrxn, TagRepositoryTrxn>();
        services.AddScoped<ITeamRepositoryQuery, TeamRepositoryQuery>();
        services.AddScoped<ITeamRepositoryTrxn, TeamRepositoryTrxn>();
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();

        services.AddTransient<AuditInterceptor<string, Guid?>>();
        services.AddTransient<ConnectionNoLockInterceptor>();

        ConfigureDatabaseContexts(services, config);
    }

    private static void ConfigureDatabaseContexts(IServiceCollection services, IConfiguration config)
    {
        var dBConnectionStringTrxn = config.GetConnectionString("TaskFlowDbContextTrxn");
        var dBConnectionStringQuery = config.GetConnectionString("TaskFlowDbContextQuery");
        if (string.IsNullOrEmpty(dBConnectionStringTrxn) || string.IsNullOrEmpty(dBConnectionStringQuery))
        {
            throw new ArgumentException("Database connection strings cannot be null or empty.");
        }
        ConfigureSqlDatabase(services, dBConnectionStringTrxn, dBConnectionStringQuery);
    }

    private static void ConfigureSqlDatabase(IServiceCollection services, string dbConnectionStringTrxn, string dbConnectionStringQuery)
    {
        services.AddPooledDbContextFactory<TaskFlowDbContextTrxn>((sp, options) =>
        {
            ConfigureTrxnDbContext(options, dbConnectionStringTrxn);
            var auditInterceptor = sp.GetRequiredService<AuditInterceptor<string, Guid?>>();
            options.UseExceptionProcessor().AddInterceptors(auditInterceptor);
        });
        services.AddScoped<DbContextScopedFactory<TaskFlowDbContextTrxn, string, Guid?>>();
        services.AddScoped(sp => sp.GetRequiredService<DbContextScopedFactory<TaskFlowDbContextTrxn, string, Guid?>>().CreateDbContext());

        services.AddPooledDbContextFactory<TaskFlowDbContextQuery>((sp, options) =>
        {
            ConfigureQueryDbContext(options, dbConnectionStringQuery);
            options.UseExceptionProcessor();
        });
        services.AddScoped<DbContextScopedFactory<TaskFlowDbContextQuery, string, Guid?>>();
        services.AddScoped(sp => sp.GetRequiredService<DbContextScopedFactory<TaskFlowDbContextQuery, string, Guid?>>().CreateDbContext());
    }

    private static void ConfigureSqlOptions(DbContextOptionsBuilder options, string connectionString)
    {
        if (connectionString.Contains("database.windows.net"))
        {
            options.UseAzureSql(connectionString, sqlOptions =>
            {
                sqlOptions.UseCompatibilityLevel(170);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            });
        }
        else
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.UseCompatibilityLevel(160);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            });
        }
    }

    private static void ConfigureTrxnDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        ConfigureSqlOptions(options, connectionString);
    }

    private static void ConfigureQueryDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        var readOnlyConnectionString = connectionString.Contains("ApplicationIntent=")
            ? connectionString
            : connectionString + ";ApplicationIntent=ReadOnly";
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        ConfigureSqlOptions(options, readOnlyConnectionString);
    }
}
