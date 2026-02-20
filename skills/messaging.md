# Messaging

## Prerequisites

- [package-dependencies.md](package-dependencies.md) — `Package.Infrastructure.Messaging` package types
- [solution-structure.md](solution-structure.md) — project layout and Infrastructure layer conventions
- [bootstrapper.md](bootstrapper.md) — centralized DI registration
- [configuration.md](configuration.md) — appsettings and secrets management
- [background-services.md](background-services.md) — hosted service patterns for message processors
- [function-app.md](function-app.md) — Azure Functions triggers for Service Bus, Event Grid, Event Hub

## Overview

External messaging uses `Package.Infrastructure.Messaging` which provides abstractions over three Azure messaging services:

| Service | Package Namespace | Pattern | Best For |
|---------|------------------|---------|----------|
| **Service Bus** | `Messaging.ServiceBus` | Queue / Topic-Subscription | Reliable command processing, ordered delivery, dead-letter handling |
| **Event Grid** | `Messaging.EventGrid` | Publish-Subscribe | Event-driven reactions, Azure resource events, webhooks |
| **Event Hub** | `Messaging.EventHub` | Stream ingestion | High-throughput telemetry, event streams, real-time analytics |

> **Internal vs External Messaging:** Use `IInternalMessageBus` (from `Package.Infrastructure.BackgroundService`) for in-process domain events within the same host. Use the messaging abstractions in this skill for cross-service/cross-process communication.

---

## Service Bus

### Sender Interface

```csharp
public interface IServiceBusSender
{
    Task SendMessageAsync(string queueOrTopicName, string message,
        string? correlationId = null, IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
    Task SendBatchAsync<T>(string queueOrTopicName, ICollection<T> batch,
        string? correlationId = null, CancellationToken cancellationToken = default);
}
```

### Receiver/Processor Interface

```csharp
public interface IServiceBusReceiver { }

// ServiceBusProcessorBase provides:
// - RegisterProcessor(queueOrTopicName, subscriptionName, funcProcess, funcError)
// - StartProcessingAsync(queueOrTopicName, subscriptionName?, cancellationToken)
// - StopProcessingAsync(queueOrTopicName, subscriptionName?, cancellationToken)
```

### Settings

```csharp
public class ServiceBusSenderSettingsBase
{
    public string ServiceBusClientName { get; set; } = null!;
    public bool LogMessageData { get; set; } = true;
}

public class ServiceBusProcessorSettingsBase
{
    public string ServiceBusClientName { get; set; } = null!;
    public bool LogMessageData { get; set; } = true;
    public ServiceBusProcessorOptions ServiceBusProcessorOptions { get; set; } = null!;
}
```

### Concrete Implementations

```csharp
namespace {Project}.Infrastructure.Messaging;

// Sender
public class {Project}ServiceBusSender : ServiceBusSenderBase, I{Project}ServiceBusSender
{
    public {Project}ServiceBusSender(
        ILogger<{Project}ServiceBusSender> logger,
        IOptions<{Project}ServiceBusSenderSettings> settings,
        IAzureClientFactory<ServiceBusClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public interface I{Project}ServiceBusSender : IServiceBusSender { }
public class {Project}ServiceBusSenderSettings : ServiceBusSenderSettingsBase { }

// Processor
public class {Project}ServiceBusProcessor : ServiceBusProcessorBase, I{Project}ServiceBusProcessor
{
    public {Project}ServiceBusProcessor(
        ILogger<{Project}ServiceBusProcessor> logger,
        IOptions<{Project}ServiceBusProcessorSettings> settings,
        IAzureClientFactory<ServiceBusClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public interface I{Project}ServiceBusProcessor : IServiceBusReceiver { }
public class {Project}ServiceBusProcessorSettings : ServiceBusProcessorSettingsBase { }
```

### Configuration

```json
{
  "ConnectionStrings": {
    "ServiceBus1": ""
  },
  "{Project}ServiceBusSenderSettings": {
    "ServiceBusClientName": "{Project}SBClient",
    "LogMessageData": true
  },
  "{Project}ServiceBusProcessorSettings": {
    "ServiceBusClientName": "{Project}SBClient",
    "LogMessageData": true,
    "ServiceBusProcessorOptions": {
      "MaxConcurrentCalls": 10,
      "AutoCompleteMessages": true,
      "PrefetchCount": 0
    }
  }
}
```

