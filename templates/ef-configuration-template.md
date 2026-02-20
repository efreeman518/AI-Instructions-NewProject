# EF Configuration Template

| | |
|---|---|
| **File** | `Infrastructure.Data/Configuration/{Entity}Configuration.cs` |
| **Depends on** | [entity-template](entity-template.md) |
| **Referenced by** | [data-access.md](../skills/data-access.md), [repository-template](repository-template.md) |

## File: Infrastructure/Data/Configuration/{Entity}Configuration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configuration;

public class {Entity}Configuration() : EntityBaseConfiguration<{Entity}>(false)  // false = PK not clustered
{
    public override void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        base.Configure(builder);

        builder.ToTable("{Entity}");

        // ===== Tenant Composite Index =====
        builder.HasIndex(e => new { e.TenantId, e.Id })
               .HasDatabaseName("CIX_{Entity}_TenantId_Id")
               .IsUnique()
               .IsClustered();

        // ===== Properties =====
        builder.Property(e => e.TenantId)
               .IsRequired();

        builder.Property(e => e.Name)
               .IsRequired()
               .HasMaxLength(200);  // nvarchar(200) — realistic length for names

        // decimal(10,4) is the global default from ConfigureDefaultDataTypes;
        // override here only if this property needs different precision:
        // builder.Property(e => e.Price).HasPrecision(10, 4);

        // DateTime properties auto-map to datetime2 via ConfigureDefaultDataTypes;
        // explicit override if needed:
        // builder.Property(e => e.DueDate).HasColumnType("datetime2");

        builder.Property(e => e.Flags)
               .IsRequired()
               .HasDefaultValue({Entity}Flags.None);

        // ===== Relationships =====
        builder.HasMany(e => e.{ChildEntity}s)
               .WithOne()
               .HasForeignKey("{Entity}Id")
               .OnDelete(DeleteBehavior.Cascade);

        // ===== Indexes =====
        builder.HasIndex(e => new { e.TenantId, e.Name })
               .HasDatabaseName("IX_{Entity}_TenantId_Name");

        builder.HasIndex(e => e.Flags)
               .HasDatabaseName("IX_{Entity}_Flags");
    }
}
```

## File: Infrastructure/Data/Configuration/{ChildEntity}Configuration.cs

```csharp
namespace Infrastructure.Data.Configuration;

public class {ChildEntity}Configuration : EntityBaseConfiguration<{ChildEntity}>
{
    public override void Configure(EntityTypeBuilder<{ChildEntity}> builder)
    {
        base.Configure(builder);

        builder.ToTable("{ChildEntity}");

        builder.Property(e => e.{Entity}Id)
               .IsRequired();

        // ... child-specific properties
    }
}
```

## SQL Data Type Defaults

Apply these conventions to **every** entity configuration. The base DbContext's `ConfigureDefaultDataTypes` helper can set these globally, but each configuration should be explicit about constraints.

| C# Type | SQL Type | Convention | Example |
|---------|----------|------------|---------|
| `string` | `nvarchar(N)` | Always specify a realistic `HasMaxLength(N)`. Use lengths that match real-world data (e.g., Name → 200, Email → 254, Sku → 50, Description → 2000). **Rarely** use `nvarchar(max)` — only for truly unbounded text like rich HTML content or large notes fields. | `.HasMaxLength(200)` |
| `decimal` | `decimal(10,4)` | Default precision is `decimal(10,4)` for monetary/quantity values. Adjust only when the domain requires different precision (e.g., exchange rates → `decimal(18,8)`, integer-only counts → `decimal(10,0)`). | `.HasPrecision(10, 4)` |
| `DateTime` | `datetime2` | All `DateTime` and `DateTime?` properties map to `datetime2`. Never use the legacy `datetime` SQL type. | `.HasColumnType("datetime2")` |

### ConfigureDefaultDataTypes Helper

In the abstract base DbContext (`{Project}DbContextBase`), add a global convention method that sets these defaults for all entities:

```csharp
protected static void ConfigureDefaultDataTypes(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        foreach (var property in entityType.GetProperties())
        {
            // All decimals → decimal(10,4) unless explicitly overridden
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
            {
                if (property.GetPrecision() is null)
                    property.SetPrecision(10);
                if (property.GetScale() is null)
                    property.SetScale(4);
            }

            // All DateTime → datetime2
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
            {
                property.SetColumnType("datetime2");
            }
        }
    }
}
```

> **Individual configurations can override these defaults** when the domain requires it (e.g., `.HasPrecision(18, 8)` for currency exchange rates).

## Notes

- `EntityBaseConfiguration<T>` handles `Id` (non-clustered PK, client-generated) and `RowVersion` (concurrency token) — NO audit fields
- Audit fields (CreatedDate, CreatedBy, UpdatedDate, UpdatedBy) are managed by the `AuditInterceptor` on the DbContext, not configured here
- Clustered index on `(TenantId, Id)` ensures tenant data locality
- Composite indexes always lead with `TenantId` for filtered queries
- Enum properties: use `HasDefaultValue({Enum}.None)` for flags enums; `HasConversion<string>()` is optional for readability
- All string properties must have `HasMaxLength()` with a realistic length — no unbounded `nvarchar(max)` unless the field genuinely stores large text
- All `decimal` properties use `HasPrecision(10, 4)` by default — override per-property when needed
- All `DateTime` properties map to `datetime2` — the global convention handles this, but explicit `.HasColumnType("datetime2")` is acceptable for clarity
