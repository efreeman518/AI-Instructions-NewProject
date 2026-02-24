# Mapper Template

| | |
|---|---|
| **File** | `Application.Contracts/Mappers/{Entity}Mapper.cs` |
| **Depends on** | [entity-template](entity-template.md), [dto-template](dto-template.md) |
| **Referenced by** | [service-template](service-template.md), [repository-template](repository-template.md) |

## File: Application/Contracts/Mappers/{Entity}Mapper.cs

```csharp
using System.Linq.Expressions;
using EF.Common;

namespace Application.Contracts.Mappers;

public static class {Entity}Mapper
{
    // ===== Entity → DTO (extension method) =====
    public static {Entity}Dto ToDto(this {Entity} entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Flags = entity.Flags,
            {ChildEntity}s = entity.{ChildEntity}s.Select(c => c.ToDto()).ToList(),
            Parent = entity.Parent?.ToDto()
        };

    // ===== DTO → Entity (factory, returns DomainResult) =====
    public static DomainResult<{Entity}> ToEntity(this {Entity}Dto dto, Guid tenantId)
    {
        return {Entity}.Create(tenantId, dto.Name);
    }

    // ===== EF-Safe Projectors (Expression<Func<T, TDto>>) =====

    // Minimal projection for lists/grids
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> ProjectorSearch =
        entity => new {Entity}Dto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Flags = entity.Flags
            // No child collections for search results
        };

    // Full projection for detail view
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> ProjectorRoot =
        entity => new {Entity}Dto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Flags = entity.Flags,
            {ChildEntity}s = entity.{ChildEntity}s.Select(c => new {ChildEntity}Dto
            {
                Id = c.Id,
                // ... child properties
            }).ToList()
        };

    // Lookup projection for dropdowns/autocompletes
    public static readonly Expression<Func<{Entity}, StaticItem<Guid, Guid?>>> ProjectorStaticItems =
        entity => new StaticItem<Guid, Guid?>(entity.Id, entity.Name, null);
}
```

## File: Application/Contracts/Mappers/{ChildEntity}Mapper.cs

```csharp
namespace Application.Contracts.Mappers;

public static class {ChildEntity}Mapper
{
    public static {ChildEntity}Dto ToDto(this {ChildEntity} entity) =>
        new()
        {
            Id = entity.Id,
            // ... properties
        };

    public static readonly Expression<Func<{ChildEntity}, {ChildEntity}Dto>> ProjectorSearch =
        entity => new {ChildEntity}Dto
        {
            Id = entity.Id,
            // ... properties
        };
}
```

## Notes

- Mappers are **static classes** in `Application.Contracts/Mappers/` — no DI, no state
- **`ToDto()`** — Extension method on entity. Used after loading full entity with includes.
- **`ToEntity()`** — Extension method on DTO. Returns `DomainResult<T>` (delegates to domain factory).
- **Projectors** — `Expression<Func<T, TDto>>` for EF query projection. MUST be EF-safe (no method calls, no `ToString(format)`, no complex ternaries).
- **Multiple projectors per entity** — `ProjectorSearch` (minimal), `ProjectorRoot` (full), `ProjectorStaticItems` (lookup).
- **No mapper registration** — Static classes, no DI needed.
- **No audit fields** — Audit data (CreatedDate, CreatedBy, etc.) is managed by the `AuditInterceptor`, not mapped on DTOs
