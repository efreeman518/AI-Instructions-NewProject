using Application.Contracts.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Scheduler;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection RegisterSchedulerServices(this IServiceCollection services, IConfiguration config, ILogger logger)
    {
        logger.LogInformation("Registering scheduler services");

        services.AddHealthChecks()
            .AddDbContextCheck<TaskFlowDbContextTrxn>("TaskFlowDbContextTrxn");

        // TickerQ registration would go here
        // services.AddTickerQ(options => { ... });

        return services;
    }
}
