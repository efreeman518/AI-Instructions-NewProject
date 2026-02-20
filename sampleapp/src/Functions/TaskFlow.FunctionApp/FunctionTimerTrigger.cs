// ═══════════════════════════════════════════════════════════════
// Pattern: Timer Trigger — cron-scheduled function.
// Uses %SettingName% syntax to read cron expression from config.
// ExponentialBackoffRetry for transient failures.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Timer trigger with retry policy.
/// Rule: Use %SettingName% for trigger bindings that read from configuration.
/// Rule: Apply [ExponentialBackoffRetry] to timer and message triggers.
/// Cron expression comes from local.settings.json "TimerCron" or Azure App Setting.
/// </summary>
public class FunctionTimerTrigger(
    ILogger<FunctionTimerTrigger> logger,
    IConfiguration configuration,
    IOptions<Settings> settings)
{
    [Function(nameof(FunctionTimerTrigger))]
    [ExponentialBackoffRetry(5, "00:00:05", "00:15:00")]
    public async Task Run([TimerTrigger("%TimerCron%")] TimerInfo timerInfo)
    {
        logger.LogInformation("TimerTrigger - Start {ExecutionUtc}", DateTime.UtcNow);

        // Pattern: Delegate to application service.
        // var result = await reminderService.ProcessDueRemindersAsync();
        await Task.CompletedTask;

        logger.LogInformation("TimerTrigger - Finish {ExecutionUtc} {NextSchedule}",
            DateTime.UtcNow, timerInfo.ScheduleStatus?.Next);
    }
}
