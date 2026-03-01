namespace TaskFlow.Api.Endpoints;

public static class TagEndpoints
{
    public static RouteGroupBuilder MapTagEndpoints(this RouteGroupBuilder group)
    {
        var tagGroup = group.MapGroup("/tags").WithTags("Tags");

        tagGroup.MapPost("/search", async (SearchRequest<TagDto> request, ITagService service, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(request, ct)));

        tagGroup.MapGet("/{id:guid}", async (Guid id, ITagService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        tagGroup.MapPost("/", async (TagDto dto, ITagService service, CancellationToken ct) =>
            (await service.CreateAsync(dto, ct)).Match<IResult>(s => Results.Created($"/api/tags/{s.Id}", s), e => Results.BadRequest(e), () => Results.NotFound()));

        tagGroup.MapPut("/", async (TagDto dto, ITagService service, CancellationToken ct) =>
            (await service.UpdateAsync(dto, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        tagGroup.MapDelete("/{id:guid}", async (Guid id, ITagService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).Match<IResult>(() => Results.NoContent(), e => Results.BadRequest(e)));

        return group;
    }
}
