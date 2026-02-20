# Azure Functions (Isolated Worker)

## Prerequisites

- [solution-structure.md](solution-structure.md) — project layout
- [bootstrapper.md](bootstrapper.md) — DI registration
- [aspire.md](aspire.md) — Aspire orchestration

## Overview

Azure Functions uses the **.NET isolated worker model (v4)** for event-driven, serverless compute running alongside the core API. Functions handle triggers that don't belong in the API (timers, blob events, service bus messages, event grid subscriptions) while sharing the same application services via the Bootstrapper.

## Bootstrap Path (Template → Starter → Full)

Many new solutions begin with the default template (`Program.cs` + single `Function1`). Treat that as a bootstrap shell only.

1. **Template stage** — host starts locally with one timer/http function.
2. **Starter profile stage** — replace template with this skill's baseline: structured `Program.cs`, middleware, HTTP + Timer triggers, `Settings` options, Bootstrapper registrations.
3. **Full profile stage** — add Blob/Queue/ServiceBus/EventGrid triggers once bindings, infra, and testability are ready.

Do not scaffold full trigger coverage into a template-stage project in one pass; promote in stages to keep local Aspire runs cohesive.

## Scaffolding Profiles

Use `functionProfile` from [domain-inputs.schema.md](../domain-inputs.schema.md) to choose initial scope:

- `starter` — scaffold host + HTTP + Timer trigger first, then add message/blob/event triggers incrementally.
- `full` — scaffold HTTP/Timer/Blob/StorageQueue/ServiceBus/EventGrid trigger pattern with middleware and telemetry.

Use `starter` when dependencies like Service Bus, Event Grid subscriptions, or Azurite are not ready yet.

### Template Compatibility Note

If the local `Functions/FunctionApp` is template-level, align to `functionProfile: starter` first and verify local run (`func host start` or via Aspire) before adding non-HTTP triggers.

## Project Structure

```
src/Functions/{App}.FunctionApp/
├── Program.cs
├── {App}.FunctionApp.csproj
├── Settings.cs                        # POCO for IOptions<> injection
├── appsettings.json                   # App-level config (NOT runtime startup — use local.settings.json for that)
├── host.json
├── local.settings.json
├── Function{TriggerType}.cs           # One file per trigger (flat — no subfolder)
├── FunctionHttpTrigger.cs
├── FunctionTimerTrigger.cs
├── FunctionBlobTrigger.cs
├── FunctionServiceBusQueue.cs
├── FunctionServiceBusTopic.cs
├── FunctionEventGridTriggerBlob.cs
├── FunctionEventGridTriggerCustom.cs
├── FunctionStorageQueueTrigger.cs
├── FunctionHttpHealth.cs
├── Infrastructure/
│   ├── GlobalExceptionHandler.cs      # IFunctionsWorkerMiddleware — catches + logs all exceptions
│   ├── GlobalLogger.cs                # IFunctionsWorkerMiddleware — logs trigger/finish per invocation
│   ├── IDatabaseService.cs            # Function-specific service interfaces
│   └── DatabaseService.cs
└── Model/
    ├── EventGridEvent.cs              # Custom model if needed (Azure SDK provides one too)
    └── TimerInfo.cs
```

> **Naming convention:** Trigger classes are named `Function{TriggerType}` (e.g., `FunctionBlobTrigger`, `FunctionHttpHealth`). One trigger per file, flat in the project root.

## NuGet Packages

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/TaskFlow.FunctionApp.csproj` and `sampleapp/src/Directory.Packages.props`

Key package groups:
- **Functions core:** `Microsoft.Azure.Functions.Worker`, `.Worker.Sdk`, `.Extensions.Http`, `.Extensions.Http.AspNetCore`, `.Extensions.Timer`, `.Extensions.Storage`, `.Extensions.ServiceBus`, `.Extensions.EventGrid`, `.Extensions.Warmup`, `.Extensions.Abstractions`, `.ApplicationInsights`
- **Identity + Azure App Config:** `Azure.Identity`, `Microsoft.Azure.AppConfiguration.Functions.Worker`
- **Observability:** `Azure.Monitor.OpenTelemetry.Exporter`, `Microsoft.ApplicationInsights.WorkerService`, `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.Http/Runtime`

The `.csproj` uses `<AzureFunctionsVersion>v4</AzureFunctionsVersion>`, `<OutputType>Exe</OutputType>`, and references the Bootstrapper project.

## Program.cs

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/Program.cs`

