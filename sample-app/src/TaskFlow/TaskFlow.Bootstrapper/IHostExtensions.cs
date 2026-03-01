using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EF.BackgroundServices.InternalMessageBus;

namespace TaskFlow.Bootstrapper;

public static class IHostExtensions
{
    public static async Task RunStartupTasks(this IHost host)
    {
        var msgBus = host.Services.GetRequiredService<IInternalMessageBus>();
        msgBus.AutoRegisterHandlers();
        using var scope = host.Services.CreateScope();
        var startupTasks = scope.ServiceProvider.GetServices<IStartupTask>();
        foreach (var startupTask in startupTasks)
        {
            await startupTask.ExecuteAsync();
        }
    }
}
