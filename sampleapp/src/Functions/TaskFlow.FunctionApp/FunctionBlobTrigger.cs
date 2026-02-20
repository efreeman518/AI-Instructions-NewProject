// ═══════════════════════════════════════════════════════════════
// Pattern: Blob Trigger — fires when a blob is created/updated.
// Connection string resolved from config via "StorageBlob1" key.
// If all 5 retries fail, Azure adds a message to webjobs-blobtrigger-poison queue.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Blob trigger — event-driven file processing.
/// Rule: Use %SettingName% for container name from config.
/// Rule: fileName is extracted from the blob path by the Functions runtime.
/// If all 5 retries fail, Azure Functions adds a message to webjobs-blobtrigger-poison queue.
/// </summary>
public class FunctionBlobTrigger(
    ILogger<FunctionBlobTrigger> logger,
    IOptions<Settings> settings)
{
    [Function(nameof(FunctionBlobTrigger))]
    public async Task Run(
        [BlobTrigger("%BlobContainer%/{fileName}", Connection = "StorageBlob1")] string fileContent,
        string fileName)
    {
        logger.LogInformation("BlobTrigger - Start {FileName}", fileName);

        // Pattern: Process blob content — e.g., parse CSV, import data, generate thumbnail.
        // var result = await attachmentService.ProcessUploadAsync(fileName, fileContent);
        await Task.CompletedTask;

        logger.LogInformation("BlobTrigger - Finish {FileName}", fileName);
    }
}
