# Background Services (TickerQ Scheduler)

## Prerequisites

- [solution-structure.md](solution-structure.md)
- [bootstrapper.md](bootstrapper.md)
- [aspire.md](aspire.md)
- [data-access.md](data-access.md)

## Purpose

Use `{Host}.Scheduler` for cron/time-based orchestration with persisted scheduling state via TickerQ. Keep in-process queue consumers and listeners in `{Host}.BackgroundServices`.

## Non-Negotiables

1. Scheduler is a separate host project from API.
2. Job methods are thin `[TickerFunction]` adapters; business logic lives in handlers.
3. Deploy one scheduler replica unless Redis coordination is enabled.
4. TickerQ persistence uses `SchedulerDbContext` and `[Scheduler]` schema.
5. TickerQ schema setup is not EF migrations.

---

## Scheduler vs BackgroundServices

- `{Host}.Scheduler`: persisted cron/time scheduling, dashboard, orchestration.
- `{Host}.BackgroundServices`: channel consumers, long-running listeners, queue pumps.
- Use both when needed, but keep their responsibilities and deployment independent.

## Minimal Scheduler Structure

```
{Host}.Scheduler/
├── Program.cs
├── RegisterSchedulerServices.cs
├── Abstractions/IScheduledJobHandler.cs
├── Jobs/BaseTickerQJob.cs
├── Jobs/{Feature}Jobs.cs
├── Handlers/{JobName}Handler.cs
├── Infrastructure/{App}SchedulerExceptionHandler.cs
├── Infrastructure/SchedulerHealthCheck.cs
└── appsettings*.json
```

Reference: `sample-app/src/TaskFlow/TaskFlow.Scheduler/`.

---

## Registration Sequence (Required)

The startup flow must remain in this order:

1. Add service defaults.
2. Register bootstrapper infra/app services.
3. Register scheduler-specific services.
4. Configure TickerQ.
5. Build app.
6. Configure/validate TickerQ database.
7. Enable `UseTickerQ()` middleware.
8. Map health/endpoints and run.

```csharp
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

---

## Job/Handler Split (Required)

```csharp
public class ReminderJobs(...) : BaseTickerQJob(...)
{
    [TickerFunction("ProcessDueReminders", "10 */5 * * * *", TickerTaskPriority.High)]
    public async Task ProcessDueRemindersAsync(TickerFunctionContext context, CancellationToken ct)
    {
        await ExecuteJobAsync<ProcessDueRemindersHandler>("ProcessDueReminders", context, ct);
    }
}
```

- Job methods only map trigger → handler.
- Handler implements domain/application logic and remains testable.
- `BaseTickerQJob` manages scope creation, telemetry, and exception wiring.

---

## TickerQ Configuration Contract

`RegisterSchedulerServices` and `AddTickerQConfig` must cover:

- Scoped handlers and job adapters.
- Scheduler settings (`MaxConcurrency`, time zone, poll interval).
- EF Core persistence for TickerQ with `[Scheduler]` schema.
- Optional dashboard (secure credentials only).
- Optional Redis coordination for multi-node deployments.

If workflows are time-policy sensitive (billing windows, scheduled publish, SLA windows), bind a shared time-boundary policy and avoid hard-coded timezone math inside handlers.

Key settings:

| Section | Keys |
|---|---|
| `ConnectionStrings` | `{Project}DbContextTrxn`, `SchedulerDbContext` |
| `Scheduling` | `UsePersistence`, `EnableDashboard`, `EnableRedis`, `PollIntervalSeconds`, `GenerateDeploymentScript` |
| `Scheduling:Dashboard` | `Username`, `Password` |

## Database Setup

- `SchedulerDbContext` may target same database as app DB or a dedicated DB.
- TickerQ tables remain isolated by `[Scheduler]` schema.
- Setup options:
  - Enable `Scheduling:GenerateDeploymentScript`.
  - Or apply manual TickerQ SQL setup script.

---

## Runtime Scheduling APIs

- One-off jobs: use `ITimeTickerManager<T>.EnqueueAsync("JobName", scheduledTime, payload)`.
- Cron jobs: use `ICronTickerManager<T>.AddOrUpdateCronTickerAsync(...)` and `RemoveCronTickerAsync(...)`.

TickerQ cron format is six fields: seconds, minutes, hours, day-of-month, month, day-of-week.

For ingestion/event-time workflows, define and apply allowed-lateness/watermark behavior before triggering reconciliation jobs.

---

## Deployment Rules

- Default deployment is single scheduler replica.
- Multi-node scheduler is allowed only with Redis coordination package/config.
- Keep scheduler as its own resource in Aspire/AppHost.
- If dashboard is enabled, secure credentials through environment variables or Key Vault.

---

## Lite Mode

In `lite` mode keep only:

- Program + registration + one job + one handler.
- Persistence and core scheduling settings.

Skip by default:

- Dashboard
- Custom telemetry classes
- Custom exception handler
- Expanded one-off/cron examples

---

## Verification

- [ ] Scheduler project builds cleanly
- [ ] `dotnet run --project src/{Host}/{Host}.Scheduler` starts successfully
- [ ] At least one `[TickerFunction]` is registered
- [ ] Jobs delegate through `ExecuteJobAsync<THandler>()`
- [ ] `SchedulerDbContext` is resolved and reachable
- [ ] Aspire config uses `WithReplicas(1)` unless Redis coordination is enabled
- [ ] If dashboard enabled, credentials are not default/plain test values
- [ ] If `{Host}.BackgroundServices` exists, it remains a separate project

See [placeholder-tokens.md](../placeholder-tokens.md) for token definitions.