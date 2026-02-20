// ═══════════════════════════════════════════════════════════════
// Pattern: Service Bus Queue Trigger — message-driven processing.
// Rule: Comment out [Function] attribute when Service Bus isn't wired up yet.
// ═══════════════════════════════════════════════════════════════

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Service Bus queue trigger — processes messages from a queue.
/// Rule: Comment out [Function] attribute on triggers that aren't wired up yet
/// to prevent runtime errors during local development.
/// </summary>
public class FunctionServiceBusQueueTrigger(
    ILogger<FunctionServiceBusQueueTrigger> logger,
    IOptions<Settings> settings)
{
    // Pattern: Commented out until Service Bus is provisioned and connection string is configured.
    //[Function(nameof(FunctionServiceBusQueueTrigger))]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueueName%", Connection = "ServiceBusQueue")] ServiceBusReceivedMessage message)
    {
        logger.LogInformation("ServiceBusQueueTrigger - Start MessageId: {MessageId} {Body}",
            message.MessageId, message.Body);

        // Pattern: Deserialize message and delegate to application service.
        // var payload = message.Body.ToObjectFromJson<TodoItemDto>();
        // await todoItemService.ProcessAsync(payload);
        await Task.CompletedTask;

        logger.LogInformation("ServiceBusQueueTrigger - Finish MessageId: {MessageId}", message.MessageId);
    }
}
