using Azure.Identity;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddAzureClientServices(IServiceCollection services, IConfiguration config)
    {
        services.AddAzureClients(builder =>
        {
            builder.ConfigureDefaults(config.GetSection("AzureClientDefaults"));
            builder.UseCredential(new DefaultAzureCredential());
        });
    }
}
