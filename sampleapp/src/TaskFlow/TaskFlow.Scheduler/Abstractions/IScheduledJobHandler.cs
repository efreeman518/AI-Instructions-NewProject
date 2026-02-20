// ═══════════════════════════════════════════════════════════════
// Pattern: Scheduled job handler abstraction.
// Clean interface decoupled from TickerQ infrastructure.
// Handlers implement this interface — fully testable without TickerQ.
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.Scheduler.Abstractions;

/// <summary>
/// Pattern: IScheduledJobHandler — clean handler interface.
/// Decouples business logic from TickerQ infrastructure.
/// Handlers are registered as scoped services and resolved by BaseTickerQJob.
/// </summary>
public interface IScheduledJobHandler
{
    /// <summary>Unique name matching the TickerFunction attribute name.</summary>
    string JobName { get; }

    /// <summary>Execute the scheduled job business logic.</summary>
    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pattern: Immutable context record passed from BaseTickerQJob to handlers.
/// Contains scheduling metadata without exposing TickerQ types.
/// </summary>
public record JobExecutionContext(
    string JobId,
    string JobName,
    DateTimeOffset ScheduledTime,
    DateTimeOffset ActualTime,
    int Attempt = 1,
    Dictionary<string, object>? CustomData = null);