### DI Registration

```csharp
private static void AddServiceBusServices(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.AddServiceBusClient(config.GetConnectionString("ServiceBus1")!)
            .WithName("{Project}SBClient");
    });

    // Sender
    services.Configure<{Project}ServiceBusSenderSettings>(
        config.GetSection("{Project}ServiceBusSenderSettings"));
    services.AddScoped<I{Project}ServiceBusSender, {Project}ServiceBusSender>();

    // Processor (typically singleton for background processing)
    services.Configure<{Project}ServiceBusProcessorSettings>(
        config.GetSection("{Project}ServiceBusProcessorSettings"));
    services.AddSingleton<I{Project}ServiceBusProcessor, {Project}ServiceBusProcessor>();
}
```

### Usage — Sending

```csharp
public class OrderService(
    I{Project}ServiceBusSender sbSender,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<Result> SubmitOrderAsync(OrderDto dto, CancellationToken ct = default)
    {
        // ... validate and save order ...

        // Send message to queue for async processing
        await sbSender.SendMessageAsync(
            queueOrTopicName: "order-processing",
            message: JsonSerializer.Serialize(new OrderSubmittedMessage { OrderId = order.Id }),
            correlationId: Activity.Current?.Id,
            metadata: new Dictionary<string, object>
            {
                ["MessageType"] = nameof(OrderSubmittedMessage)
            },
            cancellationToken: ct);

        return Result.Success();
    }
}
```

### Usage — Batch Sending

```csharp
await sbSender.SendBatchAsync(
    queueOrTopicName: "notification-queue",
    batch: notifications.Select(n => new NotificationMessage
    {
        RecipientId = n.RecipientId,
        Body = n.Body
    }).ToList(),
    correlationId: Activity.Current?.Id,
    cancellationToken: ct);
```

### Usage — Receiving (Background Service)

```csharp
public class OrderProcessingBackgroundService(
    I{Project}ServiceBusProcessor sbProcessor,
    IServiceProvider serviceProvider,
    ILogger<OrderProcessingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        sbProcessor.RegisterProcessor(
            queueOrTopicName: "order-processing",
            subscriptionName: null,  // null for queue, set for topic subscription
            funcProcess: async args =>
            {
                using var scope = serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                var message = JsonSerializer.Deserialize<OrderSubmittedMessage>(
                    args.Message.Body.ToString());
                await orderService.ProcessOrderAsync(message!.OrderId, stoppingToken);
            },
            funcError: args =>
            {
                logger.LogError(args.Exception, "Service Bus processing error on {EntityPath}",
                    args.EntityPath);
                return Task.CompletedTask;
            });

        await sbProcessor.StartProcessingAsync("order-processing", cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

---

## Event Grid

### Publisher Interface

```csharp
public interface IEventGridPublisher
{
    Task<int> SendAsync(EventGridEvent egEvent, CancellationToken cancellationToken = default);
}
```

### Event Model

```csharp
// Package-provided event wrapper
public class EventGridEvent
{
    public string? Id { get; set; }
    public string Subject { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string DataVersion { get; set; } = null!;
    public object Data { get; set; } = null!;
    public DateTimeOffset? EventTime { get; set; }
    public string? Topic { get; set; }  // null for topic URL, set for domain URL
}
```

### Settings

```csharp
public class EventGridPublisherSettingsBase
{
    public string EventGridPublisherClientName { get; set; } = null!;
    public bool LogEventData { get; set; } = true;
}
```

### Concrete Implementation

```csharp
namespace {Project}.Infrastructure.Messaging;

public class {Project}EventGridPublisher : EventGridPublisherBase, I{Project}EventGridPublisher
{
    public {Project}EventGridPublisher(
        ILogger<{Project}EventGridPublisher> logger,
        IOptions<{Project}EventGridPublisherSettings> settings,
        IAzureClientFactory<EventGridPublisherClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public interface I{Project}EventGridPublisher : IEventGridPublisher { }
public class {Project}EventGridPublisherSettings : EventGridPublisherSettingsBase { }
```

### Configuration

```json
{
  "{Project}EventGridPublisherSettings": {
    "EventGridPublisherClientName": "{Project}EGClient",
    "LogEventData": true
  }
}
```

### DI Registration

```csharp
private static void AddEventGridServices(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.AddEventGridPublisherClient(
            new Uri(config["EventGrid:TopicEndpoint"]!),
            new Azure.AzureKeyCredential(config["EventGrid:TopicKey"]!))
            .WithName("{Project}EGClient");
    });

