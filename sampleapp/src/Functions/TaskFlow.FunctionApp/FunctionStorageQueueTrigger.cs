// ═══════════════════════════════════════════════════════════════
// Pattern: Storage Queue Trigger — lightweight message processing.
// If all five attempts fail, the runtime adds a message to {queuename}-poison.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Storage queue trigger — simpler alternative to Service Bus.
/// Uses Azure Storage Queues (local: Azurite, cloud: Azure Storage Account).
/// Poison messages go to {queuename}-poison after 5 failed attempts.
/// </summary>
public class FunctionStorageQueueTrigger(
    ILogger<FunctionStorageQueueTrigger> logger,
    IOptions<Settings> settings)
{
    [Function(nameof(FunctionStorageQueueTrigger))]
    public async Task Run(
        [QueueTrigger("%StorageQueueName%", Connection = "StorageQueue1")] string queueItem)
    {
        logger.LogInformation("StorageQueueTrigger - Start message: {QueueItem}", queueItem);

        // Pattern: Deserialize and delegate to application service.
        // var payload = JsonSerializer.Deserialize<TodoItemDto>(queueItem);
        // await todoItemService.ProcessAsync(payload);
        await Task.CompletedTask;

        logger.LogInformation("StorageQueueTrigger - Finish message: {QueueItem}", queueItem);
    }
}
