// ═══════════════════════════════════════════════════════════════
// Pattern: Endpoint — Tag CRUD (non-tenant, global entity).
// Tags are shared across all tenants — no tenantId in route.
// Route: v1/tags
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using Application.Models;
using Microsoft.AspNetCore.Mvc;
using EF.Common;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Pattern: Non-tenant entity endpoint — no tenantId in route path.
/// Tags are global reference data shared across all tenants.
/// </summary>
public static class TagEndpoints
{
    private static bool _problemDetailsIncludeStackTrace;

    public static IEndpointRouteBuilder MapTagEndpoints(
        this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        _problemDetailsIncludeStackTrace = problemDetailsIncludeStackTrace;

        group.MapPost("/search", Search)
            .Produces<PagedResponse<TagDto>>(StatusCodes.Status200OK)
            .WithSummary("Search Tags with paging");

        group.MapGet("/lookup", Lookup)
            .Produces<StaticList<StaticItem<Guid, Guid?>>>(StatusCodes.Status200OK)
            .WithSummary("Lookup Tags for autocomplete");

        group.MapGet("/{id:guid}", GetById)
            .Produces<DefaultResponse<TagDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single Tag");

        group.MapPost("/", Create)
            .Produces<DefaultResponse<TagDto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Create a new Tag");

        group.MapPut("/{id:guid}", Update)
            .Produces<DefaultResponse<TagDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing Tag");

        group.MapDelete("/{id:guid}", Delete)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithSummary("Delete a Tag");

        return group;
    }

    private static async Task<IResult> Search(
        [FromServices] ITagService service,
        [FromBody] SearchRequest<TagSearchFilter> request)
    {
        var items = await service.SearchAsync(request);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> Lookup(
        [FromServices] ITagService service,
        [FromQuery] string? search)
    {
        // Pattern: Tags are non-tenant — no tenantId parameter.
        var items = await service.LookupAsync(null, search);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetById(
        [FromServices] ITagService service, Guid id)
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
        [FromServices] ITagService service,
        [FromBody] DefaultRequest<TagDto> request)
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
        [FromServices] ITagService service,
        Guid id,
        [FromBody] DefaultRequest<TagDto> request)
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
        [FromServices] ITagService service, Guid id)
    {
        var result = await service.DeleteAsync(id);
        return result.Match<IResult>(
            () => TypedResults.NoContent(),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }
}
