using Infrastructure.EntraExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddEntraExtServices(IServiceCollection services, IConfiguration config)
    {
        services.Configure<EntraExtServiceSettings>(
            config.GetSection(EntraExtServiceSettings.ConfigurationSection));
        services.AddSingleton<IEntraExtService, EntraExtService>();
    }
}
