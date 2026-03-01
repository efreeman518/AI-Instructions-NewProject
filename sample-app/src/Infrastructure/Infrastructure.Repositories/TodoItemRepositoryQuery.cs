using EF.Domain;

namespace Infrastructure.Repositories;

public class TodoItemRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITodoItemRepositoryQuery
{
    public async Task<PagedResponse<TodoItemDto>> SearchAsync(SearchRequest<TodoItemSearchFilter> request, CancellationToken ct = default)
    {
        var q = DB.Set<TodoItem>().ComposeIQueryable(false);

        if (request.Sorts?.Any() ?? false)
        {
            q = q.OrderBy(request.Sorts);
        }
        else
        {
            q = q.OrderByDescending(e => e.Id);
        }

        var filter = request.Filter;
        if (filter is not null)
        {
            if (filter.Status.HasValue)
                q = q.Where(e => e.Status == filter.Status.Value);

            if (filter.CategoryId.HasValue)
                q = q.Where(e => e.CategoryId == filter.CategoryId.Value);

            if (filter.TeamId.HasValue)
                q = q.Where(e => e.TeamId == filter.TeamId.Value);

            if (filter.AssignedToId.HasValue)
                q = q.Where(e => e.AssignedToId == filter.AssignedToId.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm;
                q = term.Contains('*')
                    ? q.Where(e => e.Title.Contains(term.Replace("*", "")))
                    : q.Where(e => e.Title == term);
            }
        }

        (var data, var total) = await q.QueryPageProjectionAsync(
            e => e.ToDto(),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            includeTotal: true,
            splitQueryOptions: SplitQueryThresholdOptions.Default,
            cancellationToken: ct).ConfigureAwait(ConfigureAwaitOptions.None);

        return new PagedResponse<TodoItemDto>
        {
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            Data = data,
            Total = total
        };
    }
}
