// ═══════════════════════════════════════════════════════════════
// Pattern: Global logger middleware — IFunctionsWorkerMiddleware.
// Logs trigger start/finish for every function invocation.
// Adds structured logging with function name and timestamps.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace TaskFlow.FunctionApp.Infrastructure;

/// <summary>
/// Pattern: IFunctionsWorkerMiddleware — structured logging per invocation.
/// Logs function name, binding data, and timing for every trigger.
/// Registered after GlobalExceptionHandler in the middleware pipeline.
/// </summary>
public class GlobalLogger(ILogger<GlobalLogger> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;
        logger.LogInformation("Function [{FunctionName}]: triggered {Time}",
            functionName, TimeProvider.System.GetUtcNow());

        // Pattern: Log binding data for debugging — shows trigger payload summary.
        var request = context.BindingContext;
        if (request?.BindingData?.Values != null)
        {
            logger.LogInformation("Function [{FunctionName}]: Request data {Data}",
                functionName, string.Join(";", request.BindingData.Values));
        }

        await next(context);

        logger.LogInformation("Function [{FunctionName}]: finished {Time}",
            functionName, TimeProvider.System.GetUtcNow());
    }
}
