// ═══════════════════════════════════════════════════════════════
// Pattern: HTTP Trigger — sample API-style function.
// AuthorizationLevel.Function — requires x-functions-key header in Azure.
// Uses primary constructor for DI injection.
// ═══════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: HTTP trigger function — used for API-style endpoints.
/// Rule: Use nameof(ClassName) for [Function] attribute — prevents string drift.
/// Rule: AuthorizationLevel.Function for business endpoints; Anonymous only for health.
/// local debug: http://localhost:7071/api/FunctionHttpTrigger
/// Azure: include the function key in x-functions-key HTTP request header.
/// </summary>
public class FunctionHttpTrigger(
    ILogger<FunctionHttpTrigger> logger,
    IConfiguration configuration,
    IOptions<Settings> settings)
{
    [Function(nameof(FunctionHttpTrigger))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req)
    {
        logger.LogInformation("HttpTrigger - Start url: {Url}", req.Url);
        _ = await new StreamReader(req.Body).ReadToEndAsync();

        // Pattern: Delegate to application service via Bootstrapper-registered DI.
        // var result = await someService.DoWorkAsync();

        logger.LogInformation("HttpTrigger - Finish url: {Url}", req.Url);
        return new OkObjectResult($"HttpTrigger completed {DateTime.UtcNow}");
    }
}
