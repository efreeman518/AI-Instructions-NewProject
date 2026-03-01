using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EF.BackgroundServices.InternalMessageBus;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddConfigurationServices(IServiceCollection services, IConfiguration config)
    {
        if (config.GetValue<string>("AzureAppConfig:Endpoint") != null)
        {
            services.AddAzureAppConfiguration();
        }
    }

    private static void AddInternalServices(IServiceCollection services)
    {
        services.AddSingleton<IInternalMessageBus, InternalMessageBus>();
    }
}
