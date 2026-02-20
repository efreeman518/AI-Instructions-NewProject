# Notifications

## Overview

The notification system provides multi-channel message delivery (email, SMS, web push, app push) through a provider-based architecture. It can be deployed in two modes:

1. **Integrated** — notification services are registered in the API host's DI container and invoked directly from application services or message handlers.
2. **Standalone** — a separate ASP.NET Core Minimal API microservice (`{Host}.Notification`) with its own endpoints, independently deployable.

> Choose **integrated** when notification volume is low and the API host can handle the load. Choose **standalone** when notifications require independent scaling, isolation, or their own retry/queue infrastructure.

## When to Use

- The domain inputs specify `notifications: true` or list notification channels.
- The solution needs to send emails, SMS, web push, or mobile push notifications.
- Event-driven flows (e.g., scheduled call reminders, user onboarding) trigger messages.

## Project Structure

### Infrastructure Library (always created)

```
src/Infrastructure/
└── {Project}.Infrastructure.Notification/
    ├── Infrastructure.Notification.csproj
    ├── ServiceCollectionExtensions.cs      # DI registration
    ├── INotificationService.cs             # Unified interface
    ├── NotificationService.cs              # Orchestrator
    ├── NotificationServiceSettings.cs      # Settings POCO
    ├── Configuration/
    │   └── NotificationOptions.cs          # Strongly-typed config
    ├── Model/
    │   ├── EmailMessage.cs
    │   ├── SmsMessage.cs
    │   ├── WebPushMessage.cs
    │   └── AppPushMessage.cs
    ├── Providers/
    │   ├── IProviders.cs                   # Per-channel interfaces
    │   ├── Email/
    │   │   └── SmtpEmailProvider.cs
    │   ├── Sms/
    │   │   └── TwilioSmsProvider.cs        # or Azure Communication Services
    │   ├── WebPush/
    │   │   └── WebPushProvider.cs
    │   └── AppPush/
    │       └── AppPushProvider.cs
    ├── Exceptions/
    │   └── NotificationException.cs
    └── Utilities/
        └── BatchThrottlingOptions.cs
```

### Standalone Host (optional — separate deployable)

```
src/{Host}/
└── {Host}.Notification/
    ├── {Host}.Notification.csproj
    ├── Program.cs
    ├── RegisterNotificationServices.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── Dockerfile
    ├── Properties/
    │   └── launchSettings.json
    ├── Contracts/
    │   └── NotificationRequest.cs
    ├── Endpoints/
    │   └── NotificationEndpoints.cs
    ├── Services/
    │   └── INotificationAppService.cs
    │   └── NotificationAppService.cs
    ├── Infrastructure/
    │   └── ExceptionHandlerMiddleware.cs
    └── Telemetry/
        └── NotificationMetrics.cs
```

## NuGet Packages

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Notification/TaskFlow.Infrastructure.Notification.csproj`

Key packages: `MailKit`, `Lib.Net.Http.WebPush`, `Microsoft.Extensions.Http.Resilience`, `Polly`. Standalone host also references the notification infrastructure project and Bootstrapper.

## Provider Interfaces

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Notification/Providers/IProviders.cs`

Each channel (email, SMS, web push, app push) has its own interface: `IEmailProvider`, `ISmsProvider`, `IWebPushProvider`, `IAppPushProvider`. All return `Task<bool>` for single sends and `Task<IReadOnlyList<bool>>` for batch sends.

## Unified Notification Service

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Notification/NotificationService.cs`

`INotificationService` provides a unified interface for all channels. The implementation takes optional provider dependencies — if a provider is null (not configured), it logs a warning and returns false (graceful degradation). Each method wraps the provider call in try/catch for fault isolation.

## DI Registration

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Notification/ServiceCollectionExtensions.cs`

