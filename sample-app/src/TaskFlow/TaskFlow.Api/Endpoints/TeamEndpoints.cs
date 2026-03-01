namespace TaskFlow.Api.Endpoints;

public static class TeamEndpoints
{
    public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder group)
    {
        var teamGroup = group.MapGroup("/teams").WithTags("Teams");

        teamGroup.MapPost("/search", async (SearchRequest<TeamDto> request, ITeamService service, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(request, ct)));

        teamGroup.MapGet("/{id:guid}", async (Guid id, ITeamService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        teamGroup.MapPost("/", async (TeamDto dto, ITeamService service, CancellationToken ct) =>
            (await service.CreateAsync(dto, ct)).Match<IResult>(s => Results.Created($"/api/teams/{s.Id}", s), e => Results.BadRequest(e), () => Results.NotFound()));

        teamGroup.MapPut("/", async (TeamDto dto, ITeamService service, CancellationToken ct) =>
            (await service.UpdateAsync(dto, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        teamGroup.MapDelete("/{id:guid}", async (Guid id, ITeamService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).Match<IResult>(() => Results.NoContent(), e => Results.BadRequest(e)));

        // Members
        teamGroup.MapPost("/{teamId:guid}/members", async (Guid teamId, TeamMemberDto member, ITeamService service, CancellationToken ct) =>
            (await service.AddMemberAsync(teamId, member, ct)).Match<IResult>(s => Results.Created($"/api/teams/{teamId}/members", s), e => Results.BadRequest(e), () => Results.NotFound()));

        teamGroup.MapDelete("/{teamId:guid}/members/{memberId:guid}", async (Guid teamId, Guid memberId, ITeamService service, CancellationToken ct) =>
            (await service.RemoveMemberAsync(teamId, memberId, ct)).Match<IResult>(() => Results.NoContent(), e => Results.BadRequest(e)));

        return group;
    }
}
