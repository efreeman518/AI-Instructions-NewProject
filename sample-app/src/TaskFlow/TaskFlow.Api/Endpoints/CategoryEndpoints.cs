namespace TaskFlow.Api.Endpoints;

public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        var catGroup = group.MapGroup("/categories").WithTags("Categories");

        catGroup.MapPost("/search", async (SearchRequest<CategoryDto> request, ICategoryService service, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(request, ct)));

        catGroup.MapGet("/{id:guid}", async (Guid id, ICategoryService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        catGroup.MapPost("/", async (CategoryDto dto, ICategoryService service, CancellationToken ct) =>
            (await service.CreateAsync(dto, ct)).Match<IResult>(s => Results.Created($"/api/categories/{s.Id}", s), e => Results.BadRequest(e), () => Results.NotFound()));

        catGroup.MapPut("/", async (CategoryDto dto, ICategoryService service, CancellationToken ct) =>
            (await service.UpdateAsync(dto, ct)).Match<IResult>(s => Results.Ok(s), e => Results.BadRequest(e), () => Results.NotFound()));

        catGroup.MapDelete("/{id:guid}", async (Guid id, ICategoryService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).Match<IResult>(() => Results.NoContent(), e => Results.BadRequest(e)));

        return group;
    }
}
