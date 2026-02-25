# Message Handler Template

## Output

| Field | Value |
|-------|-------|
| **File** | `src/Application/{Project}.Application.MessageHandlers/{EventName}Handler.cs` |
| **Depends on** | `Application.Contracts` (for event DTOs in `Events/`), `EF.BackgroundServices` (for `IMessageHandler<T>`, `IInternalMessageBus` in namespace `EF.BackgroundServices.InternalMessageBus`) |
| **Referenced by** | Auto-registered by `IInternalMessageBus.AutoRegisterHandlers()` in Bootstrapper |

---

## Event DTO

```csharp
// File: src/Application/{Project}.Application.Contracts/Events/{EventName}.cs
namespace Application.Contracts.Events;

/// <summary>
/// Raised when {describe when this event occurs}.
/// </summary>
public record {EventName}(
    Guid Id,
    Guid TenantId,
    // Add event-specific properties
    string Detail
);
```

---

## Handler

```csharp
// File: src/Application/{Project}.Application.MessageHandlers/{EventName}Handler.cs
using Application.Contracts.Events;
using Microsoft.Extensions.Logging;
using EF.BackgroundServices.InternalMessageBus;

namespace Application.MessageHandlers;

public class {EventName}Handler(
    ILogger<{EventName}Handler> logger) : IMessageHandler<{EventName}>
{
    public async Task HandleAsync({EventName} message, CancellationToken ct = default)
    {
        logger.LogInformation("{Handler} processing {Event}: {Id}",
            nameof({EventName}Handler), nameof({EventName}), message.Id);

        // ===== Business Logic =====
        // Examples:
        //   - Send notification (email, push, SMS)
        //   - Update a read model or cache
        //   - Trigger a downstream workflow
        //   - Audit logging

        await Task.CompletedTask;
    }
}
```

---

## Publishing Events

Events are published through `IInternalMessageBus` from service methods:

```csharp
// In a service class (e.g., {Entity}Service.cs)
public class {Entity}Service(
    IInternalMessageBus messageBus,
    // ... other dependencies
    ) : I{Entity}Service
{
    public async Task<Result<DefaultResponse<{Entity}Dto>>> CreateAsync(
        DefaultRequest<{Entity}Dto> request, CancellationToken ct = default)
    {
        // ... create entity logic ...

        await repoTrxn.SaveChangesAsync(ct);

        // Publish event after successful save
        await messageBus.PublishAsync(new {EventName}(
            newEntity.Id,
            newEntity.TenantId,
            "Entity created"
        ), ct);

        return Result<DefaultResponse<{Entity}Dto>>.Success(
            new() { Item = newEntity.ToDto() });
    }
}
```

---

## Handler Registration

Handlers are auto-discovered. In the Bootstrapper:

```csharp
// In RegisterApplicationServices():
services.AutoRegisterHandlers(typeof({EventName}Handler).Assembly);
```

The `AutoRegisterHandlers` extension scans the assembly for all `IMessageHandler<T>` implementations and registers them as scoped services.

---

## Common Handler Patterns

### Audit Handler

```csharp
public class AuditHandler(ILogger<AuditHandler> logger) : IMessageHandler<AuditEvent>
{
    public Task HandleAsync(AuditEvent message, CancellationToken ct = default)
    {
        logger.LogInformation("AUDIT [{Action}] Entity={Entity} Id={Id} By={User}",
            message.Action, message.EntityType, message.EntityId, message.UserId);
        return Task.CompletedTask;
    }
}
```

### Reschedule / Side-Effect Handler

```csharp
public class RescheduleCallRequestHandler(
    ILogger<RescheduleCallRequestHandler> logger,
    I{Entity}RepositoryTrxn repo) : IMessageHandler<RescheduleCallRequest>
{
    public async Task HandleAsync(RescheduleCallRequest message, CancellationToken ct = default)
    {
        var entity = await repo.Get{Entity}Async(message.EntityId, false, ct);
        if (entity == null) return;

        // Apply side effect
        entity.Update(/* ... */);
        await repo.SaveChangesAsync(ct);
    }
}
```

### Callback Validation Handler (for webhook-originated events)

```csharp
public class ProviderWebhookReceivedHandler(
    ILogger<ProviderWebhookReceivedHandler> logger,
    IWebhookValidator webhookValidator,
    IEventDeduplicator deduplicator) : IMessageHandler<ProviderWebhookReceived>
{
    public async Task HandleAsync(ProviderWebhookReceived message, CancellationToken ct = default)
    {
        if (!webhookValidator.IsValid(message.Signature, message.Timestamp, message.RawPayload))
            return;

        if (!await deduplicator.TryBeginAsync(message.ProviderEventId, ct))
            return;

        // apply side effects only after validation + dedup
    }
}
```

---

## Notes

- Handlers should be **thin** — delegate complex logic to services or domain methods.
- Each handler handles **one event type**. Use separate handler classes for different events.
- Handlers run **in-process** via `IInternalMessageBus`. For cross-service messaging, use Azure Service Bus (see [function-app.md](../skills/function-app.md) for Service Bus triggers).
- Keep handlers **idempotent** — the same event may be delivered more than once in retry scenarios.
- Use `CancellationToken` and honor cancellation in all async operations.
- If workflow compensation metadata exists, keep rollback handlers explicit and ordered according to workflow policy.
