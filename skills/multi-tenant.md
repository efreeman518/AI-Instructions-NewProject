# Multi-Tenant Architecture

## Overview

Multi-tenancy is implemented via **automatic EF query filters**, **tenant boundary validation** in the service layer, and **request context** populated from authenticated user claims. Global admins can bypass tenant restrictions.

## Tenant Entity Interface

Entities that are tenant-scoped implement `ITenantEntity<Guid>`:

```csharp
// From Package.Infrastructure.Domain.Contracts
public interface ITenantEntity<TTenantId>
{
    TTenantId TenantId { get; }
}
```

```csharp
// Entity implementation
public class Product : EntityBase, ITenantEntity<Guid>
{
    public Guid TenantId { get; init; }  // init — set once at creation, never changed
    // ...
}
```

## Automatic Query Filters

The base DbContext discovers all `ITenantEntity<Guid>` types and applies query filters automatically.

> **Reference implementation:** See `sampleapp/src/Infrastructure/TaskFlow.Infrastructure.Data/Migrations/20260101000000_InitialCreate.cs` for tenant-scoped composite clustered indexes, and `sampleapp/src/Domain/TaskFlow.Domain.Model/` for entities implementing `ITenantEntity<Guid>`.

```csharp
// In {Project}DbContextBase
private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
{
    var tenantEntityClrTypes = modelBuilder.Model.GetEntityTypes()
        .Where(et => typeof(ITenantEntity<Guid>).IsAssignableFrom(et.ClrType))
        .Select(et => et.ClrType);

    foreach (var clrType in tenantEntityClrTypes)
    {
        var filter = BuildTenantFilter(clrType);  // from DbContextBase<TAuditId, TTenantId>
        modelBuilder.Entity(clrType).HasQueryFilter(filter);
    }
}
```

This means **every query** on a tenant entity automatically filters by the current user's tenant. No manual `.Where(e => e.TenantId == tenantId)` needed — EF does it transparently.

To bypass the filter (e.g., for global admin cross-tenant queries), use:
```csharp
dbContext.Set<Product>().IgnoreQueryFilters().Where(...)
```

## Request Context

`IRequestContext<TAuditId, TTenantId>` is a scoped service populated from HTTP claims or background service context.

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Bootstrapper/RegisterServices.cs` for `IRequestContext` registration with HTTP claim extraction and background service fallback.

```csharp
public interface IRequestContext<TAuditId, TTenantId>
{
    string CorrelationId { get; }
    TAuditId AuditId { get; }        // User identifier for audit trail
    TTenantId TenantId { get; }      // Current tenant (null for global admin)
    IReadOnlyCollection<string> Roles { get; }
}
```

### Registration (in Bootstrapper)

```csharp
services.AddScoped<IRequestContext<string, Guid?>>(provider =>
{
    var httpContext = provider.GetService<IHttpContextAccessor>()?.HttpContext;
    var correlationId = Guid.NewGuid().ToString();

    if (httpContext == null)
    {
        // Background service context
        return new RequestContext<string, Guid?>(correlationId, $"BackgroundService-{correlationId}", null, []);
    }

    // Extract from HTTP headers/claims
    if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValues))
    {
        var val = headerValues.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(val)) correlationId = val;
    }

    var user = httpContext.User;
    var auditId = user.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
        ?? user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
        ?? "NoAuditClaim";

    var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "userTenantId")?.Value;
    var tenantId = Guid.TryParse(tenantIdClaim, out var tid) ? tid : (Guid?)null;
    var roles = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

    return new RequestContext<string, Guid?>(correlationId, auditId, tenantId, roles);
});
```

## Tenant Boundary Validation

Centralized validation in `Application.Services/Rules/`:

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.Contracts/Services/ITenantBoundaryValidator.cs` for the interface, `sampleapp/src/Application/TaskFlow.Application.Services/TenantBoundaryValidator.cs` for the implementation, and `sampleapp/src/Application/TaskFlow.Application.Services/Rules/ValidationHelper.cs` for the static validation helper methods.

