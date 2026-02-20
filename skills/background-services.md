# Background Services (TickerQ Scheduler)

## Prerequisites

- [solution-structure.md](solution-structure.md) — project layout and package management
- [bootstrapper.md](bootstrapper.md) — DI registration and startup tasks
- [aspire.md](aspire.md) — Aspire orchestration (Scheduler runs as its own project)
- [data-access.md](data-access.md) — if scheduler jobs need database access

## Overview

Background services use **TickerQ** as the primary scheduler for cron-based and one-off time-based jobs. TickerQ provides source-generated job handlers, EF Core persistence (SQL Server), a built-in dashboard, and cron expression scheduling — all running in-process within a dedicated Scheduler host project.

The key architectural pattern separates **Jobs** (thin TickerQ adapters with `[TickerFunction]` attributes) from **Handlers** (testable business logic implementing `IScheduledJobHandler`). A `BaseTickerQJob` class provides telemetry, metrics, and error handling common to all jobs.

### Scheduler vs. BackgroundServices

This skill covers the **TickerQ Scheduler** — a dedicated host for scheduled/cron-based jobs.

The solution also includes a **`{Host}.BackgroundServices`** project for non-scheduled background work:

| Concern | `{Host}.Scheduler` (this skill) | `{Host}.BackgroundServices` |
|---------|----------------------------------|----------------------------|
| **Engine** | TickerQ | `IHostedService` / `BackgroundService` |
| **Use case** | Cron jobs, recurring scheduled tasks, time-based one-off jobs | Task queues, channel-based processors, long-running listeners |
| **Persistence** | SQL Server (`SchedulerDbContext`, `[Scheduler]` schema) | None (in-memory channels) or custom |
| **Dashboard** | TickerQ built-in dashboard | None |
| **Scaling** | Single replica (or Redis-coordinated multi-node) | Multiple replicas OK |
| **Examples** | Nightly data sync, hourly report generation, retry stale items | Process uploaded files, handle webhook callbacks, run ML inference |

Use `{Host}.BackgroundServices` when you need hosted services, `System.Threading.Channels`-based task queues, or other non-TickerQ background processing. Both are optional; include them based on domain requirements.

## Architecture

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/` for the complete Scheduler project layout, and `sampleapp/src/TaskFlow/TaskFlow.BackgroundServices/` for channel-based background services.

```
{Host}.Scheduler/
├── Program.cs                              # Host startup, TickerQ middleware
├── RegisterSchedulerServices.cs            # TickerQ config + scheduler-specific DI
├── Abstractions/
│   └── IScheduledJobHandler.cs             # Clean handler interface + JobExecutionContext
├── Jobs/
│   ├── BaseTickerQJob.cs                   # Base class — telemetry, metrics, scope creation
│   ├── {Feature}Jobs.cs                    # TickerQ adapter (one class per feature area)
├── Handlers/
│   ├── {JobName}Handler.cs                 # Business logic (testable, no TickerQ deps)
├── Infrastructure/
│   ├── {App}SchedulerExceptionHandler.cs   # Global TickerQ exception handler
│   ├── SchedulerHealthCheck.cs             # Custom scheduler health check
├── Telemetry/
│   ├── SchedulingMetrics.cs                # OpenTelemetry Meter (counters, histograms)
│   └── SchedulingActivitySource.cs         # OpenTelemetry ActivitySource
├── appsettings.json / .Development.json / .Production.json
└── Dockerfile
```

The Scheduler project is a **separate host** from the API:
- Runs as a standalone ASP.NET Core app so it can host the TickerQ dashboard.
- Shares the same `Bootstrapper` for infrastructure/application DI but registers its own TickerQ-specific services.
- In Aspire, it runs as its own project with `WithReplicas(1)` to prevent duplicate job execution (unless Redis coordination is enabled).
- In production, deploy as a separate Azure Container App or App Service with a **single replica** (unless Redis coordination is enabled via `TickerQ.Caching.StackExchangeRedis`).
- TickerQ uses a **separate connection string** (`SchedulerDbContext`) with its own `[Scheduler]` schema — this can point to the same physical database or a dedicated one.

## Program.cs

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Program.cs`

