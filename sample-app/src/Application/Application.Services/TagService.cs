namespace Application.Services;

internal class TagService(
    ILogger<TagService> logger,
    IRequestContext<string, Guid?> requestContext,
    ITagRepositoryTrxn repoTrxn,
    ITagRepositoryQuery repoQuery) : ITagService
{
    private Guid? CallerTenantId => requestContext.TenantId;

    public async Task<PagedResponse<TagDto>> SearchAsync(SearchRequest<TagDto> request, CancellationToken ct = default)
    {
        logger.LogDebug("Searching tags");
        return await repoQuery.SearchAsync(request, ct);
    }

    public async Task<Result<TagDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, ct);
        if (entity == null) return Result<TagDto>.None();
        return Result<TagDto>.Success(entity.ToDto());
    }

    public async Task<Result<TagDto>> CreateAsync(TagDto dto, CancellationToken ct = default)
    {
        var entityResult = Tag.Create(dto.Name, dto.Description);
        if (entityResult.IsFailure) return Result<TagDto>.Failure(entityResult.ErrorMessage);

        var entity = entityResult.Value!;
        repoTrxn.Create(ref entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<TagDto>.Success(entity.ToDto());
    }

    public async Task<Result<TagDto>> UpdateAsync(TagDto dto, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(dto.Id, ct);
        if (entity == null) return Result<TagDto>.None();

        var updateResult = entity.Update(dto.Name, dto.Description);
        if (updateResult.IsFailure) return Result<TagDto>.Failure(updateResult.ErrorMessage);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<TagDto>.Success(entity.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, ct);
        if (entity == null) return Result.Success();
        repoTrxn.Delete(entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result.Success();
    }
}
