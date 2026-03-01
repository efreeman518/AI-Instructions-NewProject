using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class TodoItemRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITodoItemRepositoryTrxn
{
    public async Task<TodoItem?> GetAsync(Guid id, bool includeChildren = false, CancellationToken ct = default)
    {
        var includesList = new List<Expression<Func<IQueryable<TodoItem>, IIncludableQueryable<TodoItem, object?>>>>();

        if (includeChildren)
        {
            includesList.Add(q => q.Include(e => e.Comments));
            includesList.Add(q => q.Include(e => e.Attachments));
            includesList.Add(q => q.Include(e => e.Reminders));
            includesList.Add(q => q.Include(e => e.History));
        }

        return await GetEntityAsync(
            true,
            filter: e => e.Id == id,
            splitQueryThresholdOptions: SplitQueryThresholdOptions.Default,
            includes: [.. includesList],
            cancellationToken: ct
        ).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    public void Create(ref TodoItem entity)
    {
        DB.Set<TodoItem>().Add(entity);
    }
}
