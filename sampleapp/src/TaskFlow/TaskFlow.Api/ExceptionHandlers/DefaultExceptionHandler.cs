// ═══════════════════════════════════════════════════════════════
// Pattern: Global exception handler — IExceptionHandler (.NET 8+).
// Maps known exception types to appropriate ProblemDetails responses.
// Last-resort handler — catches anything the middleware pipeline missed.
// ═══════════════════════════════════════════════════════════════

using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using EF.Common;

namespace TaskFlow.Api.ExceptionHandlers;

/// <summary>
/// Pattern: IExceptionHandler implementation registered via AddExceptionHandler.
/// Maps exception types to HTTP status codes with ProblemDetails payloads.
/// Logs the exception and returns a structured error response.
/// </summary>
public class DefaultExceptionHandler(
    ILogger<DefaultExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapException(exception);

        logger.LogError(exception,
            "Unhandled exception [{ExceptionType}]: {Message} — returning {StatusCode}",
            exception.GetType().Name, exception.Message, statusCode);

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = ProblemDetailsHelper.BuildProblemDetailsResponse(
            statusCodeOverride: (int)statusCode,
            message: environment.IsDevelopment() ? exception.Message : title,
            traceId: httpContext.TraceIdentifier,
            includeStackTrace: environment.IsDevelopment());

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// Pattern: Map exception types to HTTP status codes.
    /// Add domain-specific mappings as the project grows.
    /// </summary>
    private static (HttpStatusCode StatusCode, string Title) MapException(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "A required argument was not provided."),
            ArgumentException => (HttpStatusCode.BadRequest, "An argument was invalid."),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            InvalidOperationException => (HttpStatusCode.Conflict, "The operation is not valid for the current state."),
            NotImplementedException => (HttpStatusCode.NotImplemented, "This feature is not yet implemented."),
            TimeoutException => (HttpStatusCode.GatewayTimeout, "The operation timed out."),
            OperationCanceledException => (HttpStatusCode.BadRequest, "The operation was cancelled."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };
    }
}
