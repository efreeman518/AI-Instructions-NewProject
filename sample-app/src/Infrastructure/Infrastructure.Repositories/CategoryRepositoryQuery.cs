namespace Infrastructure.Repositories;

public class CategoryRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ICategoryRepositoryQuery
{
    public async Task<PagedResponse<CategoryDto>> SearchAsync(SearchRequest<CategoryDto> request, CancellationToken ct = default)
    {
        var q = DB.Set<Category>().ComposeIQueryable(false);

        if (request.Sorts?.Any() ?? false)
            q = q.OrderBy(request.Sorts);
        else
            q = q.OrderBy(e => e.DisplayOrder).ThenBy(e => e.Name);

        (var data, var total) = await q.QueryPageProjectionAsync(
            e => e.ToDto(),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            includeTotal: true,
            splitQueryOptions: SplitQueryThresholdOptions.Default,
            cancellationToken: ct).ConfigureAwait(ConfigureAwaitOptions.None);

        return new PagedResponse<CategoryDto>
        {
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            Data = data,
            Total = total
        };
    }
}
