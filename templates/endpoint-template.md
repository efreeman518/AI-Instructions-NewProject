# Endpoint Template

| | |
|---|---|
| **File** | `{Host}.Api/Endpoints/{Entity}Endpoints.cs` |
| **Depends on** | [service-template](service-template.md), [dto-template](dto-template.md) |
| **Referenced by** | [api.md](../skills/api.md), [test-template](test-template.md) |

## File: {Host}.Api/Endpoints/{Entity}Endpoints.cs

```csharp
using Microsoft.AspNetCore.Mvc;

namespace {Host}.Api.Endpoints;

public static class {Entity}Endpoints
{
    private static bool _problemDetailsIncludeStackTrace;

    public static IEndpointRouteBuilder Map{Entity}Endpoints(this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        _problemDetailsIncludeStackTrace = problemDetailsIncludeStackTrace;

        group.MapPost("/search", Search)
            .Produces<PagedResponse<{Entity}Dto>>(StatusCodes.Status200OK)
            .WithSummary("Search {Entities} with paging, filters, and sorts");

        group.MapGet("/lookup", Lookup)
            .Produces<StaticList<StaticItem<Guid, Guid?>>>(StatusCodes.Status200OK)
            .WithSummary("Lookup list for autocomplete");

        group.MapGet("/{id:guid}", GetById)
            .Produces<DefaultResponse<{Entity}Dto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single {Entity}");

        group.MapPost("/", Create)
            .Produces<DefaultResponse<{Entity}Dto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Create a new {Entity}");

        group.MapPut("/{id:guid}", Update)
            .Produces<DefaultResponse<{Entity}Dto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing {Entity}");

        group.MapDelete("/{id:guid}", Delete)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithSummary("Delete a {Entity}");

        return group;
    }

    private static async Task<IResult> Search(
        [FromServices] I{Entity}Service service,
        [FromBody] SearchRequest<{Entity}SearchFilter> request)
    {
        var items = await service.SearchAsync(request);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> Lookup(
        [FromServices] I{Entity}Service service,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search)
    {
        var items = await service.LookupAsync(tenantId, search);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetById(
        [FromServices] I{Entity}Service service, Guid id)
    {
        var result = await service.GetAsync(id);
        return result.Match<IResult>(
            response => TypedResults.Ok(response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, statusCodeOverride: StatusCodes.Status400BadRequest)),
            () => TypedResults.NotFound(id));
    }

    private static async Task<IResult> Create(
        HttpContext httpContext,
        [FromServices] I{Entity}Service service,
        [FromBody] DefaultRequest<{Entity}Dto> request)
    {
        var result = await service.CreateAsync(request);
        return result.Match<IResult>(
            response => TypedResults.Created(httpContext.Request.Path, response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }

    private static async Task<IResult> Update(
        HttpContext httpContext,
        [FromServices] I{Entity}Service service,
        Guid id,
        [FromBody] DefaultRequest<{Entity}Dto> request)
    {
        if (request.Item.Id != null && request.Item.Id != id)
            return TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponse(
                statusCodeOverride: StatusCodes.Status400BadRequest,
                message: $"URL/body ID mismatch: {id} <> {request.Item.Id}"));

        var result = await service.UpdateAsync(request);
        return result.Match(
            response => response.Item is null ? Results.NotFound(id) : TypedResults.Ok(response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }

    private static async Task<IResult> Delete(
        HttpContext httpContext,
        [FromServices] I{Entity}Service service, Guid id)
    {
        var result = await service.DeleteAsync(id);
        return result.Match<IResult>(
            () => TypedResults.NoContent(),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }
}
```

## Registration in Pipeline

In `WebApplicationBuilderExtensions.cs`, add to `SetupApiVersionedEndpoints`:

```csharp
app.MapGroup("v{apiVersion:apiVersion}/tenant/{tenantId}/{entity}")
    .WithApiVersionSet(apiVersionSet)
    .RequireAuthorization("TenantMatch")
    .Map{Entity}Endpoints(includeErrorDetails);
```
