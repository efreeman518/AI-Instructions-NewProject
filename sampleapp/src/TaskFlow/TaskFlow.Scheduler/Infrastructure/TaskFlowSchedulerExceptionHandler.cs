// ═══════════════════════════════════════════════════════════════
// Pattern: Global TickerQ exception handler.
// Maps TickerQ Guid job IDs to human-readable names for logging.
// Uses static ConcurrentDictionary — registered by BaseTickerQJob,
// consumed here when TickerQ reports a failure.
// ═══════════════════════════════════════════════════════════════

using System.Collections.Concurrent;
using TickerQ.Utilities.Base;

namespace TaskFlow.Scheduler.Infrastructure;

/// <summary>
/// Pattern: ITickerExceptionHandler — global catch-all for TickerQ failures.
/// Rule: Static ConcurrentDictionary maps Guid → job name because TickerQ
/// only exposes Guid identifiers in the exception context.
/// BaseTickerQJob.RegisterJobName() populates the mapping before each run.
/// </summary>
public class TaskFlowSchedulerExceptionHandler(
    ILogger<TaskFlowSchedulerExceptionHandler> logger) : ITickerExceptionHandler
{
    // Pattern: Static mapping — shared across all job executions within the process.
    private static readonly ConcurrentDictionary<Guid, string> _jobNames = new();

    /// <summary>Called by BaseTickerQJob before handler execution.</summary>
    public static void RegisterJobName(Guid jobId, string jobName) => _jobNames[jobId] = jobName;

    /// <summary>Called by BaseTickerQJob after successful handler execution.</summary>
    public static void UnregisterJobName(Guid jobId) => _jobNames.TryRemove(jobId, out _);

    /// <summary>
    /// Pattern: TickerQ calls this when a job throws an unhandled exception.
    /// Logs the error with the human-readable job name (not just the Guid).
    /// </summary>
    public Task HandleExceptionAsync(TickerExceptionContext context)
    {
        var jobName = _jobNames.GetValueOrDefault(context.TickerId, "Unknown");

        logger.LogError(context.Exception,
            "TickerQ job failed — Job: {JobName}, Id: {TickerId}, Retry: {RetryCount}",
            jobName, context.TickerId, context.RetryCount);

        // Pattern: Clean up mapping after logging — job is finished (failed).
        _jobNames.TryRemove(context.TickerId, out _);

        return Task.CompletedTask;
    }
}
