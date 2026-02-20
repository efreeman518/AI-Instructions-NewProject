// ═══════════════════════════════════════════════════════════════
// Pattern: Endpoint — TodoItem full CRUD + Search + Lookup.
// Static class with static methods. Uses Result.Match() for HTTP mapping.
// TypedResults for type-safe responses. ProblemDetails for all errors.
// Route: v1/tenant/{tenantId}/todoitems
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using Application.Models;
using Microsoft.AspNetCore.Mvc;
using Package.Infrastructure.Common;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Pattern: Static endpoint class — one per entity/aggregate.
/// MapTodoItemEndpoints is called from WebApplicationBuilderExtensions.SetupApiVersionedEndpoints.
/// Each method maps a service Result to HTTP TypedResults using Result.Match().
/// </summary>
public static class TodoItemEndpoints
{
    private static bool _problemDetailsIncludeStackTrace;

    public static IEndpointRouteBuilder MapTodoItemEndpoints(
        this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        _problemDetailsIncludeStackTrace = problemDetailsIncludeStackTrace;

        // Pattern: Search uses POST — complex filter payload in body, not query params.
        group.MapPost("/search", Search)
            .Produces<PagedResponse<TodoItemDto>>(StatusCodes.Status200OK)
            .WithSummary("Search TodoItems with paging, filters, and sorts");

        // Pattern: Lookup returns lightweight items for autocomplete/dropdowns.
        group.MapGet("/lookup", Lookup)
            .Produces<StaticList<StaticItem<Guid, Guid?>>>(StatusCodes.Status200OK)
            .WithSummary("Lookup TodoItems for autocomplete");

        group.MapGet("/{id:guid}", GetById)
            .Produces<DefaultResponse<TodoItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single TodoItem");

        group.MapPost("/", Create)
            .Produces<DefaultResponse<TodoItemDto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Create a new TodoItem");

        group.MapPut("/{id:guid}", Update)
            .Produces<DefaultResponse<TodoItemDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing TodoItem");

        group.MapDelete("/{id:guid}", Delete)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithSummary("Delete a TodoItem");

        return group;
    }

    // ═══════════════════════════════════════════════════════════════
    // Search — POST /search
    // Pattern: SearchRequest<TFilter> → PagedResponse<TDto>
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IResult> Search(
        [FromServices] ITodoItemService service,
        [FromBody] SearchRequest<TodoItemSearchFilter> request)
    {
        var items = await service.SearchAsync(request);
        return TypedResults.Ok(items);
    }

    // ═══════════════════════════════════════════════════════════════
    // Lookup — GET /lookup?tenantId=&search=
    // Pattern: Returns StaticList for autocomplete/dropdowns.
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IResult> Lookup(
        [FromServices] ITodoItemService service,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search)
    {
        var items = await service.LookupAsync(tenantId, search);
        return TypedResults.Ok(items);
    }

    // ═══════════════════════════════════════════════════════════════
    // GetById — GET /{id:guid}
    // Pattern: Result.Match() maps 3 outcomes: success, errors, not-found.
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IResult> GetById(
        [FromServices] ITodoItemService service, Guid id)
    {
        var result = await service.GetAsync(id);
        return result.Match<IResult>(
            response => TypedResults.Ok(response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, statusCodeOverride: StatusCodes.Status400BadRequest)),
            () => TypedResults.NotFound(id));
    }

    // ═══════════════════════════════════════════════════════════════
    // Create — POST /
    // Pattern: Returns 201 Created with Location header.
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IResult> Create(
        HttpContext httpContext,
        [FromServices] ITodoItemService service,
        [FromBody] DefaultRequest<TodoItemDto> request)
    {
        var result = await service.CreateAsync(request);
        return result.Match<IResult>(
            response => TypedResults.Created(httpContext.Request.Path, response),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Update — PUT /{id:guid}
    // Pattern: URL/body ID mismatch check before delegating to service.
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IResult> Update(
        HttpContext httpContext,
        [FromServices] ITodoItemService service,
        Guid id,
        [FromBody] DefaultRequest<TodoItemDto> request)
    {
        // Pattern: Validate URL ID matches body ID — prevents accidental cross-entity updates.
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

    // ═══════════════════════════════════════════════════════════════
    // Delete — DELETE /{id:guid}
    // Pattern: Returns 204 NoContent on success.
    // ═══════════════════════════════════════════════════════════════

    private static async Task<IResult> Delete(
        HttpContext httpContext,
        [FromServices] ITodoItemService service, Guid id)
    {
        var result = await service.DeleteAsync(id);
        return result.Match<IResult>(
            () => TypedResults.NoContent(),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }
}
