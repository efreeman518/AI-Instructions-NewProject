// ═══════════════════════════════════════════════════════════════
// Pattern: Endpoint — Category CRUD with cache-on-write.
// Simpler than TodoItem — tenant-scoped, cacheable static data.
// Route: v1/tenant/{tenantId}/categories
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using Application.Models;
using Microsoft.AspNetCore.Mvc;
using EF.Common;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Pattern: Simpler entity endpoint — demonstrates cache-on-write for static data.
/// Categories are rarely-changing reference data cached in StaticData named cache.
/// </summary>
public static class CategoryEndpoints
{
    private static bool _problemDetailsIncludeStackTrace;

    public static IEndpointRouteBuilder MapCategoryEndpoints(
        this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        _problemDetailsIncludeStackTrace = problemDetailsIncludeStackTrace;

        group.MapPost("/search", Search)
            .Produces<PagedResponse<CategoryDto>>(StatusCodes.Status200OK)
            .WithSummary("Search Categories with paging");

        group.MapGet("/lookup", Lookup)
            .Produces<StaticList<StaticItem<Guid, Guid?>>>(StatusCodes.Status200OK)
            .WithSummary("Lookup Categories for autocomplete");

        group.MapGet("/{id:guid}", GetById)
            .Produces<DefaultResponse<CategoryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single Category");

        group.MapPost("/", Create)
            .Produces<DefaultResponse<CategoryDto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Create a new Category");

        group.MapPut("/{id:guid}", Update)
            .Produces<DefaultResponse<CategoryDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update an existing Category");

        group.MapDelete("/{id:guid}", Delete)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithSummary("Delete a Category");

        return group;
    }

    private static async Task<IResult> Search(
        [FromServices] ICategoryService service,
        [FromBody] SearchRequest<CategorySearchFilter> request)
    {
        var items = await service.SearchAsync(request);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> Lookup(
        [FromServices] ICategoryService service,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search)
    {
        var items = await service.LookupAsync(tenantId, search);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetById(
        [FromServices] ICategoryService service, Guid id)
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
        [FromServices] ICategoryService service,
        [FromBody] DefaultRequest<CategoryDto> request)
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
        [FromServices] ICategoryService service,
        Guid id,
        [FromBody] DefaultRequest<CategoryDto> request)
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
        [FromServices] ICategoryService service, Guid id)
    {
        var result = await service.DeleteAsync(id);
        return result.Match<IResult>(
            () => TypedResults.NoContent(),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }
}
