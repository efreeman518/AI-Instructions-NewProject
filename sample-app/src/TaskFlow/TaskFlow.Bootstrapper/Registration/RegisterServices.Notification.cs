using Infrastructure.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddNotificationServices(IServiceCollection services, IConfiguration config)
    {
        services.AddNotificationServices(config);
    }
}
