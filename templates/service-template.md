# Service Template

| | |
|---|---|
| **File** | `Application.Services/{Entity}Service.cs` |
| **Depends on** | [repository-template](repository-template.md), [mapper-template](mapper-template.md), [dto-template](dto-template.md) |
| **Referenced by** | [endpoint-template](endpoint-template.md), [bootstrapper.md](../skills/bootstrapper.md) |
| **Sampleapp** | `sample-app/src/Application/TaskFlow.Application.Services/Services/TodoItemService.cs` |

## File: Application/Services/{Entity}Service.cs

```csharp
namespace Application.Services;

internal class {Entity}Service(
    ILogger<{Entity}Service> logger,
    IRequestContext<string, Guid?> requestContext,
    I{Entity}RepositoryTrxn repoTrxn,
    I{Entity}RepositoryQuery repoQuery,
    IInternalMessageBus messageBus,
    IEntityCacheProvider cache,
    IFusionCacheProvider fusionCacheProvider,
    ITenantBoundaryValidator tenantBoundaryValidator) : I{Entity}Service
{
    private readonly IFusionCache _cache = fusionCacheProvider.GetCache(AppConstants.DEFAULT_CACHE);

    private Guid? RequestTenantId => requestContext.TenantId;
    private IReadOnlyCollection<string> RequestRoles => requestContext.Roles;
    private bool IsGlobalAdmin => RequestRoles.Contains(AppConstants.ROLE_GLOBAL_ADMIN);

    // ===== Search =====
    public async Task<PagedResponse<{Entity}Dto>> SearchAsync(
        SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default)
    {
        if (!IsGlobalAdmin)
        {
            request.Filter ??= new();
            request.Filter.TenantId = RequestTenantId;
        }
        return await repoQuery.Search{Entity}Async(request, ct);
    }

    // ===== Get =====
    public async Task<Result<DefaultResponse<{Entity}Dto>>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.Get{Entity}Async(id, true, ct);
        if (entity == null) return Result<DefaultResponse<{Entity}Dto>>.None();

        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
            logger, RequestTenantId, RequestRoles, entity.TenantId,
            "{Entity}:Get", nameof({Entity}), entity.Id);
        if (boundary.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(boundary.ErrorMessage!);

        return Result<DefaultResponse<{Entity}Dto>>.Success(new() { Item = entity.ToDto() });
    }

    // ===== Create =====
    public async Task<Result<DefaultResponse<{Entity}Dto>>> CreateAsync(
        DefaultRequest<{Entity}Dto> request, CancellationToken ct = default)
    {
        var dto = request.Item;

        // Structure validation
        var validation = {Entity}StructureValidator.ValidateCreate(dto);
        if (validation.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(validation.Errors);

        // Tenant boundary
        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
            logger, RequestTenantId, RequestRoles, dto.TenantId,
            "{Entity}:Create", nameof({Entity}));
        if (boundary.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(boundary.ErrorMessage!);

        // Create domain entity via factory + UpdateFromDto for children
        var entityResult = dto.ToEntity(dto.TenantId)
            .Bind(e => repoTrxn.UpdateFromDto(e, dto));
        if (entityResult.IsFailure)
            return Result<DefaultResponse<{Entity}Dto>>.Failure(entityResult.ErrorMessage);

        var entity = entityResult.Value!;
        repoTrxn.Create(ref entity);

        try
        {
            await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        }
        catch (Exception ex)
        {
            return Result<DefaultResponse<{Entity}Dto>>.Failure(ex.GetBaseException().Message);
        }

        await _cache.SetAsync($"{Entity}:{entity.Id}", entity.ToDto(), token: ct);

        return Result<DefaultResponse<{Entity}Dto>>.Success(new() { Item = entity.ToDto() });
    }

    // ===== Update =====
    public async Task<Result<DefaultResponse<{Entity}Dto>>> UpdateAsync(
        DefaultRequest<{Entity}Dto> request, CancellationToken ct = default)
    {
        var dto = request.Item;

        // Structure validation
        var validation = {Entity}StructureValidator.ValidateUpdate(dto);
        if (validation.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(validation.Errors);

        // Fetch existing
        var entity = await repoTrxn.Get{Entity}Async(dto.Id!.Value, true, ct);
        if (entity == null) return Result<DefaultResponse<{Entity}Dto>>.Success(new() { Item = null });

        // Tenant boundary
        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
            logger, RequestTenantId, RequestRoles, entity.TenantId,
            "{Entity}:Update", nameof({Entity}), entity.Id);
        if (boundary.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(boundary.ErrorMessage!);

        // Prevent tenant change
        var tenantChange = tenantBoundaryValidator.PreventTenantChange(
            logger, entity.TenantId, dto.TenantId, nameof({Entity}), entity.Id);
        if (tenantChange.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(tenantChange.ErrorMessage!);

        // Update domain entity via UpdateFromDto (handles children)
        var updateResult = repoTrxn.UpdateFromDto(entity, dto);
        if (updateResult.IsFailure)
            return Result<DefaultResponse<{Entity}Dto>>.Failure(updateResult.ErrorMessage);

        try
        {
            await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        }
        catch (Exception ex)
        {
            return Result<DefaultResponse<{Entity}Dto>>.Failure(ex.GetBaseException().Message);
        }

        await _cache.SetAsync($"{Entity}:{entity.Id}", entity.ToDto(), token: ct);

        return Result<DefaultResponse<{Entity}Dto>>.Success(new() { Item = entity.ToDto() });
    }

    // ===== Delete (idempotent — return success if not found) =====
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.Get{Entity}Async(id, false, ct);
        if (entity == null) return Result.Success();  // idempotent

        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
            logger, RequestTenantId, RequestRoles, entity.TenantId,
            "{Entity}:Delete", nameof({Entity}), entity.Id);
        if (boundary.IsFailure) return Result.Failure(boundary.ErrorMessage!);

        repoTrxn.Delete(entity);

        try
        {
            await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.GetBaseException().Message);
        }

        await _cache.RemoveAsync($"{Entity}:{entity.Id}", token: ct);

        return Result.Success();
    }

    // ===== Lookup (autocomplete / dropdowns) =====
    public async Task<StaticList<StaticItem<Guid, Guid?>>> LookupAsync(
        Guid? tenantId, string? search, CancellationToken ct = default)
    {
        // Use request-context tenant if not global admin
        if (!IsGlobalAdmin) tenantId = RequestTenantId;
        return await repoQuery.Lookup{Entity}Async(tenantId, search, ct);
    }
}
```

## File: Application/Contracts/Services/I{Entity}Service.cs

```csharp
namespace Application.Contracts.Services;

public interface I{Entity}Service
{
    Task<PagedResponse<{Entity}Dto>> SearchAsync(SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> CreateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> UpdateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<StaticList<StaticItem<Guid, Guid?>>> LookupAsync(Guid? tenantId, string? search, CancellationToken ct = default);
}
```

## Common Mistakes (Verified via Test Failures)

1. **Delete no-op** — Forgetting `repoTrxn.Delete(entity)` before `SaveChangesAsync`. The entity is loaded but never marked for deletion. Save commits nothing.
2. **CreateAsync incomplete** — `Entity.Create()` only accepts factory constructor args. Additional DTO properties (e.g., `EstimatedHours`, `ActualHours`, `Description`) must be applied via `entity.Update(...)` after creation. If omitted, domain validation that depends on those fields won't trigger.
3. **Wrong SaveChangesAsync** — `DbContextBase.SaveChangesAsync(CancellationToken)` throws `NotImplementedException` by design. Must use `SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct)`.

## Policy Notes

- Monetary and time-boundary sensitive logic should be delegated to dedicated policy services (for example money calculation, entitlement resolution, and period boundary policy) rather than hard-coded inside endpoint handlers.
