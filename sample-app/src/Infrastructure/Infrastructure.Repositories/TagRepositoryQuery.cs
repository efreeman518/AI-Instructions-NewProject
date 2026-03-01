namespace Infrastructure.Repositories;

public class TagRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITagRepositoryQuery
{
    public async Task<PagedResponse<TagDto>> SearchAsync(SearchRequest<TagDto> request, CancellationToken ct = default)
    {
        var q = DB.Set<Tag>().ComposeIQueryable(false);

        if (request.Sorts?.Any() ?? false)
            q = q.OrderBy(request.Sorts);
        else
            q = q.OrderBy(e => e.Name);

        (var data, var total) = await q.QueryPageProjectionAsync(
            e => e.ToDto(),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            includeTotal: true,
            splitQueryOptions: SplitQueryThresholdOptions.Default,
            cancellationToken: ct).ConfigureAwait(ConfigureAwaitOptions.None);

        return new PagedResponse<TagDto>
        {
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            Data = data,
            Total = total
        };
    }
}