The Function App host follows: `FunctionsApplication.CreateBuilder(args)` → load `appsettings.json` (before `ConfigureFunctionsWorkerDefaults`) → static startup logger → `ConfigureFunctionsWebApplication()` → `DefaultAzureCredential` → Azure App Config (optional) → OpenTelemetry (logs, traces, metrics) → Bootstrapper chain → function-specific DI + middleware → build → run.

```csharp
// Condensed pattern — see sampleapp for full implementation
var builder = FunctionsApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true);  // BEFORE ConfigureFunctionsWorkerDefaults
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

> **Key points:**
> - `appsettings.json` must be loaded **before** `ConfigureFunctionsWorkerDefaults` (which adds env vars).
> - `local.settings.json` is for the Functions runtime startup only — not for app-level config.
> - Top-level try/catch/finally ensures startup failures are logged to App Insights.
> - `RunStartupTasks()` runs any `IStartupTask` implementations (e.g., EF migrations, cache warmup).

## Middleware

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/Infrastructure/GlobalExceptionHandler.cs` and `GlobalLogger.cs`

### Global Exception Handler

`GlobalExceptionHandler` implements `IFunctionsWorkerMiddleware`. It wraps `next(context)` in try/catch, logs exceptions, and can inject pre/post-function context items.

### Global Logger

`GlobalLogger` implements `IFunctionsWorkerMiddleware`. It logs trigger start/finish with function name, timestamp, and binding data for structured tracing.

## Trigger Templates

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/` for all trigger files:
> - `FunctionHttpTrigger.cs` — HTTP trigger with `AuthorizationLevel.Function`
> - `FunctionTimerTrigger.cs` — Timer with `[ExponentialBackoffRetry]` and `%TimerCron%` binding
> - `FunctionBlobTrigger.cs` — Blob trigger with `%BlobContainer%` binding
> - `FunctionServiceBusQueueTrigger.cs` — Service Bus queue trigger
> - `FunctionStorageQueueTrigger.cs` — Storage queue trigger
> - `FunctionEventGridTrigger.cs` — Event Grid trigger
> - `FunctionHttpHealth.cs` — Anonymous HTTP health check

All triggers use:
- **Primary constructors** for DI injection (`ILogger<T>`, `IConfiguration`, `IOptions<Settings>`)
- **`[Function(nameof(ClassName))]`** for type-safe naming
- **`%SettingName%`** syntax for config bindings (resolved from `local.settings.json`)
- **Structured logging** with start/finish per invocation

### Trigger Pattern Summary

| Trigger | Attribute | Binding Expression | Retry |
|---------|-----------|-------------------|-------|
| HTTP | `[HttpTrigger(AuthorizationLevel.Function, "get", "post")]` | Route | None |
| Timer | `[TimerTrigger("%TimerCron%")]` | `%TimerCron%` | `[ExponentialBackoffRetry]` |
| Blob | `[BlobTrigger("%BlobContainer%/{fileName}")]` | `Connection = "StorageBlob1"` | 5 retries → poison queue |
| Service Bus Queue | `[ServiceBusTrigger("%QueueName%")]` | `Connection = "ServiceBusQueue"` | Dead-letter |
| Service Bus Topic | `[ServiceBusTrigger("%TopicName%", "%SubName%")]` | `Connection = "ServiceBusTopic"` | Dead-letter |
| Event Grid (Blob) | `[EventGridTrigger]` | Subscription filter | Retry policy |
| Event Grid (Custom) | `[EventGridTrigger]` | Custom topic/domain | Retry policy |
| Storage Queue | `[QueueTrigger("%QueueName%")]` | `Connection = "StorageQueue1"` | 5 retries → poison queue |
| Health | `[HttpTrigger(AuthorizationLevel.Anonymous, "get")]` | None | None |

## Settings Class

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/Settings.cs`

Create Settings POCOs as needed for `IOptions<T>` injection. Register via `.Configure<T>(config.GetSection("SectionName"))` in Program.cs.

## host.json

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/host.json`

Key sections: `version: "2.0"`, `logging.applicationInsights.samplingSettings`, `logLevel` overrides per function/namespace. Adjust as needed for debugging.

## appsettings.json

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/appsettings.json`

For config setup/injection — **NOT** for Function runtime startup (use `local.settings.json` for that). Contains `CacheSettings`, custom Settings sections, and `Logging` configuration.

## local.settings.json

> **Reference implementation:** See `sampleapp/src/Functions/TaskFlow.FunctionApp/local.settings.json`

