using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Notification;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationServiceSettings>(
            configuration.GetSection(NotificationServiceSettings.ConfigurationSection));

        services.AddSingleton<INotificationService, NotificationService>();

        return services;
    }
}