The Scheduler host follows: Aspire service defaults → Bootstrapper chain → `RegisterSchedulerServices` → `AddTickerQConfig` → build → `ConfigureTickerQDatabase` → `UseTickerQ()` → health/OpenAPI endpoints → run.

```csharp
// Condensed pattern — see sampleapp for full implementation
builder.AddServiceDefaults(config, appName);
services
    .RegisterInfrastructureServices(config)
    .RegisterApplicationServices(config)
    .RegisterSchedulerServices(config);
builder.AddTickerQConfig();
var app = builder.Build();
await app.ConfigureTickerQDatabase(config, logger);
app.UseTickerQ();
app.MapDefaultEndpoints();
await app.RunAsync();
```

## TickerQ Configuration (RegisterSchedulerServices)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/RegisterSchedulerServices.cs`

`RegisterSchedulerServices` registers: scoped job handlers, scoped TickerQ job adapters, singleton telemetry metrics, health checks. `AddTickerQConfig` configures: scheduler (max concurrency, timezone, idle timeout, poll interval), EF Core persistence (SQL Server, `[Scheduler]` schema), and optional dashboard (basic auth).

## Job Handler Interface (Abstraction)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Abstractions/IScheduledJobHandler.cs`

`IScheduledJobHandler` defines a clean abstraction with `JobName` property and `ExecuteAsync(JobExecutionContext, CancellationToken)` method. `JobExecutionContext` is a record containing JobId, JobName, ScheduledTime, ActualTime, Attempt, and optional CustomData.

## Base Job Class (Telemetry Wrapper)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Jobs/BaseTickerQJob.cs`

`BaseTickerQJob` is the abstract base for all TickerQ job functions. It provides `ExecuteJobAsync<THandler>()` which:
1. Registers the job name for the exception handler
2. Starts a `Stopwatch` and OpenTelemetry `Activity`
3. Creates a DI scope, resolves the handler, and calls `ExecuteAsync`
4. Records success metrics and activity status
5. On failure, records retry metrics and re-throws for the global exception handler

## Concrete Job (TickerQ Adapter)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Jobs/ReminderJobs.cs` and `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Jobs/MaintenanceJobs.cs`

Each Job class groups related `[TickerFunction]` methods for a feature area. The attribute defines the job name, cron expression, and priority. All business logic is delegated to the handler.

```csharp
// Condensed pattern — see sampleapp for full implementation
public class {Feature}Jobs(...) : BaseTickerQJob(...)
{
    [TickerFunction("Process{Feature}", "10 */5 * * * *", TickerTaskPriority.High)]
    public async Task Process{Feature}Async(TickerFunctionContext context, CancellationToken ct)
    {
        await ExecuteJobAsync<Process{Feature}Handler>("Process{Feature}", context, ct);
    }
}
```

## Concrete Handler (Business Logic)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Handlers/ProcessDueRemindersHandler.cs` and `DatabaseMaintenanceHandler.cs`

Handlers implement `IScheduledJobHandler` and contain the actual business logic. They are decoupled from TickerQ and fully testable. Handlers create their own service scope to resolve application services.

## Telemetry

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Telemetry/SchedulingMetrics.cs` and `SchedulingActivitySource.cs`

`SchedulingMetrics` uses `IMeterFactory` to create counters (executions, failures, retries) and a histogram (duration) under the `{Host}.Scheduler` meter. `SchedulingActivitySource` provides a static `ActivitySource` for OpenTelemetry distributed tracing.

## Exception Handler

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Infrastructure/TaskFlowSchedulerExceptionHandler.cs`

