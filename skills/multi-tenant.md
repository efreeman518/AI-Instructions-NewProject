# Multi-Tenant Architecture

Reference implementation: `sample-app/src/Infrastructure/TaskFlow.Infrastructure.Data/` (query filters), `sample-app/src/Application/TaskFlow.Application.Services/` (tenant boundary validation).

## Purpose

Enforce tenant isolation through data, service, and request-context layers with explicit global-admin escape paths only where intended.

## Enforcement Layers

1. EF query filters on tenant-scoped entities.
2. Service-layer tenant boundary validation.
3. Scoped `IRequestContext` built from authenticated claims (or background fallback context).

## Non-Negotiables

1. Tenant-scoped entities implement `ITenantEntity<Guid>`.
2. DbContext applies tenant query filters automatically for tenant entities.
3. Services validate tenant boundary before returning/modifying entity data.
4. Create/update flows derive tenant from request context, not client payload.
5. Global-admin bypass is explicit and auditable.

---

## Tenant Entity Contract

```csharp
public interface ITenantEntity<TTenantId>
{
    TTenantId TenantId { get; }
}

public class TodoItem : EntityBase, ITenantEntity<Guid>
{
    public Guid TenantId { get; init; }
}
```

`TenantId` should be immutable after creation.

---

## Automatic Query Filters

```csharp
private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
{
    var tenantEntityClrTypes = modelBuilder.Model.GetEntityTypes()
        .Where(et => typeof(ITenantEntity<Guid>).IsAssignableFrom(et.ClrType))
        .Select(et => et.ClrType);

    foreach (var clrType in tenantEntityClrTypes)
    {
        var filter = BuildTenantFilter(clrType);
        modelBuilder.Entity(clrType).HasQueryFilter(filter);
    }
}
```

Use `IgnoreQueryFilters()` only for explicitly authorized cross-tenant paths (for example, global admin tooling).

---

## Request Context Contract

```csharp
public interface IRequestContext<TAuditId, TTenantId>
{
    string CorrelationId { get; }
    TAuditId AuditId { get; }
    TTenantId TenantId { get; }
    IReadOnlyCollection<string> Roles { get; }
}
```

Registration pattern:

- HTTP path: resolve correlation id, audit id, tenant claim, and roles.
- background path: create fallback context with no tenant and synthetic audit id.

---

## Tenant Boundary Validator

Keep centralized service-level checks in `Application.Services/Rules/`.

Core responsibilities:

1. allow global-admin bypass (`AppConstants.ROLE_GLOBAL_ADMIN`),
2. fail when request tenant is missing for tenant-scoped operations,
3. fail on tenant mismatch,
4. prevent tenant reassignment after entity creation.

Pattern:

```csharp
public sealed class TenantBoundaryValidator : ITenantBoundaryValidator
{
    public Result EnsureTenantBoundary(
        ILogger logger,
        Guid? requestTenantId,
        IReadOnlyCollection<string> requestRoles,
        Guid? entityTenantId,
        string operation,
        string entityName,
        Guid? entityId = null)
    {
        if (requestRoles.Contains(AppConstants.ROLE_GLOBAL_ADMIN))
            return Result.Success();

        if (!requestTenantId.HasValue)
            return Result.Failure("Request context has no tenant.");

        if (entityTenantId.HasValue && requestTenantId.Value != entityTenantId.Value)
            return Result.Failure("Access denied: tenant mismatch.");

        return Result.Success();
    }
}
```

---

## Service Usage Rules

For entity reads/writes:

1. load entity (or query projection),
2. enforce `EnsureTenantBoundary(...)`,
3. continue only on success.

For searches:

- non-admin requests must force filter tenant to request context tenant,
- never trust client-supplied tenant filter as-is.

---

## API and Route Considerations

- Tenant-scoped APIs may include `tenantId` in route.
- Apply route-tenant vs claim-tenant policy (`TenantMatch`) at gateway/API boundary.
- Keep cross-tenant endpoints clearly separated and admin-guarded.

---

## Data-Access Performance Rule

Use composite tenant access index on hot entities:

```csharp
builder.HasIndex(e => new { e.TenantId, e.Id })
       .HasDatabaseName("CIX_{Entity}_TenantId_Id")
       .IsUnique()
       .IsClustered();
```

---

## Testing Expectations

Minimum test matrix:

1. same-tenant access succeeds,
2. cross-tenant access is rejected,
3. global-admin cross-tenant access succeeds where explicitly allowed,
4. tenant-change attempts fail.

---

## Verification

- [ ] tenant entities implement `ITenantEntity<Guid>`
- [ ] DbContext applies tenant query filters for tenant entities
- [ ] request context resolves tenant/roles from claims (with background fallback)
- [ ] `TenantBoundaryValidator` is used in service operations
- [ ] create/update flows set tenant from request context, not DTO payload
- [ ] global-admin bypass is explicit and limited
- [ ] tests cover same-tenant, cross-tenant, and admin-bypass scenarios
- [ ] cross-check with [application-layer.md](application-layer.md) and [domain-model.md](domain-model.md)