```csharp
namespace Application.Services.Rules;

public sealed class TenantBoundaryValidator : ITenantBoundaryValidator
{
    /// <summary>
    /// Ensures the request context can access the target entity's tenant.
    /// Global admins bypass; non-admins must match tenantId.
    /// </summary>
    public Result EnsureTenantBoundary(
        ILogger logger,
        Guid? requestTenantId,
        IReadOnlyCollection<string> requestRoles,
        Guid? entityTenantId,
        string operation,
        string entityName,
        Guid? entityId = null)
    {
        // Global admin can access any tenant
        if (requestRoles.Contains(AppConstants.ROLE_GLOBAL_ADMIN))
            return Result.Success();

        // Request context must have a tenant
        if (!requestTenantId.HasValue)
        {
            logger.LogWarning("Tenant boundary violation: no tenant. Op={Op}, Entity={Entity}", operation, entityName);
            return Result.Failure("Request context has no tenant.");
        }

        // Tenant must match
        if (entityTenantId.HasValue && requestTenantId.Value != entityTenantId.Value)
        {
            logger.LogWarning("Tenant boundary violation: {RequestTenant} != {EntityTenant}. Op={Op}, Entity={Entity}, Id={Id}",
                requestTenantId, entityTenantId, operation, entityName, entityId);
            return Result.Failure("Access denied: tenant mismatch.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Prevents changing an entity's tenant after creation.
    /// </summary>
    public Result PreventTenantChange(
        ILogger logger,
        Guid? existingTenantId,
        Guid? incomingTenantId,
        string entityName,
        Guid entityId)
    {
        if (existingTenantId.HasValue && incomingTenantId.HasValue && existingTenantId != incomingTenantId)
        {
            logger.LogWarning("Tenant change attempt: {Entity} {Id} from {Old} to {New}",
                entityName, entityId, existingTenantId, incomingTenantId);
            return Result.Failure("Cannot change entity tenant.");
        }
        return Result.Success();
    }
}
```

## Usage in Services

Every service method calls boundary validation:

```csharp
public async Task<Result<DefaultResponse<ProductDto>>> GetAsync(Guid id, CancellationToken ct = default)
{
    var entity = await repoTrxn.GetProductAsync(id, true, ct);
    if (entity == null) return Result<DefaultResponse<ProductDto>>.None();

    // Boundary check
    var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
        logger, requestContext.TenantId, requestContext.Roles, entity.TenantId, "Product:Get", nameof(Product), entity.Id);
    if (boundary.IsFailure) return Result<DefaultResponse<ProductDto>>.Failure(boundary.ErrorMessage!);

    return Result<DefaultResponse<ProductDto>>.Success(new() { Item = entity.ToDto() });
}
```

For search operations, force the tenant filter on the request:

```csharp
public async Task<PagedResponse<ProductDto>> SearchAsync(SearchRequest<ProductSearchFilter> request, CancellationToken ct = default)
{
    if (!IsGlobalAdmin)
    {
        request.Filter ??= new();
        request.Filter.TenantId = requestContext.TenantId;  // Override any client-supplied tenant
    }
    return await repoQuery.SearchProductAsync(request, ct);
}
```

## Tenant-Scoped API Routes

API endpoints include `tenantId` in the route for tenant-scoped operations:

```
v1/tenant/{tenantId}/product/{id}
v1/tenant/{tenantId}/product/search
```

With a `TenantMatch` authorization policy at the gateway/API level that verifies the route `tenantId` matches the user's claim.

## Clustered Index for Tenant Performance

EF configurations use a composite clustered index on `(TenantId, Id)`:

```csharp
builder.HasIndex(e => new { e.TenantId, e.Id })
       .HasDatabaseName("CIX_{Entity}_TenantId_Id")
       .IsUnique()
       .IsClustered();
```

This ensures rows for the same tenant are physically adjacent on disk, dramatically improving query performance for tenant-filtered queries.

## Testing Tenant Boundaries

```csharp
public abstract class TenantBoundaryTestBase : TestBase
{
    protected static readonly Guid TenantA = Guid.NewGuid();
    protected static readonly Guid TenantB = Guid.NewGuid();

    [TestMethod]
    public async Task Get_CrossTenant_ReturnsFailure()
    {
        SetRequestContext(TenantA, "User");  // Request context is in TenantA
        // Setup entity in TenantB
        // Assert service returns failure
    }

    [TestMethod]
    public async Task Get_GlobalAdmin_CrossTenant_Succeeds()
    {
        SetRequestContext(TenantA, AppConstants.ROLE_GLOBAL_ADMIN);
        // Setup entity in TenantB
        // Assert service returns success (global admin bypasses)
    }
}
```

---

## Verification

After generating multi-tenant code, confirm:

- [ ] `TenantEntityBase` extends `EntityBase` with `TenantId` property
- [ ] `{App}DbContextTrxn` applies global query filter `.HasQueryFilter(e => e.TenantId == _tenantId)` to all tenant entities
- [ ] `ITenantProvider` is registered as scoped, resolving tenant from the request context
- [ ] `TenantBoundaryValidator` in the service layer rejects cross-tenant access for non-global-admin users
- [ ] Create operations always set `TenantId` from the authenticated user's context (never from the DTO)
- [ ] Global admin role (`AppConstants.ROLE_GLOBAL_ADMIN`) bypasses tenant filter when explicitly needed
- [ ] Tests cover: same-tenant access, cross-tenant rejection, and global admin cross-tenant access
- [ ] Cross-references: DTOs implement `ITenantEntityDto` per [application-layer.md](application-layer.md), entity uses `TenantEntityBase` per [domain-model.md](domain-model.md)
