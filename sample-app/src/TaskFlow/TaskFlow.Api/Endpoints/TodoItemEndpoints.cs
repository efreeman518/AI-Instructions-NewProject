namespace TaskFlow.Api.Endpoints;

public static class TodoItemEndpoints
{
    public static RouteGroupBuilder MapTodoItemEndpoints(this RouteGroupBuilder group)
    {
        var todoGroup = group.MapGroup("/todoitems").WithTags("TodoItems");

        todoGroup.MapPost("/search", async (SearchRequest<TodoItemSearchFilter> request, ITodoItemService service, CancellationToken ct) =>
        {
            var result = await service.SearchAsync(request, ct);
            return Results.Ok(result);
        });

        todoGroup.MapGet("/{id:guid}", async (Guid id, ITodoItemService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(id, ct);
            return result.Match<IResult>(
                dto => Results.Ok(dto),
                errors => Results.BadRequest(errors),
                () => Results.NotFound());
        });

        todoGroup.MapPost("/", async (TodoItemDto dto, ITodoItemService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(dto, ct);
            return result.Match<IResult>(
                created => Results.Created($"/api/todoitems/{created.Id}", created),
                errors => Results.BadRequest(errors),
                () => Results.NotFound());
        });

        todoGroup.MapPut("/", async (TodoItemDto dto, ITodoItemService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(dto, ct);
            return result.Match<IResult>(
                updated => Results.Ok(updated),
                errors => Results.BadRequest(errors),
                () => Results.NotFound());
        });

        todoGroup.MapDelete("/{id:guid}", async (Guid id, ITodoItemService service, CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(id, ct);
            return result.Match<IResult>(
                () => Results.NoContent(),
                errors => Results.BadRequest(errors));
        });

        // State transitions
        todoGroup.MapPost("/{id:guid}/start", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.StartAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/complete", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.CompleteAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/block", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.BlockAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/unblock", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.UnblockAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/cancel", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.CancelAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/archive", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.ArchiveAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/restore", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.RestoreAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        todoGroup.MapPost("/{id:guid}/reopen", async (Guid id, ITodoItemService service, CancellationToken ct) =>
            (await service.ReopenAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        // Assignment
        todoGroup.MapPost("/{id:guid}/assign", async (Guid id, Guid? assignedToId, ITodoItemService service, CancellationToken ct) =>
            (await service.AssignAsync(id, assignedToId, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        // Comments
        todoGroup.MapPost("/{id:guid}/comments", async (Guid id, CommentDto comment, ITodoItemService service, CancellationToken ct) =>
            (await service.AddCommentAsync(id, comment, ct)).Match<IResult>(s => Results.Created($"/api/todoitems/{id}/comments", s), e => Results.BadRequest(e), () => Results.NotFound()));

        return group;
    }
}
