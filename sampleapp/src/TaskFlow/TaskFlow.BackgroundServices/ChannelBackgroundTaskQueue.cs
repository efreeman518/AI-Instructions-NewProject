// ═══════════════════════════════════════════════════════════════
// Pattern: Channel-based background task queue.
// Uses System.Threading.Channels for bounded, thread-safe queueing.
// Producers (services, endpoints) enqueue work items; consumer
// (QueuedBackgroundService) dequeues and executes them.
// ═══════════════════════════════════════════════════════════════

using System.Threading.Channels;

namespace TaskFlow.BackgroundServices;

/// <summary>
/// Pattern: IBackgroundTaskQueue — producer/consumer abstraction.
/// Bounded channel prevents unbounded memory growth under load.
/// Work items receive IServiceProvider for scoped service resolution.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>Enqueue a work item. Throws if channel is full (bounded).</summary>
    ValueTask QueueBackgroundWorkItemAsync(
        Func<IServiceProvider, CancellationToken, Task> workItem);

    /// <summary>Dequeue next work item. Blocks until available or cancelled.</summary>
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Pattern: Channel-based queue implementation.
/// BoundedChannel with capacity 100 — if full, producers wait (BoundedChannelFullMode.Wait).
/// Thread-safe by design — multiple producers + single consumer is the expected pattern.
/// </summary>
public class ChannelBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    public ChannelBackgroundTaskQueue(int capacity = 100)
    {
        // Pattern: BoundedChannel prevents unbounded memory growth.
        // Wait mode: producers block when channel is full (backpressure).
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,   // Only QueuedBackgroundService reads.
            SingleWriter = false   // Multiple services may enqueue.
        };
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
    }

    public async ValueTask QueueBackgroundWorkItemAsync(
        Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
