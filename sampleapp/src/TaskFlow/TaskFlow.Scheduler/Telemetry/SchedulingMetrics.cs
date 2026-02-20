// ═══════════════════════════════════════════════════════════════
// Pattern: OpenTelemetry Meter — custom scheduler metrics.
// Counters for job executions, failures, retries.
// Histogram for job duration (milliseconds).
// Recorded automatically by BaseTickerQJob.
// ═══════════════════════════════════════════════════════════════

using System.Diagnostics.Metrics;

namespace TaskFlow.Scheduler.Telemetry;

/// <summary>
/// Pattern: Custom metrics using System.Diagnostics.Metrics.
/// IMeterFactory (injected) creates the Meter — Aspire automatically exports to OTLP.
/// Metric naming follows OpenTelemetry semantic conventions: {component}.{metric}.{unit}.
/// </summary>
public class SchedulingMetrics
{
    private readonly Counter<long> _jobExecutions;
    private readonly Counter<long> _jobFailures;
    private readonly Counter<long> _jobRetries;
    private readonly Histogram<double> _jobDuration;

    public SchedulingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("TaskFlow.Scheduler");

        _jobExecutions = meter.CreateCounter<long>(
            "scheduler.job.executions",
            description: "Total number of scheduled job executions");

        _jobFailures = meter.CreateCounter<long>(
            "scheduler.job.failures",
            description: "Total number of scheduled job failures");

        _jobRetries = meter.CreateCounter<long>(
            "scheduler.job.retries",
            description: "Total number of scheduled job retries");

        _jobDuration = meter.CreateHistogram<double>(
            "scheduler.job.duration",
            unit: "ms",
            description: "Duration of scheduled job executions in milliseconds");
    }

    public void RecordJobSuccess(string jobName, double durationMs)
    {
        _jobExecutions.Add(1, new KeyValuePair<string, object?>("job.name", jobName));
        _jobDuration.Record(durationMs, new KeyValuePair<string, object?>("job.name", jobName));
    }

    public void RecordJobFailure(string jobName)
    {
        _jobFailures.Add(1, new KeyValuePair<string, object?>("job.name", jobName));
    }

    public void RecordJobRetry(string jobName, int attempt)
    {
        _jobRetries.Add(1,
            new KeyValuePair<string, object?>("job.name", jobName),
            new KeyValuePair<string, object?>("job.attempt", attempt));
    }
}