The global TickerQ exception handler implements `ITickerExceptionHandler`. It uses a static `ConcurrentDictionary<Guid, string>` to map TickerQ's internal job GUIDs to human-readable job names for structured logging. `BaseTickerQJob` calls `RegisterJobName`/`UnregisterJobName` around each execution.

## Health Check

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/Infrastructure/SchedulerHealthCheck.cs`

The scheduler health check returns `Healthy` with data about persistence, dashboard, and poll interval settings from configuration.

## Scheduling Jobs at Runtime

### Scheduling Time-Based Jobs (One-Off)

Inject `ITimeTickerManager<T>` to schedule one-off jobs from application services. Call `EnqueueAsync("JobName", scheduledTime, payload)` to queue a time-based job.

### Managing Cron Jobs at Runtime

Inject `ICronTickerManager<T>` to add, update, or remove cron schedules at runtime:
- `AddOrUpdateCronTickerAsync("JobName", "0 0 * * * *")` — create or update
- `RemoveCronTickerAsync("JobName")` — remove a cron job

## Common Cron Expressions

| Expression | Schedule |
|------------|----------|
| `0 */5 * * * *` | Every 5 minutes |
| `10 */5 * * * *` | Every 5 minutes at 10 seconds past the minute |
| `0 0 * * * *` | Every hour |
| `0 0 0 * * *` | Daily at midnight UTC |
| `0 0 2 * * 0` | Every Sunday at 2 AM UTC |
| `0 0 9 * * 1-5` | Weekdays at 9 AM UTC |
| `0 0 0 1 * *` | First of each month at midnight UTC |

> TickerQ uses **6-field cron** (seconds, minutes, hours, day-of-month, month, day-of-week).

## TickerQ Database Setup

TickerQ persists its job state in a SQL Server database using a dedicated `[Scheduler]` schema. This uses a **separate connection string** (`SchedulerDbContext`) from the application's main `{Project}DbContextTrxn`.

> **Important**: TickerQ schema management does NOT use EF Core migrations. It uses either a SQL setup script or the `GenerateDeploymentScript` config option.

### Option A: Auto-Generated Deployment Script

Set `Scheduling:GenerateDeploymentScript` to `true` in appsettings. TickerQ generates the SQL script at startup:

```json
{
  "Scheduling": {
    "GenerateDeploymentScript": true
  }
}
```

### Option B: Manual SQL Script

Run the TickerQ database setup SQL against your target database. See the `TickerQ_Database_Setup.md` in the Scheduler project for the full script.

### ConfigureTickerQDatabase Extension

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/RegisterSchedulerServices.cs` — `ConfigureTickerQDatabase` method

Verifies TickerQ database connectivity at startup. Skips gracefully if `SchedulerDbContext` connection string is not configured.

## Configuration

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/appsettings.json`

Key configuration sections:

| Section | Keys | Purpose |
|---------|------|--------|
| `ConnectionStrings` | `{Project}DbContextTrxn`, `SchedulerDbContext` | Main DB + TickerQ persistence (can be same DB) |
| `Scheduling` | `UsePersistence`, `EnableDashboard`, `EnableRedis`, `PollIntervalSeconds`, `GenerateDeploymentScript` | TickerQ behavior |
| `Scheduling:Dashboard` | `Username`, `Password` | Dashboard basic auth (use Key Vault in prod) |

> The `SchedulerDbContext` connection can point to the same database as the main `{Project}DbContextTrxn` — TickerQ uses the `[Scheduler]` schema to keep tables separate.

### Redis Coordination (Multi-Node)

For high-availability deployments with multiple scheduler instances, add `TickerQ.Caching.StackExchangeRedis` and enable via `Scheduling:EnableRedis`. Without Redis, deploy with **exactly one replica** to prevent duplicate job execution.

## Aspire Integration

> **Reference implementation:** See `sampleapp/src/Aspire/AppHost/AppHost.cs` — scheduler resource

The Scheduler is declared as a separate project with `WithReplicas(1)`, references to both the main DB and `SchedulerDbContext`, and shared Aspire infrastructure (App Config, App Insights, Redis).

## NuGet Packages

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Scheduler/TaskFlow.Scheduler.csproj`