Providers are registered conditionally based on configuration sections. If `Notification:Email` exists, `IEmailProvider` is registered; similarly for SMS, WebPush, AppPush. The unified `INotificationService` is always registered. Resilience is added via `AddStandardResilienceHandler()` on the HTTP client.

        // Register unified service
        services.AddSingleton<INotificationService, NotificationService>();

        // Add resilience (Polly retry + circuit breaker)
        services.AddHttpClient("NotificationClient")
            .AddStandardResilienceHandler();

        return services;
    }
}
```

## Integrated Mode — Bootstrapper Registration

Register notification services in the Bootstrapper chain. Then invoke `INotificationService` from application services or message handlers:

```csharp
// Condensed pattern — see sampleapp message handler for full example
public class CallReminderHandler(INotificationService notificationService) : IMessageHandler<CallReminderEvent>
{
    public async Task HandleAsync(CallReminderEvent message, CancellationToken ct)
    {
        await notificationService.SendSmsAsync(new SmsMessage
        {
            To = message.PhoneNumber,
            Body = $"Reminder: Your call is scheduled for {message.ScheduledTime:g}"
        }, ct);
    }
}
```

## Standalone Mode — Program.cs

The standalone notification microservice follows the standard host pattern: Aspire service defaults → notification infrastructure DI → app-level service → OpenAPI → endpoint mapping. See the API skill (`api.md`) for the general Program.cs pattern.

## Standalone Mode — Endpoints

Notification endpoints map to `api/notifications` with POST routes per channel (`/email`, `/email/batch`, `/sms`) and a GET `/status` for provider health. Follow the same Minimal API patterns described in `api.md`.

## Configuration

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Notification/NotificationServiceSettings.cs` and `Configuration/NotificationOptions.cs`

The `Notification` config section defines per-channel settings:

| Key | Purpose |
|-----|---------|
| `Notification:Email:Provider` | Provider type (e.g., `Smtp`, `AzureCommunication`) |
| `Notification:Email:Smtp:Host/Port/UseSsl/FromAddress` | SMTP connection details |
| `Notification:Sms:Provider` | Provider type (e.g., `Twilio`) |
| `Notification:Sms:Twilio:AccountSid/AuthToken/FromNumber` | Twilio credentials (from Key Vault) |
| `Notification:BatchThrottling:MaxConcurrency` | Max concurrent sends per batch |
| `Notification:BatchThrottling:DelayBetweenBatchesMs` | Throttle delay between batches |

## Aspire Integration (Standalone Mode)

In standalone mode, declare the notification microservice in the Aspire AppHost as a separate project with HTTP endpoints and optional Redis reference for template caching.

## Lite Mode

When scaffolding in lite mode:
- Skip the notification system entirely unless explicitly requested.
- If requested in lite mode, use integrated mode with email-only provider.
- Skip batch endpoints, SMS, web push, and app push providers.
- Skip telemetry classes and resilience configuration.

## Rules

1. **Provider pattern** — each channel (email, SMS, push) has its own `I{Channel}Provider` interface. Never put channel-specific logic in the unified `NotificationService`.
2. **Resilience** — use `Microsoft.Extensions.Http.Resilience` (Polly) for retry and circuit-breaker on all external calls. Configure per-provider via `IOptions<T>`.
3. **Batch throttling** — batch sends must respect `MaxConcurrency` to avoid provider rate limits. Use `SemaphoreSlim` or `Parallel.ForEachAsync` with throttling.
4. **No domain dependency** — `Infrastructure.Notification` must NOT reference Domain or Application projects. It receives plain message DTOs only.
5. **Integrated vs standalone** — the infrastructure library is identical in both modes. Only the host project differs. Never duplicate provider implementations.
6. **Configuration-driven providers** — only register providers that have configuration present. Missing config section = provider not available (graceful degradation).
7. **Secrets** — credentials (API keys, SMTP passwords) must come from Key Vault or user secrets, never from `appsettings.json` directly.
8. **Placeholder tokens** — see [placeholder-tokens.md](../placeholder-tokens.md) for all token definitions.

## Verification

1. `dotnet build src/Infrastructure/{Project}.Infrastructure.Notification/` — confirm clean build
2. If standalone: `dotnet build src/{Host}/{Host}.Notification/` — confirm host builds
3. Verify `INotificationService` is registered in DI (integrated: Bootstrapper, standalone: Program.cs)
4. Verify provider interfaces are registered only when configuration section exists
5. Confirm no references to Domain.Model or Application.Services from the notification project
