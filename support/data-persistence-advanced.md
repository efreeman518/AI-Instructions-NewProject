# Data Persistence — Advanced

Load this file only when the current task needs design-time factory setup, migration strategy, JSON column troubleshooting, startup seeding, or zero-downtime schema change guidance.

Core read/write repository and EF configuration guidance stays in [../skills/data-persistence.md](../skills/data-persistence.md).

---

## Design-Time Factory

Use `IDesignTimeDbContextFactory<{Project}DbContextTrxn>` for CLI migrations.

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<{Project}DbContextTrxn>
{
    public {Project}DbContextTrxn CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EFCORETOOLSDB")
            ?? throw new InvalidOperationException("Set EFCORETOOLSDB env var");

        var optionsBuilder = new DbContextOptionsBuilder<{Project}DbContextTrxn>();
        optionsBuilder.UseSqlServer(connectionString);
        return new {Project}DbContextTrxn(optionsBuilder.Options);
    }
}
```

---

## JSON Columns (`ToJson()`) Troubleshooting

`ToJson()` with owned types is the preferred pattern for structured data stored as JSON in SQL Server. EF Core may still fail to generate migrations for complex graphs with nested collections or dictionaries.

Fallback: use a serializer-backed value conversion to `nvarchar(max)` with a custom `ValueComparer`.

```csharp
builder.Property(e => e.ComplexData)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<ComplexType>(v, (JsonSerializerOptions?)null)!)
    .HasColumnType("nvarchar(max)")
    .Metadata.SetValueComparer(
        new ValueComparer<ComplexType>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<ComplexType>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
```

If you use this fallback, record it in `HANDOFF.md` and repo docs. Do not hand-edit migration files to work around `ToJson()` failures.

---

## Migrations

### EF CLI Prerequisites

Before running any migration command, ensure `dotnet ef` is available. If not installed globally, set up repo-local tooling:

```powershell
dotnet ef --version
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

The startup project must reference `Microsoft.EntityFrameworkCore.Design`.

If `nuget.config` uses `<packageSourceMapping>`, add an explicit entry for `dotnet-ef` under `nuget.org`.

### Migration Naming

Format: `YYYYMMDD_Description`.

```text
20260301_InitialCreate
20260305_AddCategoryColorHex
20260310_AddTodoItemPriority
```

Never rename a migration after it has been shared with any environment or teammate.

### Canonical Commands

```powershell
$env:EFCORETOOLSDB = "Server=..."

dotnet ef migrations add {MigrationName} `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/Host/{Host}.Api `
  --context {App}DbContextTrxn

dotnet ef database update `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/Host/{Host}.Api `
  --context {App}DbContextTrxn

dotnet ef migrations script --idempotent `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/Host/{Host}.Api `
  --context {App}DbContextTrxn `
  -o migrations.sql
```

### Data Migrations

Migration files should contain schema changes only. Use one-time background jobs for non-trivial backfill and use `migrationBuilder.Sql()` only for simple, safe updates.

### Zero-Downtime Schema Changes

Use expand/contract:

1. Expand: add nullable/defaulted shape, deploy code that writes both old and new.
2. Backfill: migrate existing data.
3. Contract: remove old shape in a later deployment.

Never combine expand and contract in one deployment.

### Rollback Strategy

Development:

```powershell
dotnet ef database update {PreviousMigrationName} `
  --context {App}DbContextTrxn
```

Production:

- Use idempotent forward-only scripts.
- Prefer blue-green deployment.
- Never delete a migration applied to any shared environment.

### Multi-Store Consistency

For SQL + Cosmos/Table hybrids:

1. EF migrations apply to SQL only.
2. Document stores evolve through code and defaults.
3. Deploy tolerant code before the SQL migration.

---

## Startup Seeding / Reference Data

Use the `IStartupTask` pattern for idempotent seed data after `app.Build()` and before `app.RunAsync()`.

```csharp
public class SeedReferenceDataTask : IStartupTask
{
    private readonly IDbContextFactory<{App}DbContextTrxn> _factory;

    public SeedReferenceDataTask(IDbContextFactory<{App}DbContextTrxn> factory)
        => _factory = factory;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var existing = (await db.Set<{Entity}>()
            .Where(e => e.IsSystem)
            .Select(e => e.Id)
            .ToListAsync(ct)).ToHashSet();

        var seeds = {Entity}Seeds.All
            .Where(s => !existing.Contains(s.Id));

        foreach (var seed in seeds)
        {
            db.Set<{Entity}>().Add(seed);
        }

        await db.SaveChangesAsync(true, ct);
    }
}
```

Seeding rules:

- Seed deterministically with fixed GUIDs.
- Check existence before insert.
- Mark seeded rows with `IsSystem = true`.
- Register startup tasks in dependency order.

---

## Load This File When

- You are running `dotnet ef` commands.
- You need design-time factory guidance.
- A JSON-column mapping fails during migration generation.
- You are planning expand/contract schema changes.
- You need startup seeding patterns.