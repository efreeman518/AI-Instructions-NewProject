namespace Infrastructure.Repositories;

public class CategoryRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ICategoryRepositoryTrxn
{
    public async Task<Category?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await GetEntityAsync<Category>(
            true,
            filter: e => e.Id == id,
            splitQueryThresholdOptions: SplitQueryThresholdOptions.Default,
            cancellationToken: ct
        ).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    public void Create(ref Category entity)
    {
        DB.Set<Category>().Add(entity);
    }

    public async Task<bool> HasActiveItemsAsync(Guid categoryId, CancellationToken ct = default)
    {
        return await DB.Set<TodoItem>().AnyAsync(t => t.CategoryId == categoryId, ct);
    }
}
