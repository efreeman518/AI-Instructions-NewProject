# Messaging

Base types (`IServiceBusSender`, `IEventGridPublisher`, `IEventHubProducer`) come from the `EF.Messaging` package — see [package-dependencies.md](package-dependencies.md) and the [EF.Packages repo](https://github.com/efreeman518/EF.Packages) for full API details.

## Prerequisites

- [package-dependencies.md](package-dependencies.md)
- [bootstrapper.md](bootstrapper.md)
- [configuration.md](configuration.md)
- [background-services.md](background-services.md)

Rule: use `IInternalMessageBus` for in-process events; use this skill for cross-service messaging.

## Service Selection

| Need | Service |
|---|---|
| Reliable queue/topic workflows, retries, DLQ | Service Bus |
| Event notifications and pub/sub routing | Event Grid |
| High-throughput telemetry/event streams | Event Hub |

## Core Pattern

Implement messaging as provider-specific adapters with shared conventions:

- settings class per concrete sender/processor
- named Azure SDK clients via `IAzureClientFactory<T>`
- correlation IDs + metadata propagation
- scoped DI in background handlers

### Service Bus (queue/topic workflows)

```csharp
public interface IServiceBusSender
{
    Task SendMessageAsync(string queueOrTopicName, string message,
        string? correlationId = null,
        IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
}

public class {Project}ServiceBusSender : ServiceBusSenderBase, I{Project}ServiceBusSender { }
public class {Project}ServiceBusProcessor : ServiceBusProcessorBase, I{Project}ServiceBusProcessor { }
```

```csharp
services.AddAzureClients(builder =>
    builder.AddServiceBusClient(config.GetConnectionString("ServiceBus1")!)
        .WithName("{Project}SBClient"));

services.Configure<{Project}ServiceBusSenderSettings>(config.GetSection("{Project}ServiceBusSenderSettings"));
services.Configure<{Project}ServiceBusProcessorSettings>(config.GetSection("{Project}ServiceBusProcessorSettings"));
```

```csharp
sbProcessor.RegisterProcessor("todoitem-processing", null,
    async args =>
    {
        using var scope = serviceProvider.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ITodoItemService>();
        var payload = JsonSerializer.Deserialize<TodoItemCompletedMessage>(args.Message.Body.ToString());
        await svc.ProcessCompletionAsync(payload!.TodoItemId, stoppingToken);
    },
    args => { logger.LogError(args.Exception, "Service Bus error on {EntityPath}", args.EntityPath); return Task.CompletedTask; });
```

### Event Grid (pub/sub notifications)

```csharp
public interface IEventGridPublisher
{
    Task<int> SendAsync(EventGridEvent egEvent, CancellationToken cancellationToken = default);
}

public class {Project}EventGridPublisher : EventGridPublisherBase, I{Project}EventGridPublisher { }
```

```csharp
services.AddAzureClients(builder =>
    builder.AddEventGridPublisherClient(
        new Uri(config["EventGrid:TopicEndpoint"]!),
        new AzureKeyCredential(config["EventGrid:TopicKey"]!))
    .WithName("{Project}EGClient"));
```

### Event Hub (high-throughput streams)

```csharp
public interface IEventHubProducer
{
    Task SendAsync(string message, string? partitionId = null, string? partitionKey = null,
        string? correlationId = null, IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
}

public interface IEventHubProcessor
{
    Task RegisterAndStartEventProcessor(
        Func<ProcessEventArgs, Task> funcProcess,
        Func<ProcessErrorEventArgs, Task> funcError,
        CancellationToken cancellationToken);
}
```

```csharp
services.AddAzureClients(builder =>
{
    builder.AddEventHubProducerClient(config.GetConnectionString("EventHub1")!, "hub-name")
        .WithName("{Project}EHProducer");

    builder.AddEventProcessorClient(
            config.GetConnectionString("EventHub1")!,
            "$Default",
            config.GetConnectionString("BlobStorage1")!,
            "event-hub-checkpoints")
        .WithName("{Project}EHProcessor");
});
```

## Aspire Integration

```csharp
var serviceBus = builder.AddAzureServiceBus("ServiceBus1");
serviceBus.AddQueue("todoitem-processing");

var eventHub = builder.AddAzureEventHubs("EventHub1").AddHub("telemetry");

builder.AddProject<Projects.{Project}_Api>("{project}-api")
    .WithReference(serviceBus)
    .WithReference(eventHub);
```

## Rules

1. One settings class per concrete sender/processor (`*SettingsBase` inheritance).
2. Named Azure clients via `IAzureClientFactory<T>`.
3. Background processors create DI scopes for scoped dependencies.
4. Preserve correlation IDs in message metadata.
5. Event Hub processors checkpoint regularly (not every event unless required).
6. Configure retries + dead-letter handling for Service Bus consumers.
7. Keep message contracts versioned and backward-compatible.

## Verification

- [ ] Sender/processor inherit correct base classes
- [ ] Named clients and settings sections are aligned
- [ ] Background processors are registered and startable
- [ ] Batch sending handles message-size constraints
- [ ] Aspire references match connection names used by services
