using TaskFlow.Bootstrapper.StartupTasks;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddStartupTasks(IServiceCollection services)
    {
        services.AddTransient<IStartupTask, ApplyEFMigrationsStartup>();
        services.AddTransient<IStartupTask, WarmupDependencies>();
    }
}
