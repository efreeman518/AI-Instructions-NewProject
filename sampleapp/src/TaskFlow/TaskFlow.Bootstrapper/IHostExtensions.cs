// ═══════════════════════════════════════════════════════════════
// Pattern: Host extension — runs post-Build() initialization.
// 1. AutoRegisterHandlers() on IInternalMessageBus (discovers IMessageHandler<T> types).
// 2. Run all IStartupTask implementations sequentially.
// Called from Program.cs: await app.RunStartupTasks();
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EF.BackgroundServices.InternalMessageBus;

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Extension method on IHost for running startup tasks.
/// Separates startup logic from Program.cs for cleaner composition.
/// </summary>
public static class IHostExtensions
{
    /// <summary>
    /// Pattern: Post-Build() initialization sequence.
    /// 1. Auto-register message handlers (scans DI for IMessageHandler&lt;T&gt; implementations).
    /// 2. Execute startup tasks (migrations, cache warmup, etc.) sequentially.
    /// </summary>
    public static async Task RunStartupTasks(this IHost host)
    {
        // Pattern: Auto-register message handlers — discovers all IMessageHandler<T>
        // implementations registered in DI and wires them to the internal message bus.
        var msgBus = host.Services.GetRequiredService<IInternalMessageBus>();
        msgBus.AutoRegisterHandlers();

        // Pattern: Run startup tasks sequentially in a scoped context.
        using var scope = host.Services.CreateScope();
        var startupTasks = scope.ServiceProvider.GetServices<IStartupTask>();

        foreach (var task in startupTasks)
        {
            await task.ExecuteAsync();
        }
    }
}