    services.Configure<{Project}EventGridPublisherSettings>(
        config.GetSection("{Project}EventGridPublisherSettings"));
    services.AddScoped<I{Project}EventGridPublisher, {Project}EventGridPublisher>();
}
```

### Usage

```csharp
await egPublisher.SendAsync(new EventGridEvent
{
    Subject = $"/orders/{orderId}",
    EventType = "Order.Approved",
    DataVersion = "1.0",
    Data = new { OrderId = orderId, ApprovedBy = userId }
}, ct);
```

---

## Event Hub

### Producer Interface

```csharp
public interface IEventHubProducer
{
    Task SendAsync(string message, string? partitionId = null, string? partitionKey = null,
        string? correlationId = null, IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
    Task SendBatchAsync<T>(ICollection<T> batch, string? partitionId = null,
        string? partitionKey = null, string? correlationId = null,
        IDictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);
}
```

### Processor Interface

```csharp
public interface IEventHubProcessor
{
    Task RegisterAndStartEventProcessor(
        Func<ProcessEventArgs, Task> funcProcess,
        Func<ProcessErrorEventArgs, Task> funcError,
        CancellationToken cancellationToken);
    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}
```

### Settings

```csharp
public class EventHubProducerSettingsBase
{
    public string EventHubProducerClientName { get; set; } = null!;
    public long? MaxBatchByteSize { get; set; } = null;
    public bool LogMessageData { get; set; } = true;
}

public class EventHubProcessorSettingsBase
{
    public string EventHubProcessorClientName { get; set; } = null!;
    public int TaskSleepIntervalSeconds { get; set; } = 10;
}
```

### Concrete Implementations

```csharp
namespace {Project}.Infrastructure.Messaging;

// Producer
public class {Project}EventHubProducer : EventHubProducerBase, I{Project}EventHubProducer
{
    public {Project}EventHubProducer(
        ILogger<{Project}EventHubProducer> logger,
        IOptions<{Project}EventHubProducerSettings> settings,
        IAzureClientFactory<EventHubProducerClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public interface I{Project}EventHubProducer : IEventHubProducer { }
public class {Project}EventHubProducerSettings : EventHubProducerSettingsBase { }

// Processor
public class {Project}EventHubProcessor : EventHubProcessorBase, I{Project}EventHubProcessor
{
    public {Project}EventHubProcessor(
        ILogger<{Project}EventHubProcessor> logger,
        IOptions<{Project}EventHubProcessorSettings> settings,
        IAzureClientFactory<EventProcessorClient> clientFactory)
        : base(logger, settings, clientFactory) { }
}

public interface I{Project}EventHubProcessor : IEventHubProcessor { }
public class {Project}EventHubProcessorSettings : EventHubProcessorSettingsBase { }
```

### Configuration

```json
{
  "ConnectionStrings": {
    "EventHub1": ""
  },
  "{Project}EventHubProducerSettings": {
    "EventHubProducerClientName": "{Project}EHProducer",
    "LogMessageData": true
  },
  "{Project}EventHubProcessorSettings": {
    "EventHubProcessorClientName": "{Project}EHProcessor"
  }
}
```

### DI Registration

```csharp
private static void AddEventHubServices(IServiceCollection services, IConfiguration config)
{
    services.AddAzureClients(builder =>
    {
        builder.AddEventHubProducerClient(config.GetConnectionString("EventHub1")!, "hub-name")
            .WithName("{Project}EHProducer");

        builder.AddEventProcessorClient(
            config.GetConnectionString("EventHub1")!,
            "$Default",  // consumer group
            config.GetConnectionString("BlobStorage1")!,  // checkpoint store
            "event-hub-checkpoints")  // checkpoint container
            .WithName("{Project}EHProcessor");
    });

    services.Configure<{Project}EventHubProducerSettings>(
        config.GetSection("{Project}EventHubProducerSettings"));
    services.AddScoped<I{Project}EventHubProducer, {Project}EventHubProducer>();

    services.Configure<{Project}EventHubProcessorSettings>(
        config.GetSection("{Project}EventHubProcessorSettings"));
    services.AddSingleton<I{Project}EventHubProcessor, {Project}EventHubProcessor>();
}
```

### Usage — Producing

```csharp
// Single message
await ehProducer.SendAsync(
    message: JsonSerializer.Serialize(telemetryEvent),
    partitionKey: tenantId.ToString(),
    correlationId: Activity.Current?.Id,
    cancellationToken: ct);

// Batch
await ehProducer.SendBatchAsync(
    batch: telemetryEvents,
    partitionKey: tenantId.ToString(),
    cancellationToken: ct);
```

### Usage — Processing (Background Service)

```csharp
public class TelemetryProcessingService(
    I{Project}EventHubProcessor ehProcessor,
    IServiceProvider serviceProvider,
    ILogger<TelemetryProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ehProcessor.RegisterAndStartEventProcessor(
            funcProcess: async args =>
            {
                var data = JsonSerializer.Deserialize<TelemetryEvent>(
                    args.Data.EventBody.ToString());
                // Process event...

                // Periodically checkpoint
                await args.UpdateCheckpointAsync(stoppingToken);
            },
            funcError: args =>
            {
                logger.LogError(args.Exception, "Event Hub processing error: {Operation}",
                    args.Operation);
                return Task.CompletedTask;
            },
            cancellationToken: stoppingToken);
    }
}
```

---

## Choosing a Messaging Service

| Criteria | Service Bus | Event Grid | Event Hub |
|----------|-------------|------------|-----------|
| **Pattern** | Command queue / pub-sub | Reactive events | Stream ingestion |
| **Ordering** | FIFO (sessions) | None | Per-partition |
| **Throughput** | Moderate (thousands/sec) | High (millions/sec) | Very high (millions/sec) |
| **Delivery** | At-least-once, at-most-once | At-least-once | At-least-once |
| **Retention** | Configurable (1-14 days) | 24h retry | Configurable (1-90 days) |
| **Dead-letter** | Built-in DLQ | Retry + dead-letter | Manual (via checkpoint) |
| **Use cases** | Orders, workflows, commands | Azure events, webhooks, notifications | Telemetry, logs, IoT, analytics |

---

## Aspire Integration

In `AppHost/Program.cs`:

```csharp
// Service Bus
var serviceBus = builder.AddAzureServiceBus("ServiceBus1");
serviceBus.AddQueue("order-processing");
serviceBus.AddTopic("order-events")
    .AddSubscription("billing")
    .AddSubscription("shipping");

// Event Hub
var eventHub = builder.AddAzureEventHubs("EventHub1")
    .AddHub("telemetry");

var api = builder.AddProject<Projects.{Project}_Api>("{project}-api")
    .WithReference(serviceBus)
    .WithReference(eventHub);
```

---

## Verification

After generating messaging code, confirm:

- [ ] Sender/producer inherits from correct base class with project-specific settings
- [ ] Processor/receiver inherits from correct base class with project-specific settings
- [ ] DI registers named Azure clients via `IAzureClientFactory`
- [ ] Settings classes inherit from correct `*SettingsBase` with client name configured
- [ ] Background service for processors uses `IServiceProvider.CreateScope()` for scoped dependencies
- [ ] Batch sending handles Azure SDK batch size limits (message too large → exception)
- [ ] Service Bus processor options set appropriate `MaxConcurrentCalls` and `AutoCompleteMessages`
- [ ] Event Hub processor checkpoints periodically (not every event — performance)
- [ ] Cross-references: Function App triggers ([function-app.md](function-app.md)) can consume the same queues/topics; IaC provisions namespaces and hubs
