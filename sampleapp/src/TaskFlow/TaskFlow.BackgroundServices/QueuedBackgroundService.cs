// ═══════════════════════════════════════════════════════════════
// Pattern: Queued background service — consumer for ChannelBackgroundTaskQueue.
// Long-running BackgroundService that dequeues and executes work items.
// Creates a scope per work item for proper DI lifetime management.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.BackgroundServices;

/// <summary>
/// Pattern: BackgroundService consumer — dequeues work items from IBackgroundTaskQueue.
/// Runs for the lifetime of the host. Each work item gets its own IServiceScope.
/// Errors are logged but do not crash the host — the loop continues.
///
/// Usage: Inject IBackgroundTaskQueue into any service/endpoint and enqueue a lambda:
/// <code>
/// await queue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
/// {
///     var service = sp.GetRequiredService&lt;IMyService&gt;();
///     await service.DoWorkAsync(ct);
/// });
/// </code>
/// </summary>
public class QueuedBackgroundService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<QueuedBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("QueuedBackgroundService is starting");

        // Pattern: Continuous dequeue loop — runs until host shutdown.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Pattern: DequeueAsync blocks until a work item is available.
                var workItem = await taskQueue.DequeueAsync(stoppingToken);

                // Pattern: Scoped execution — each work item gets its own DI scope.
                // This ensures DbContext and other scoped services are properly disposed.
                using var scope = serviceScopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Pattern: Graceful shutdown — host is stopping, exit the loop.
                break;
            }
            catch (Exception ex)
            {
                // Pattern: Error isolation — log and continue.
                // One failed work item must not crash the entire background service.
                logger.LogError(ex, "Error occurred executing queued background work item");
            }
        }

        logger.LogInformation("QueuedBackgroundService is stopping");
    }
}
