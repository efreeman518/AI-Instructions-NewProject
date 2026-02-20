# DTO Template

| | |
|---|---|
| **File** | `Application.Models/{Entity}/{Entity}Dto.cs`, `{Entity}SearchFilter.cs` |
| **Depends on** | [entity-template](entity-template.md) |
| **Referenced by** | [mapper-template](mapper-template.md), [service-template](service-template.md), [endpoint-template](endpoint-template.md) |

## File: Application/Models/{Entity}/{Entity}Dto.cs

```csharp
using Package.Infrastructure.Domain.Contracts;

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

## File: Application/Models/{Entity}/{ChildEntity}Dto.cs

```csharp
using Package.Infrastructure.Domain.Contracts;

namespace Application.Models.{Entity};

public record {ChildEntity}Dto : EntityBaseDto
{
    public Guid? {Entity}Id { get; set; }
    // ... properties
}
```

## File: Application/Models/{Entity}/{Entity}SearchFilter.cs

```csharp
namespace Application.Models.{Entity};

public class {Entity}SearchFilter
{
    public Guid? TenantId { get; set; }
    public string? Name { get; set; }
    public {Entity}Flags? Flags { get; set; }
}
```

## Base DTO Types (from Application.Models/Shared/)

```csharp
public record EntityBaseDto : IEntityBaseDto
{
    public Guid? Id { get; set; }
}

public interface ITenantEntityDto
{
    Guid TenantId { get; set; }
}
```

## Notes

- DTOs are `record` types (value equality, `with` expressions)
- DTOs live in `Application.Models/{Entity}/` — separate project from contracts
- `Id` is `Guid?` — null on Create, required on Update (inherited from `EntityBaseDto`)
- `TenantId` is `Guid` (non-nullable) — always required
- Search filters are `class` (not record) so properties can be mutated (e.g., forcing TenantId)
- Audit fields (CreatedDate, etc.) may be included as read-only properties on response DTOs via `IEntityBaseDto` — the `AuditInterceptor` on the DbContext manages write-side audit data
- Child collections default to empty list — never null
- Use `{Entity}Flags` (flags enum) instead of a separate Status enum
