// ═══════════════════════════════════════════════════════════════
// Pattern: Global exception handler middleware — IFunctionsWorkerMiddleware.
// Wraps all function invocations — catches + logs unhandled exceptions.
// Registered via builder.UseMiddleware<GlobalExceptionHandler>() in Program.cs.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace TaskFlow.FunctionApp.Infrastructure;

/// <summary>
/// Pattern: IFunctionsWorkerMiddleware — exception handler.
/// Catches any exception from the function pipeline, logs it,
/// and optionally passes data via context.Items.
/// </summary>
public class GlobalExceptionHandler : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Pattern: Pre-function data — available to the function via context.Items.
        context.Items.Add("correlationId", Guid.NewGuid().ToString());

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            ILogger logger = context.GetLogger<GlobalExceptionHandler>();
            logger.LogError(ex, "GlobalExceptionHandler caught: {Error}", ex.Message);
            // Pattern: Do not re-throw — the runtime handles it from here.
            // Re-throwing would cause the function to be marked as failed (desired for retries).
            throw;
        }

        // Pattern: Post-function inspection — e.g., check if function set a custom item.
        if (context.Items.TryGetValue("functionitem", out object? value) && value is string message)
        {
            ILogger logger = context.GetLogger<GlobalExceptionHandler>();
            logger.LogInformation("From function: {Message}", message);
        }
    }
}
