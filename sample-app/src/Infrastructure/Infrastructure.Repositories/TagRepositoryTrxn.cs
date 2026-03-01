namespace Infrastructure.Repositories;

public class TagRepositoryTrxn(TaskFlowDbContextTrxn dbContext)
    : RepositoryBase<TaskFlowDbContextTrxn, string, Guid?>(dbContext), ITagRepositoryTrxn
{
    public async Task<Tag?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await GetEntityAsync<Tag>(
            true,
            filter: e => e.Id == id,
            splitQueryThresholdOptions: SplitQueryThresholdOptions.Default,
            cancellationToken: ct
        ).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    public void Create(ref Tag entity)
    {
        DB.Set<Tag>().Add(entity);
    }
}
