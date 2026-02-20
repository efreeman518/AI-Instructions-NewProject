// ═══════════════════════════════════════════════════════════════
// Pattern: IStartupTask — interface for tasks that run once after Build().
// Implementations: EF migrations, cache warmup, dependency warmup.
// Executed sequentially by IHostExtensions.RunStartupTasks().
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Startup task interface — executed once during application startup.
/// Tasks run sequentially after host Build() and before RunAsync().
/// Use for: migrations, cache warmup, singleton resolution, health pre-checks.
/// </summary>
public interface IStartupTask
{
    /// <summary>Execute the startup task.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
