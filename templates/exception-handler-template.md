# Exception Handler Template

| | |
|---|---|
| **File** | `{Host}.Api/ExceptionHandlers/DefaultExceptionHandler.cs` |
| **Depends on** | [api.md](../skills/api.md) |
| **Referenced by** | [api.md](../skills/api.md), [sampleapp-patterns.md](../sampleapp-patterns.md) |
| **Sampleapp** | `sample-app/src/TaskFlow/TaskFlow.Api/ExceptionHandlers/DefaultExceptionHandler.cs` |

## Purpose

Global `IExceptionHandler` that maps unexpected/infrastructure exceptions to `ProblemDetails` responses. This is the **safety net** — not a control-flow mechanism. All expected business outcomes flow through `Result<T>`/`DomainResult<T>`.

## Template

```csharp
// File: {Host}.Api/ExceptionHandlers/DefaultExceptionHandler.cs
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace {Host}.Api.ExceptionHandlers;

internal class DefaultExceptionHandler(
    ILogger<DefaultExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            UnauthorizedAccessException
                => (StatusCodes.Status403Forbidden, "Forbidden"),
            OperationCanceledException
                => (499, "Client closed request"),   // 499 = nginx convention
            ArgumentException or FormatException
                => (StatusCodes.Status400BadRequest, "Bad request"),
            _
                => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        logger.LogError(exception, "Unhandled exception: {ExceptionType} — {Message}",
            exception.GetType().Name, exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = environment.IsDevelopment() || environment.IsStaging()
                ? exception.ToString()
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;  // Exception handled — stop pipeline propagation
    }
}
```

## Registration

Register in `RegisterApiServices.cs`:

```csharp
services.AddExceptionHandler<DefaultExceptionHandler>();
services.AddProblemDetails();
```

Add to pipeline in `WebApplicationBuilderExtensions.cs` (before routing):

```csharp
app.UseExceptionHandler();
```

## Exception-to-Status Mapping

| Exception Type | HTTP Status | Title |
|---|---|---|
| `DbUpdateConcurrencyException` | 409 Conflict | Concurrency conflict |
| `UnauthorizedAccessException` | 403 Forbidden | Forbidden |
| `OperationCanceledException` | 499 | Client closed request |
| `ArgumentException` / `FormatException` | 400 Bad Request | Bad request |
| All others | 500 Internal Server Error | Internal server error |

## Rules

- **Safety net only** — business validation errors must use `Result<T>` / `DomainResult<T>`, never exceptions.
- Stack traces: include full `exception.ToString()` in Development/Staging; show only `exception.Message` in Production.
- Always log at `Error` level with structured placeholders.
- Return `true` to indicate the exception is handled and prevent further pipeline propagation.
- Add new exception mappings as needed (e.g., `HttpRequestException` → 502 for downstream failures).

## Verification Checklist

- [ ] Registered via `AddExceptionHandler<DefaultExceptionHandler>()` in `RegisterApiServices.cs`
- [ ] `UseExceptionHandler()` called in pipeline before routing
- [ ] `AddProblemDetails()` registered in services
- [ ] Stack traces gated by environment (not exposed in Production)
- [ ] All mapped exceptions return correct HTTP status codes
- [ ] Logging uses structured placeholders, not string interpolation
- [ ] No business logic errors handled here — those use `Result<T>` pattern
