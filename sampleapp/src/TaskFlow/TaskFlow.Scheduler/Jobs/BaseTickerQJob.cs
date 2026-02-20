// ═══════════════════════════════════════════════════════════════
// Pattern: BaseTickerQJob — base class for all TickerQ job functions.
// Provides scoped service resolution, OpenTelemetry tracing,
// custom metrics, and structured error handling.
// All concrete jobs inherit from this and call ExecuteJobAsync<THandler>.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.Scheduler.Abstractions;
using TaskFlow.Scheduler.Infrastructure;
using TaskFlow.Scheduler.Telemetry;
using System.Diagnostics;
using TickerQ.Utilities.Base;

namespace TaskFlow.Scheduler.Jobs;

/// <summary>
/// Pattern: Abstract base for all TickerQ jobs.
/// Rule: Jobs ≠ Handlers — jobs are thin TickerQ adapters.
/// All business logic goes in IScheduledJobHandler implementations.
/// 
/// This base provides:
/// 1. Job name registration for exception handler (human-readable names)
/// 2. OpenTelemetry activity tracing with tags
/// 3. Scoped service resolution for handler
/// 4. Metrics recording (success/retry/failure)
/// 5. Structured logging (start/complete/error)
/// </summary>
public abstract class BaseTickerQJob(
    IServiceScopeFactory serviceScopeFactory,
    SchedulingMetrics metrics,
    ILogger logger)
{
    protected async Task ExecuteJobAsync<THandler>(
        string jobName,
        TickerFunctionContext context,
        CancellationToken cancellationToken) where THandler : IScheduledJobHandler
    {
        // Pattern: Register human-readable job name for the exception handler.
        // TickerQ identifies jobs by Guid — this mapping provides readable logs.
        TaskFlowSchedulerExceptionHandler.RegisterJobName(context.Id, jobName);

        var stopwatch = Stopwatch.StartNew();

        // Pattern: OpenTelemetry activity with semantic tags.
        using var activity = SchedulingActivitySource.Source.StartActivity($"Job.{jobName}");
        activity?.SetTag("job.name", jobName);
        activity?.SetTag("job.id", context.Id);
        activity?.SetTag("job.attempt", context.RetryCount + 1);

        try
        {
            logger.LogInformation("Starting job — {JobName}, Id: {JobId}, Attempt: {Attempt}",
                jobName, context.Id, context.RetryCount + 1);

            // Pattern: Create scope and resolve handler.
            // Rule: Handlers resolve scoped services within this scope — not via constructor.
            using var scope = serviceScopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();

            var jobContext = new JobExecutionContext(
                JobId: context.Id.ToString(),
                JobName: jobName,
                ScheduledTime: DateTimeOffset.UtcNow,
                ActualTime: DateTimeOffset.UtcNow,
                Attempt: context.RetryCount + 1);

            await handler.ExecuteAsync(jobContext, cancellationToken);

            stopwatch.Stop();
            metrics.RecordJobSuccess(jobName, stopwatch.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);

            logger.LogInformation("Completed job — {JobName}, Duration: {DurationMs}ms",
                jobName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            // Pattern: Record retry metric if this is a retry attempt.
            if (context.RetryCount > 0)
                metrics.RecordJobRetry(jobName, context.RetryCount + 1);
            throw; // Re-throw — caught by global TaskFlowSchedulerExceptionHandler.
        }
        finally
        {
            // Pattern: Unregister job name only on success — exception handler needs it on failure.
            if (activity?.Status == ActivityStatusCode.Ok)
                TaskFlowSchedulerExceptionHandler.UnregisterJobName(context.Id);
        }
    }
}
