// ═══════════════════════════════════════════════════════════════
// Pattern: OpenTelemetry ActivitySource — distributed tracing for scheduler jobs.
// Each job creates an Activity via this source in BaseTickerQJob.
// Aspire dashboard shows traces across API → Scheduler → DB hops.
// ═══════════════════════════════════════════════════════════════

using System.Diagnostics;

namespace TaskFlow.Scheduler.Telemetry;

/// <summary>
/// Pattern: Static ActivitySource — shared across all scheduler jobs.
/// Activities created from this source appear as spans in the Aspire trace viewer.
/// Named to match the Meter name for consistent telemetry grouping.
/// </summary>
public static class SchedulingActivitySource
{
    public static readonly ActivitySource Source = new("TaskFlow.Scheduler");
}
