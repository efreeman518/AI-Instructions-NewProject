# Notifications

Reference implementation: `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Notification/`.

## Purpose

Provide multi-channel outbound notifications (email, SMS, web push, app push) through a unified service with pluggable channel providers.

## Deployment Modes

1. **Integrated**: notification infrastructure registered in API host.
2. **Standalone**: separate `{Host}.Notification` host for independent scale/retry/isolation.

Use integrated for low/moderate throughput; standalone for high volume or operational isolation.

## Non-Negotiables

1. Keep channel-specific logic in channel providers, not in unified service.
2. Register providers conditionally from config presence.
3. Use resilience policies for external provider calls.
4. Keep credentials in Key Vault/user-secrets, never inline in appsettings.
5. `Infrastructure.Notification` must not depend on Domain/Application implementation projects.

---

## Structure

### Infrastructure library (always)

```
src/Infrastructure/{Project}.Infrastructure.Notification/
├── ServiceCollectionExtensions.cs
├── INotificationService.cs
├── NotificationService.cs
├── NotificationServiceSettings.cs
├── Configuration/NotificationOptions.cs
├── Model/
├── Providers/
└── Exceptions/
```

### Standalone host (optional)

```
src/{Host}/{Host}.Notification/
├── Program.cs
├── RegisterNotificationServices.cs
├── Endpoints/
├── Services/
└── Telemetry/
```

---

## Core Contracts

Per-channel provider interfaces:

- `IEmailProvider`
- `ISmsProvider`
- `IWebPushProvider`
- `IAppPushProvider`

Unified contract:

- `INotificationService`

Unified service coordinates optional provider dependencies. Missing provider => graceful degradation (warn + false/no-op result).

---

## DI Registration Pattern

```csharp
public static IServiceCollection AddNotificationInfrastructure(
    this IServiceCollection services,
    IConfiguration config)
{
    // Conditionally register channel providers if config sections exist
    // Register unified service
    services.AddSingleton<INotificationService, NotificationService>();

    services.AddHttpClient("NotificationClient")
        .AddStandardResilienceHandler();

    return services;
}
```

Integrated mode: call from Bootstrapper.

Standalone mode: register in notification host and expose minimal API endpoints.

---

## Usage Pattern

Typical event-handler usage:

```csharp
public class CallReminderHandler(INotificationService notificationService) : IMessageHandler<CallReminderEvent>
{
    public async Task HandleAsync(CallReminderEvent message, CancellationToken ct)
    {
        await notificationService.SendSmsAsync(new SmsMessage
        {
            To = message.PhoneNumber,
            Body = $"Reminder: your call is scheduled for {message.ScheduledTime:g}"
        }, ct);
    }
}
```

---

## Configuration Surface

`Notification` section should include channel-specific provider settings and optional batch throttling.

Representative keys:

- `Notification:Email:Provider`
- `Notification:Email:Smtp:*`
- `Notification:Sms:Provider`
- `Notification:Sms:Twilio:*`
- `Notification:BatchThrottling:MaxConcurrency`
- `Notification:BatchThrottling:DelayBetweenBatchesMs`

---

## Lite Mode

For `scaffoldMode: lite`:

- skip notifications unless explicitly requested,
- if requested, use integrated + email-only,
- skip batch endpoints/telemetry/extra channels by default.

---

## Operational Rules

1. Keep provider registration configuration-driven.
2. Add retry/circuit-breaker resilience for outbound provider clients.
3. Respect throttling limits for batch sends (provider rate limits).
4. Keep infrastructure library reusable across integrated and standalone modes.
5. Avoid duplicate provider implementations across hosts.

---

## Verification

- [ ] infrastructure notification project builds cleanly
- [ ] standalone host (if enabled) builds and starts
- [ ] `INotificationService` is registered in DI
- [ ] channel providers register only when config exists
- [ ] no Domain/Application implementation dependencies in infrastructure notification project
- [ ] secrets are sourced from secure providers (not plain appsettings)
- [ ] resilience policies are enabled for external notification calls