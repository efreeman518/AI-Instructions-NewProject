// ═══════════════════════════════════════════════════════════════
// Pattern: Scheduled job handler — ProcessDueReminders.
// Contains business logic decoupled from TickerQ infrastructure.
// Fully testable — inject mock IReminderService for unit tests.
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using TaskFlow.Scheduler.Abstractions;

namespace TaskFlow.Scheduler.Handlers;

/// <summary>
/// Pattern: IScheduledJobHandler implementation with business logic.
/// Rule: Handlers resolve scoped services via IServiceScopeFactory — not via constructor.
/// This handler queries for due reminders and processes them (sends notifications, marks sent).
/// Designed for idempotency — running twice for the same reminder is safe.
/// </summary>
public class ProcessDueRemindersHandler(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ProcessDueRemindersHandler> logger) : IScheduledJobHandler
{
    public string JobName => "ProcessDueReminders";

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing due reminders — JobId: {JobId}, Attempt: {Attempt}",
            context.JobId, context.Attempt);

        // Pattern: Create scope to resolve scoped services (DbContext, repositories).
        using var scope = serviceScopeFactory.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();

        // Pattern: Service method handles the batch — query due reminders, send notifications, mark sent.
        // Idempotent: reminders already marked IsSent=true are skipped on re-run.
        var processedCount = await reminderService.ProcessDueRemindersAsync(cancellationToken);

        logger.LogInformation("Processed {Count} due reminders — JobId: {JobId}",
            processedCount, context.JobId);
    }
}
