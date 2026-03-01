using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class TeamRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITeamRepositoryTrxn
{
    public async Task<Team?> GetAsync(Guid id, bool includeMembers = false, CancellationToken ct = default)
    {
        var includesList = new List<Expression<Func<IQueryable<Team>, IIncludableQueryable<Team, object?>>>>();

        if (includeMembers)
        {
            includesList.Add(q => q.Include(t => t.Members));
        }

        return await GetEntityAsync(
            true,
            filter: e => e.Id == id,
            splitQueryThresholdOptions: SplitQueryThresholdOptions.Default,
            includes: [.. includesList],
            cancellationToken: ct
        ).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    public void Create(ref Team entity)
    {
        DB.Set<Team>().Add(entity);
    }
}