Required keys: `AzureWebJobsStorage`, `FUNCTIONS_WORKER_RUNTIME: "dotnet-isolated"`, trigger bindings (`TimerCron`, `BlobContainer`, `ServiceBusQueueName`, etc.), connection strings for storage/service bus, and DB connection string.

> **Azurite** is required to simulate Azure Storage locally. Install via `npm install -g azurite` and run with `azurite -s -l c:\azurite`. Use Azure Storage Explorer to manage local blobs and queues.

## Aspire Integration

> **Reference implementation:** See `sampleapp/src/Aspire/AppHost/AppHost.cs` (search for Functions project registration)

Register the Functions project via `AddProject<Projects.{Host}_Functions>` with `.WithReference()` for DB and Redis. Functions may need additional infrastructure references (storage, service bus) provided via Aspire `AddAzureStorage()`, `AddAzureServiceBus()`, etc.

## Local Development Setup

1. **Install Azure Functions Core Tools** — [latest version](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
2. **Install Azurite** — `npm install -g azurite`, run `azurite -s -l c:\azurite`
3. **VS Tunnel / ngrok** — for local EventGrid debugging: `./ngrok http http://localhost:7071`
4. **User Secrets** — `dotnet user-secrets set "ConnectionStrings:{Project}DbContextTrxn" "..."`
5. **Run** — `func host start --verbose` or F5 in Visual Studio

> Go to Tools > Options > Projects & Solutions > Azure Functions and click **Check for updates** periodically.

## Lite Mode

When scaffolding in lite mode:
- Create the Functions project with Program.cs and one HTTP trigger only.
- Skip timer, blob, service bus, event grid, and storage queue triggers.
- Skip OpenTelemetry configuration (use simple console logging).
- Skip middleware (GlobalExceptionHandler, GlobalLogger).
- Use `local.settings.json` only (skip Azure App Configuration).

## Rules

1. **Isolated worker model** — always use `FunctionsApplication.CreateBuilder(args)` with `ConfigureFunctionsWebApplication()`. Never use the in-process model.
2. **Primary constructors** — use C# primary constructors for DI in trigger classes.
3. **Bootstrapper** — reuse the shared Bootstrapper for DI registration. Do not duplicate service registrations. Function-specific services go after the Bootstrapper chain.
4. **`[Function(nameof(ClassName))]`** — always use `nameof()` for the function name to avoid string drift.
5. **AuthorizationLevel** — use `Function` for most triggers; `Anonymous` only for health checks. Never use `Anonymous` for business endpoints.
6. **App settings references** — use `%SettingName%` syntax for trigger bindings that read from configuration (e.g., `%TimerCron%`, `%BlobContainer%`).
7. **Retry policies** — apply `[ExponentialBackoffRetry]` or `[FixedDelayRetry]` to timer and message triggers.
8. **Flat file layout** — trigger classes live in the project root (not in a `Triggers/` subfolder). Name them `Function{TriggerType}`.
9. **Middleware** — register `GlobalExceptionHandler` and `GlobalLogger` via `builder.UseMiddleware<T>()`. Exception handler wraps all invocations; logger adds structured tracing per invocation.
10. **appsettings.json vs local.settings.json** — `appsettings.json` is for app-level config (caching, service URLs, etc.). `local.settings.json` is for the Functions runtime and trigger bindings. Load `appsettings.json` explicitly **before** `ConfigureFunctionsWorkerDefaults`.
11. **Comment out unneeded triggers** — comment out the `[Function]` attribute on triggers that aren't wired up yet (e.g., Service Bus) to prevent runtime errors.
12. **Placeholder tokens** — see [placeholder-tokens.md](../placeholder-tokens.md) for all token definitions.
---

## Verification

After generating the Function App, confirm:

- [ ] Project targets `net9.0` with `<OutputType>Exe</OutputType>` and `<AzureFunctionsVersion>v4</AzureFunctionsVersion>`
- [ ] `Program.cs` loads `appsettings.json` before `ConfigureFunctionsWorkerDefaults()`
- [ ] `AddBootstrapper()` called for shared DI (same as API/Scheduler)
- [ ] `GlobalExceptionHandler` and `GlobalLogger` middleware registered
- [ ] Trigger classes are in the project root, named `Function{TriggerType}` (e.g., `FunctionTimerTrigger`)
- [ ] `local.settings.json` has `"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"`
- [ ] Unused triggers have `[Function]` attribute commented out (not deleted)
- [ ] `host.json` configures retry, logging, and extension bundles
- [ ] Cross-references: Aspire AppHost references Function project per [aspire.md](aspire.md), IaC maps to Azure Function App per [iac.md](iac.md)