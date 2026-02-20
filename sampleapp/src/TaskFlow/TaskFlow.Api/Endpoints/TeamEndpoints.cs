// ═══════════════════════════════════════════════════════════════
// Pattern: Endpoint — Team CRUD with TeamMember child collection.
// Demonstrates parent entity with child management.
// Route: v1/tenant/{tenantId}/teams
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Services;
using Application.Models;
using Microsoft.AspNetCore.Mvc;
using Package.Infrastructure.Common;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Pattern: Parent entity endpoint with child collection.
/// Teams contain TeamMembers managed through the Team DTO's child collection.
/// </summary>
public static class TeamEndpoints
{
    private static bool _problemDetailsIncludeStackTrace;

    public static IEndpointRouteBuilder MapTeamEndpoints(
        this IEndpointRouteBuilder group, bool problemDetailsIncludeStackTrace)
    {
        _problemDetailsIncludeStackTrace = problemDetailsIncludeStackTrace;

        group.MapPost("/search", Search)
            .Produces<PagedResponse<TeamDto>>(StatusCodes.Status200OK)
            .WithSummary("Search Teams with paging");

        group.MapGet("/lookup", Lookup)
            .Produces<StaticList<StaticItem<Guid, Guid?>>>(StatusCodes.Status200OK)
            .WithSummary("Lookup Teams for autocomplete");

        group.MapGet("/{id:guid}", GetById)
            .Produces<DefaultResponse<TeamDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a single Team with its members");

        group.MapPost("/", Create)
            .Produces<DefaultResponse<TeamDto>>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Create a new Team");

        group.MapPut("/{id:guid}", Update)
            .Produces<DefaultResponse<TeamDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a Team and its members");

        group.MapDelete("/{id:guid}", Delete)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithSummary("Delete a Team");

        return group;
    }

    private static async Task<IResult> Search(
        [FromServices] ITeamService service,
        [FromBody] SearchRequest<TeamSearchFilter> request)
    {
        var items = await service.SearchAsync(request);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> Lookup(
        [FromServices] ITeamService service,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search)
    {
        var items = await service.LookupAsync(tenantId, search);
        return TypedResults.Ok(items);
    }

    private static async Task<IResult> GetById(
        [FromServices] ITeamService service, Guid id)
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
        [FromServices] ITeamService service,
        [FromBody] DefaultRequest<TeamDto> request)
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
        [FromServices] ITeamService service,
        Guid id,
        [FromBody] DefaultRequest<TeamDto> request)
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
        [FromServices] ITeamService service, Guid id)
    {
        var result = await service.DeleteAsync(id);
        return result.Match<IResult>(
            () => TypedResults.NoContent(),
            errors => TypedResults.Problem(ProblemDetailsHelper.BuildProblemDetailsResponseMultiple(
                messages: errors, traceId: httpContext.TraceIdentifier,
                includeStackTrace: _problemDetailsIncludeStackTrace)));
    }
}