Key packages: `TickerQ`, `TickerQ.EntityFrameworkCore`, `TickerQ.Dashboard`, `Microsoft.EntityFrameworkCore.SqlServer`, `Scalar.AspNetCore`. Optional: `TickerQ.Caching.StackExchangeRedis` for multi-node coordination.

## Lite Mode

When scaffolding in lite mode:
- Create the Scheduler project with Program.cs, one `BaseTickerQJob`-derived job, and one handler.
- Skip the dashboard configuration (`EnableDashboard: false`).
- Skip telemetry classes (`SchedulingMetrics`, `SchedulingActivitySource`).
- Skip the exception handler (use default TickerQ error logging).
- Skip time-based (one-off) job examples.
- Use inline connection string instead of Azure App Configuration.

## Rules

1. **Jobs ≠ Handlers** — Job classes (`BaseTickerQJob` subclasses) are thin TickerQ adapters. Handlers (`IScheduledJobHandler`) contain the business logic. Never put business logic in job classes.
2. **Single replica** — deploy with exactly one replica unless Redis coordination (`TickerQ.Caching.StackExchangeRedis`) is enabled. Multiple uncoordinated instances cause duplicate job execution.
3. **Scoped services** — `BaseTickerQJob.ExecuteJobAsync<THandler>()` creates its own `IServiceScope`. Handlers resolve scoped services within that scope; do NOT inject scoped services via constructor.
4. **[TickerFunction] attribute** — every job method must be decorated with `[TickerFunction("UniqueName", "cron", Priority)]`. The name must be globally unique across all Job classes.
5. **Separate SchedulerDbContext** — TickerQ uses a dedicated connection string (`SchedulerDbContext`) and `[Scheduler]` schema. This can point to the same database as the main DbContext or a dedicated one.
6. **No EF Core migrations for TickerQ** — TickerQ manages its own schema via SQL scripts or `GenerateDeploymentScript`, not EF migrations.
7. **Dashboard security** — never use default credentials in production. Configure via environment variables or Azure Key Vault.
8. **Cancellation tokens** — always accept and honor `CancellationToken` in handlers.
9. **Idempotency** — design jobs to be idempotent. If a job fails and retries, running it twice must not corrupt state.
10. **Telemetry** — all jobs pass through `BaseTickerQJob` which records OpenTelemetry traces and custom metrics automatically.
11. **Exception handler** — `{App}SchedulerExceptionHandler` uses a static `ConcurrentDictionary` to map TickerQ's `Guid` job IDs to human-readable job names for logging.
12. **Placeholder tokens** — see [placeholder-tokens.md](../placeholder-tokens.md) for all token definitions.

---

## Verification

After creating the Scheduler project, verify:

- [ ] `dotnet build` compiles the Scheduler project with zero errors
- [ ] `dotnet run --project src/{Host}/{Host}.Scheduler` starts without exceptions
- [ ] TickerQ dashboard is accessible at the configured URL (if `EnableDashboard: true`)
- [ ] At least one `[TickerFunction]` job is registered and visible in the dashboard
- [ ] `SchedulerDbContext` connection string is injected correctly (matching Aspire or appsettings)
- [ ] Jobs use `BaseTickerQJob.ExecuteJobAsync<THandler>()` — business logic is in handlers, not in job classes
- [ ] Scheduler runs with `WithReplicas(1)` in Aspire (single instance)
- [ ] `SchedulingMetrics` counters increment on job execution (check Aspire dashboard traces)
- [ ] If `{Host}.BackgroundServices` is also used, it is a separate project from the Scheduler
