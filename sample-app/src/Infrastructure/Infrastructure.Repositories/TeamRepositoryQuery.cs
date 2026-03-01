using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class TeamRepositoryQuery(TaskFlowDbContextQuery dbContext)
    : RepositoryBase<TaskFlowDbContextQuery, string, Guid?>(dbContext), ITeamRepositoryQuery
{
    public async Task<PagedResponse<TeamDto>> SearchAsync(SearchRequest<TeamDto> request, CancellationToken ct = default)
    {
        var q = DB.Set<Team>().ComposeIQueryable(false);

        if (request.Sorts?.Any() ?? false)
            q = q.OrderBy(request.Sorts);
        else
            q = q.OrderBy(e => e.Name);

        var includesList = new List<Expression<Func<IQueryable<Team>, IIncludableQueryable<Team, object?>>>>
        {
            q => q.Include(t => t.Members)
        };

        (var data, var total) = await q.QueryPageProjectionAsync(
            e => e.ToDto(),
            pageSize: request.PageSize,
            pageIndex: request.PageIndex,
            includeTotal: true,
            splitQueryOptions: SplitQueryThresholdOptions.Default,
            includes: [.. includesList],
            cancellationToken: ct).ConfigureAwait(ConfigureAwaitOptions.None);

        return new PagedResponse<TeamDto>
        {
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            Data = data,
            Total = total
        };
    }
}
