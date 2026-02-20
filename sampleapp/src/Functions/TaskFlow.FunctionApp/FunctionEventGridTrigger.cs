// ═══════════════════════════════════════════════════════════════
// Pattern: Event Grid Trigger — handles Azure Event Grid events.
// Can handle blob events, custom topic events, or domain events.
// Local debug: use VS Dev Tunnels or ngrok for webhook URL.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Event Grid trigger — blob event subscription.
/// In Azure, filter the subject when creating the subscription:
///   - begins with /blobServices/default/containers/{containername}
///   - ends with .txt (or desired extension)
/// Local debug: VS Dev Tunnels or ngrok → create subscription with webhook URL:
///   https://{tunnelurl}/runtime/webhooks/EventGrid?functionName=FunctionEventGridTrigger
/// </summary>
public class FunctionEventGridTrigger(
    ILogger<FunctionEventGridTrigger> logger,
    IOptions<Settings> settings)
{
    [Function(nameof(FunctionEventGridTrigger))]
    public async Task Run([EventGridTrigger] EventGridEvent inputEvent)
    {
        var fileName = Path.GetFileName(inputEvent.Subject);
        logger.LogInformation("EventGridTrigger - Start {FileName} {Event}",
            fileName, JsonSerializer.Serialize(inputEvent));

        _ = inputEvent.Data?.ToString();

        // Pattern: Delegate to application service based on event type.
        // switch (inputEvent.EventType) { ... }
        await Task.CompletedTask;

        logger.LogInformation("EventGridTrigger - Finish {FileName}", fileName);
    }
}
