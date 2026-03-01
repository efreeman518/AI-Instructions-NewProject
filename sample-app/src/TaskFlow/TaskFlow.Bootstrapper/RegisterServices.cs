using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EF.BackgroundServices;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    public static IServiceCollection RegisterDomainServices(this IServiceCollection services, IConfiguration config)
    {
        _ = services.GetHashCode();
        _ = config.GetHashCode();
        return services;
    }

    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        AddMessageHandlers(services);
        AddApplicationServices(services, config);
        return services;
    }

    public static IServiceCollection RegisterInfrastructureServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        AddConfigurationServices(services, config);
        AddInternalServices(services);
        AddCachingServices(services, config);
        AddRequestContextServices(services);
        AddAzureClientServices(services, config);
        AddDatabaseServices(services, config);
        AddNotificationServices(services, config);
        AddEntraExtServices(services, config);
        AddStartupTasks(services);
        return services;
    }

    public static IServiceCollection RegisterBackgroundServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddChannelBackgroundTaskQueue();
        return services;
    }
}
