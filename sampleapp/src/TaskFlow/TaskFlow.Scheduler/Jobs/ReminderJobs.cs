// ═══════════════════════════════════════════════════════════════
// Pattern: Concrete TickerQ Job — Reminder processing.
// Thin adapter — delegates all business logic to ProcessDueRemindersHandler.
// [TickerFunction] attribute defines cron schedule + priority.
// ═══════════════════════════════════════════════════════════════

using TaskFlow.Scheduler.Handlers;
using TaskFlow.Scheduler.Telemetry;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Enums;

namespace TaskFlow.Scheduler.Jobs;

/// <summary>
/// Pattern: TickerQ adapter — one class per feature area.
/// Rule: Jobs ≠ Handlers — no business logic here, only [TickerFunction] wiring.
/// Each method delegates to ExecuteJobAsync&lt;THandler&gt; from BaseTickerQJob.
/// </summary>
public class ReminderJobs(
    IServiceScopeFactory serviceScopeFactory,
    SchedulingMetrics metrics,
    ILogger<ReminderJobs> logger) : BaseTickerQJob(serviceScopeFactory, metrics, logger)
{
    /// <summary>
    /// Process due reminders — runs every 5 minutes at 10 seconds past the minute.
    /// Pattern: High priority — user-facing notifications should fire promptly.
    /// Cron: 6-field format (seconds, minutes, hours, day-of-month, month, day-of-week).
    /// </summary>
    [TickerFunction("ProcessDueReminders", "10 */5 * * * *", TickerTaskPriority.High)]
    public async Task ProcessDueRemindersAsync(
        TickerFunctionContext context,
        CancellationToken cancellationToken)
    {
        await ExecuteJobAsync<ProcessDueRemindersHandler>("ProcessDueReminders", context, cancellationToken);
    }
}
