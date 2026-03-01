namespace Application.Services;

internal class CategoryService(
    ILogger<CategoryService> logger,
    IRequestContext<string, Guid?> requestContext,
    ICategoryRepositoryTrxn repoTrxn,
    ICategoryRepositoryQuery repoQuery) : ICategoryService
{
    public async Task<PagedResponse<CategoryDto>> SearchAsync(SearchRequest<CategoryDto> request, CancellationToken ct = default)
    {
        logger.LogDebug("Searching categories");
        return await repoQuery.SearchAsync(request, ct);
    }

    public async Task<Result<CategoryDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(id, ct);
        if (entity == null) return Result<CategoryDto>.None();
        return Result<CategoryDto>.Success(entity.ToDto());
    }

    public async Task<Result<CategoryDto>> CreateAsync(CategoryDto dto, CancellationToken ct = default)
    {
        var tenantId = dto.TenantId != Guid.Empty ? dto.TenantId : requestContext.TenantId ?? Guid.Empty;
        var entityResult = Category.Create(tenantId, dto.Name, dto.Description, dto.ColorHex, dto.DisplayOrder);
        if (entityResult.IsFailure) return Result<CategoryDto>.Failure(entityResult.ErrorMessage);

        var entity = entityResult.Value!;
        repoTrxn.Create(ref entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<CategoryDto>.Success(entity.ToDto());
    }

    public async Task<Result<CategoryDto>> UpdateAsync(CategoryDto dto, CancellationToken ct = default)
    {
        var entity = await repoTrxn.GetAsync(dto.Id, ct);
        if (entity == null) return Result<CategoryDto>.None();

        var updateResult = entity.Update(dto.Name, dto.Description, dto.ColorHex, dto.DisplayOrder, dto.IsActive);
        if (updateResult.IsFailure) return Result<CategoryDto>.Failure(updateResult.ErrorMessage);

        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result<CategoryDto>.Success(entity.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var hasActiveItems = await repoTrxn.HasActiveItemsAsync(id, ct);
        if (hasActiveItems) return Result.Failure("Cannot delete category with active items.");

        var entity = await repoTrxn.GetAsync(id, ct);
        if (entity == null) return Result.Success();
        repoTrxn.Delete(entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        return Result.Success();
    }
}
