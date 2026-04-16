# Data Mapping Template (DTOs + Mappers)

| | |
|---|---|
| **Files** | `Application.Models/{Entity}/{Entity}Dto.cs`, `{Entity}SearchFilter.cs`, `{ChildEntity}Dto.cs`, `Application.Mappers/{Entity}Mapper.cs`, `Application.Mappers/{ChildEntity}Mapper.cs` |
| **Depends on** | [entity-template](entity-template.md) |
| **Referenced by** | [service-template](service-template.md), [endpoint-template](endpoint-template.md), [repository-template](repository-template.md) |

## DTOs

### File: Application/Models/{Entity}/{Entity}Dto.cs

```csharp
using EF.Domain.Contracts;

namespace Application.Models.{Entity};

public record {Entity}Dto : EntityBaseDto, ITenantEntityDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    public {Entity}Flags Flags { get; set; } = {Entity}Flags.None;

    // Child collections
    public List<{ChildEntity}Dto> {ChildEntity}s { get; set; } = [];

    // Navigation (read-only, populated by mapper)
    public {Parent}Dto? Parent { get; set; }
}
```

### File: Application/Models/{Entity}/{ChildEntity}Dto.cs

```csharp
using EF.Domain.Contracts;

namespace Application.Models.{Entity};

public record {ChildEntity}Dto : EntityBaseDto
{
    public Guid? {Entity}Id { get; set; }
    // ... properties
}
```

### File: Application/Models/{Entity}/{Entity}SearchFilter.cs

```csharp
namespace Application.Models.{Entity};

public record {Entity}SearchFilter : DefaultSearchFilter
{
    public string? Name { get; set; }
    public {Entity}Flags? Flags { get; set; }
}
```

### Base DTO Types (from Application.Models/Shared/)

```csharp
// DefaultSearchFilter — base for all search filters (Application.Models/)
public record DefaultSearchFilter
{
    public string? SearchTerm { get; set; }
    public Guid? TenantId { get; set; }
}

public abstract record EntityBaseDto : IEntityBaseDto
{
    public Guid? Id { get; set; }
}

public interface IEntityBaseDto
{
    Guid? Id { get; set; }
}

public interface ITenantEntityDto
{
    Guid TenantId { get; set; }
}
```

### DTO Rules

- DTOs are `record` types (value equality, `with` expressions)
- DTOs live in `Application.Models/{Entity}/` -- separate project from contracts
- `Id` is `Guid?` -- null on Create, required on Update (inherited from `EntityBaseDto`)
- `TenantId` is `Guid` (non-nullable) -- always required
- Search filters are `record` types inheriting `DefaultSearchFilter` (provides `SearchTerm` and `TenantId`). Add only entity-specific filter properties.
- Audit fields (CreatedDate, etc.) may be included as read-only properties on response DTOs via `IEntityBaseDto` -- the `AuditInterceptor` on the DbContext manages write-side audit data
- Child collections default to empty list -- never null
- Use `{Entity}Flags` (flags enum) instead of a separate Status enum

## Mappers

### File: Application/Mappers/{Entity}Mapper.cs

```csharp
using System.Linq.Expressions;
using EF.Common;
using EF.Domain.Contracts;  // DomainResult<T> lives here, NOT in EF.Domain

namespace Application.Mappers;

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
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> ProjectorFull =
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

### File: Application/Mappers/{ChildEntity}Mapper.cs

```csharp
namespace Application.Mappers;

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

### Mapper Rules

- Mappers are **static classes** in `Application.Mappers/` -- separate project, no DI, no state
- **`ToDto()`** -- Extension method on entity. Used after loading full entity with includes.
- **`ToEntity()`** -- Extension method on DTO. Returns `DomainResult<T>` (delegates to domain factory).
- **Projectors** -- `Expression<Func<T, TDto>>` for EF query projection. MUST be EF-safe (no method calls, no `ToString(format)`, no complex ternaries).
- **Multiple projectors per entity** -- `ProjectorBasic` (minimal), `ProjectorSearch` (standard), `ProjectorFull` (complete with children), `ProjectorStaticItems` (lookup).
- **No mapper registration** -- Static classes, no DI needed.
- **No audit fields** -- Audit data (CreatedDate, CreatedBy, etc.) is managed by the `AuditInterceptor`, not mapped on DTOs

## Child Collection Mapping

Child collections follow a consistent pattern across both DTOs and Mappers:

### DTO Side
- Parent DTO declares `List<{ChildEntity}Dto> {ChildEntity}s { get; set; } = [];` -- always initialized, never null
- Child DTO inherits from `EntityBaseDto` and includes a nullable foreign key: `Guid? {Entity}Id`
- Child DTO lives in the same namespace as the parent: `Application.Models.{Entity}/`

### Mapper Side
- **`ToDto()`** maps children inline: `entity.{ChildEntity}s.Select(c => c.ToDto()).ToList()`
- **`ToEntity()`** only creates the parent via the domain factory -- child collections are handled separately by `repoTrxn.UpdateFromDto()` in the service layer
- **`ProjectorSearch`** omits child collections for performance (flat list/grid queries)
- **`ProjectorFull`** includes child collections with inline projection (no method calls -- must remain EF-safe):
  ```csharp
  {ChildEntity}s = entity.{ChildEntity}s.Select(c => new {ChildEntity}Dto
  {
      Id = c.Id,
      // ... child properties mapped directly
  }).ToList()
  ```

### Service Layer Integration
- On **Create**: `dto.ToEntity(tenantId)` builds the parent, then `repoTrxn.UpdateFromDto(entity, dto)` syncs child collections
- On **Update**: The existing entity is fetched with includes, then `repoTrxn.UpdateFromDto(entity, dto)` diffs and syncs children (adds new, updates existing, removes missing)
- The repository's `UpdateFromDto` handles the EF change-tracking for child add/update/remove -- mappers do not manage this
