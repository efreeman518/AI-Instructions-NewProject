// ═══════════════════════════════════════════════════════════════
// Pattern: Startup Task — warmup singleton dependencies.
// Resolves key singletons to trigger their initialization eagerly,
// avoiding cold-start latency on the first request.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using Package.Infrastructure.Common;

namespace TaskFlow.Bootstrapper;

/// <summary>
/// Pattern: Dependency warmup — resolves singletons from DI to trigger initialization.
/// Prevents first-request latency from lazy singleton construction.
/// </summary>
public class WarmupDependencies(
    IServiceProvider provider,
    ILogger<WarmupDependencies> logger) : IStartupTask
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Warming up singleton dependencies...");

        // Pattern: Resolve singletons to trigger their constructors.
        // IInternalMessageBus — initializes handler registry.
        _ = provider.GetService(typeof(IInternalMessageBus));

        logger.LogInformation("Dependency warmup complete.");
        return Task.CompletedTask;
    }
}
