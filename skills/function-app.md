# Azure Functions (Isolated Worker)

## Prerequisites

- [solution-structure.md](solution-structure.md)
- [bootstrapper.md](bootstrapper.md)
- [aspire.md](aspire.md)

## Purpose

Use an isolated-worker Function App for trigger-driven workloads that should run outside API request pipelines. Reuse shared domain/application/infrastructure registrations through Bootstrapper.

## Non-Negotiables

1. Use isolated worker model (`FunctionsApplication.CreateBuilder`) only.
2. Reuse Bootstrapper registration chain; avoid duplicating DI setup.
3. Keep runtime startup keys in `local.settings.json`; use `appsettings.json` for app-level options.
4. Use configuration-bound trigger expressions (`%SettingName%`) for non-hardcoded bindings.
5. Keep function endpoints secure (`AuthorizationLevel.Function` for business handlers; `Anonymous` for health only).

---

## Profile Strategy

Set `functionProfile` in [domain-inputs.schema.md](../domain-inputs.schema.md):

- `starter`: host + HTTP + Timer first.
- `full`: add Blob/StorageQueue/ServiceBus/EventGrid after dependencies are ready.

Prefer `starter` when local infra (Azurite, Service Bus, Event Grid route) is not ready.

---

## Minimal Project Shape

```
src/Functions/{App}.FunctionApp/
├── Program.cs
├── {App}.FunctionApp.csproj
├── Settings.cs
├── appsettings.json
├── host.json
├── local.settings.json
├── FunctionHttpTrigger.cs
├── FunctionTimerTrigger.cs
├── Infrastructure/
│   ├── GlobalExceptionHandler.cs
│   └── GlobalLogger.cs
└── Model/
```

Trigger files stay flat at the project root and use `Function{TriggerType}` naming.

---

## Host Boot Sequence (Required)

```csharp
var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.ConfigureFunctionsWebApplication();

builder.Services
    .RegisterDomainServices(config)
    .RegisterInfrastructureServices(config)
    .RegisterApplicationServices(config);

builder.UseMiddleware<GlobalExceptionHandler>();
builder.UseMiddleware<GlobalLogger>();

var app = builder.Build();
await app.RunAsync();
```

Key constraints:

- `appsettings.json` is loaded explicitly for app options.
- runtime binding values come from `local.settings.json`/environment.
- startup should surface failures through structured logging.

---

## Trigger Pattern

Use primary-constructor DI + explicit function attributes:

```csharp
public class FunctionTimerTrigger(ILogger<FunctionTimerTrigger> logger)
{
    [Function(nameof(FunctionTimerTrigger))]
    [ExponentialBackoffRetry(5, "00:00:05", "00:05:00")]
    public Task Run([TimerTrigger("%TimerCron%")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("Timer fired at {UtcNow}", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
```

### Trigger Matrix

| Trigger | Binding Pattern | Notes |
|---|---|---|
| HTTP | `[HttpTrigger(AuthorizationLevel.Function, ...)]` | Business endpoints |
| Timer | `[TimerTrigger("%TimerCron%") ]` | add retry policy |
| Blob | `[BlobTrigger("%BlobContainer%/{file}")]` | storage connection required |
| Storage Queue | `[QueueTrigger("%QueueName%") ]` | poison handling required |
| Service Bus Queue/Topic | `[ServiceBusTrigger(...)]` | dead-letter strategy required |
| Event Grid | `[EventGridTrigger]` | infra route/subscription required |
| Health | HTTP + `AuthorizationLevel.Anonymous` | health only |

---

## Configuration Contract

### `local.settings.json` (runtime)

Must include:

- `FUNCTIONS_WORKER_RUNTIME: dotnet-isolated`
- `AzureWebJobsStorage`
- binding keys (`TimerCron`, `BlobContainer`, queue/topic names)
- connection strings/setting names referenced by triggers

### `appsettings.json` (app options)

Use for host-specific options and injected settings objects (`IOptions<T>`).

### `host.json`

Use for logging, sampling, extension/runtime behavior.

---

## Dependencies

Core packages:

- `Microsoft.Azure.Functions.Worker`
- `Microsoft.Azure.Functions.Worker.Sdk`
- extensions needed by enabled triggers (`Http`, `Timer`, `Storage`, `ServiceBus`, `EventGrid`)

Project file requirements:

- `<AzureFunctionsVersion>v4</AzureFunctionsVersion>`
- `<OutputType>Exe</OutputType>`
- reference to Bootstrapper project

---

## Aspire Integration

Register Functions as its own project/resource in AppHost and add only required dependencies (`.WithReference(...)`) for enabled triggers and shared infrastructure.

---

## Local Development

1. Install Functions Core Tools.
2. Start required local dependencies:
   - Azurite for Blob/Queue triggers.
   - tunneling (`ngrok`/dev tunnel) for Event Grid callback testing.
3. Populate `local.settings.json` (or user secrets/environment values).
4. Run with `func host start --verbose`.

---

## Lite Mode

For `scaffoldMode: lite`:

- generate host + one HTTP trigger only,
- keep config minimal,
- skip advanced trigger wiring until needed.

---

## Rules

1. Isolated worker only; no in-process model.
2. Shared DI comes from Bootstrapper; function-specific additions are appended after.
3. Keep trigger classes flat and consistently named.
4. Prefer `nameof(ClassName)` in `[Function(...)]`.
5. Bind settings via `%SettingName%` expressions.
6. Apply retries for timer/message triggers.
7. Do not expose business handlers as `Anonymous`.
8. If trigger dependencies are not ready, disable registration cleanly rather than shipping broken bindings.
9. Token placeholders remain defined by [placeholder-tokens.md](../placeholder-tokens.md).

---

## Verification

- [ ] project uses `v4` isolated worker (`OutputType=Exe`)
- [ ] Program boot sequence includes Bootstrapper registrations
- [ ] runtime settings include `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
- [ ] trigger bindings resolve from configuration settings
- [ ] HTTP auth levels are correct (health-only anonymous)
- [ ] required trigger dependencies are available for enabled triggers
- [ ] AppHost wiring includes Function App references per [aspire.md](aspire.md)