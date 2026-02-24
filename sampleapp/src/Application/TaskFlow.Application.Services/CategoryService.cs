// ═══════════════════════════════════════════════════════════════
// Pattern: Simple service — Category with cache-on-write for static data.
// Demonstrates the caching pattern for lookup/reference data.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using EF.Common;
using EF.Domain;
using EF.Domain.Contracts;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.Category;
using Domain.Model.Rules;
using Domain.Shared;
using ZiggyCreatures.Caching.Fusion;

namespace Application.Services;

internal class CategoryService(
    ILogger<CategoryService> logger,
    IRequestContext<string, Guid?> requestContext,
    ICategoryRepositoryQuery repoQuery,
    ICategoryUpdater updater,
    ITodoItemRepositoryQuery todoItemRepo,
    IFusionCacheProvider fusionCacheProvider) : ICategoryService
{
    private readonly IFusionCache _cache = fusionCacheProvider.GetCache(Constants.CacheNames.StaticData);
    private Guid? CallerTenantId => requestContext.TenantId;
    private bool IsGlobalAdmin => requestContext.Roles.Contains(Constants.Roles.GlobalAdmin);

    public async Task<Result<PagedResponse<CategoryDto>>> SearchAsync(
        CategorySearchFilter filter, CancellationToken ct = default)
    {
        if (!IsGlobalAdmin) filter.TenantId = CallerTenantId;
        var result = await repoQuery.QueryPageProjectionAsync(filter, ct);
        return Result<PagedResponse<CategoryDto>>.Success(result);
    }

    public async Task<Result<CategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repoQuery.QueryByIdProjectionAsync(id, ct);
        if (dto is null) return Result<CategoryDto>.NotFound();
        if (!IsGlobalAdmin && dto.TenantId != CallerTenantId)
            return Result<CategoryDto>.Forbidden("Access denied.");
        return Result<CategoryDto>.Success(dto);
    }

    public async Task<Result<CategoryDto>> CreateAsync(CategoryDto dto, CancellationToken ct = default)
    {
        if (!IsGlobalAdmin) dto.TenantId = CallerTenantId ?? dto.TenantId;

        var result = await updater.CreateAsync(dto, ct);
        if (result.IsSuccess)
        {
            // Pattern: Cache-on-write — invalidate the tenant's category list after mutation.
            await _cache.RemoveAsync($"Categories:{dto.TenantId}", token: ct);
        }
        return result;
    }

    public async Task<Result<CategoryDto>> UpdateAsync(CategoryDto dto, CancellationToken ct = default)
    {
        var result = await updater.UpdateAsync(dto, ct);
        if (result.IsSuccess)
            await _cache.RemoveAsync($"Categories:{dto.TenantId}", token: ct);
        return result;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Pattern: Cross-entity rule — check for active items before deleting.
        var hasActiveItems = await todoItemRepo.HasActiveItemsInCategoryAsync(id, ct);
        var rule = new CategoryDeletionRule(hasActiveItems);
        var ruleResult = rule.Evaluate(new Domain.Model.Entities.Category());
        if (!ruleResult.IsSuccess)
            return Result.Failure(ruleResult.Errors);

        var existing = await repoQuery.QueryByIdProjectionAsync(id, ct);
        if (existing is null) return Result.Success(); // idempotent

        var result = await updater.DeleteAsync(id, ct);
        if (result.IsSuccess)
            await _cache.RemoveAsync($"Categories:{existing.TenantId}", token: ct);
        return result;
    }

    /// <summary>
    /// Pattern: Static data with cache — categories rarely change, so cache aggressively.
    /// Uses GetOrSetAsync with a longer TTL for reference data.
    /// </summary>
    public async Task<Result<IReadOnlyList<CategoryDto>>> GetAllForTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var categories = await _cache.GetOrSetAsync(
            $"Categories:{tenantId}",
            async (ctx, cancellation) => await repoQuery.GetAllForTenantAsync(tenantId, cancellation),
            token: ct);

        return Result<IReadOnlyList<CategoryDto>>.Success(categories ?? []);
    }
}
