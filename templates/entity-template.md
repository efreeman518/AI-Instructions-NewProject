# Entity Template

| | |
|---|---|
| **File** | `Domain.Model/Entities/{Entity}.cs` |
| **Depends on** | [domain-definition-schema.md](../domain-definition-schema.md), [resource-implementation-schema.md](../resource-implementation-schema.md) |
| **Referenced by** | [dto-template](dto-template.md), [ef-configuration-template](ef-configuration-template.md), [mapper-template](mapper-template.md) |
| **Sampleapp** | `sampleapp/src/Domain/TaskFlow.Domain.Model/Entities/TodoItem.cs` |

## File: Domain/Model/Entities/{Entity}.cs

```csharp
using Domain.Shared;
using EF.Domain;
using EF.Domain.Contracts;

namespace Domain.Model;

public class {Entity} : EntityBase, ITenantEntity<Guid>  // ITenantEntity only if multi-tenant
{
    // ===== Factory Create — the ONLY way to create an instance =====
    public static DomainResult<{Entity}> Create(Guid tenantId, string name, /* additional params */)
    {
        var entity = new {Entity}(tenantId, name);
        return entity.Valid().Map(_ => entity);
    }

    // ===== Private constructor — enforces factory usage =====
    private {Entity}(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
    }

    // ===== EF Core parameterless constructor =====
    private {Entity}() { }

    // ===== Properties — private setters enforce immutability outside domain methods =====
    public Guid TenantId { get; init; }  // init for tenant (set once)
    public string Name { get; private set; } = null!;
    public {Entity}Flags Flags { get; private set; } = {Entity}Flags.None;

    // ===== Navigation Properties — ICollection<T>, never List<T> =====
    public ICollection<{ChildEntity}> {ChildEntity}s { get; private set; } = [];

    // ===== Update — returns DomainResult for validation =====
    public DomainResult<{Entity}> Update(string? name = null, {Entity}Flags? flags = null)
    {
        if (name is not null) Name = name;
        if (flags.HasValue) Flags = flags.Value;
        return Valid().Map(_ => this);
    }

    // ===== Child Collection Management =====
    public DomainResult<{ChildEntity}> Add{ChildEntity}({ChildEntity} child)
    {
        var existing = {ChildEntity}s.FirstOrDefault(c => c.Id == child.Id);
        if (existing != null) return DomainResult<{ChildEntity}>.Success(existing);  // Idempotent

        {ChildEntity}s.Add(child);
        return DomainResult<{ChildEntity}>.Success(child);
    }

    public DomainResult Remove{ChildEntity}({ChildEntity} child)
    {
        {ChildEntity}s.Remove(child);
        return DomainResult.Success();
    }

    // ===== Validation — called by Create() and Update() =====
    private DomainResult<{Entity}> Valid()
    {
        var errors = new List<DomainError>();

        if (TenantId == Guid.Empty) errors.Add(DomainError.Create("Tenant ID cannot be empty."));
        if (string.IsNullOrWhiteSpace(Name)) errors.Add(DomainError.Create("Name is required."));

        return errors.Count > 0
            ? DomainResult<{Entity}>.Failure(errors)
            : DomainResult<{Entity}>.Success(this);
    }
}
```

## File: Domain/Shared/{Entity}Flags.cs

```csharp
namespace Domain.Shared;

[Flags]
public enum {Entity}Flags
{
    None = 0,
    IsInactive = 1 << 0,
    IsArchived = 1 << 1,
    IsSuspended = 1 << 2,
    // Add domain-specific flags
}
```

## Polymorphic Ordered Block Note (Optional)

For playlist-driven content entities, model ordered blocks with:

- explicit `Position`
- block discriminator/type
- payload invariants enforced in `Valid()`/domain rules (for example text block requires text, image block requires image URL)

## File: Domain/Model/Entities/{Parent}{Related}.cs (Join Entity)

Default many-to-many join entity pattern — inherits `EntityBase` with FK on both sides. Only use a pure composite-key join (no `EntityBase`) when confident the join will remain a pure association.

```csharp
using EF.Domain;

namespace Domain.Model;

public class {Parent}{Related} : EntityBase
{
    public static DomainResult<{Parent}{Related}> Create(Guid parentId, Guid relatedId)
    {
        var entity = new {Parent}{Related}(parentId, relatedId);
        return DomainResult<{Parent}{Related}>.Success(entity);
    }

    private {Parent}{Related}(Guid parentId, Guid relatedId)
    {
        {Parent}Id = parentId;
        {Related}Id = relatedId;
    }

    private {Parent}{Related}() { }

    public Guid {Parent}Id { get; init; }
    public Guid {Related}Id { get; init; }

    public {Parent} {Parent} { get; private set; } = null!;
    public {Related} {Related} { get; private set; } = null!;

    // Add properties as needed (e.g., AssignedDate, SortOrder, CreatedBy)
}
```

**EF Configuration:**
```csharp
// Unique constraint on FK pair (PK is Id from EntityBase)
builder.HasIndex(e => new { e.{Parent}Id, e.{Related}Id }).IsUnique();

builder.HasOne(e => e.{Parent})
    .WithMany(e => e.{Parent}{Related}s)
    .HasForeignKey(e => e.{Parent}Id)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.{Related})
    .WithMany()
    .HasForeignKey(e => e.{Related}Id)
    .OnDelete(DeleteBehavior.Restrict);
```
