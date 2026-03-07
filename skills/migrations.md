# Migrations

## Purpose

EF Core migration strategy for SQL entities. Cosmos, Table Storage, and Blob Storage do not use EF migrations — see [cosmosdb-data.md](cosmosdb-data.md) for Cosmos schema evolution patterns.

Complements [data-access.md](data-access.md) (which covers migration CLI basics). This skill adds naming conventions, zero-downtime patterns, data migrations, and multi-store coordination.

---

## Migration Naming

Format: `YYYYMMDD_Description` — one migration per feature/slice.

```
20260301_InitialCreate
20260305_AddCategoryColorHex
20260310_AddTodoItemPriority
20260315_RemoveDeprecatedStatusColumn
```

> **CRITICAL:** Never rename a migration after sharing it with any environment or team member. EF tracks migrations by name in `__EFMigrationsHistory`.

---

## Migration Commands

Canonical CLI commands (consistent with [engineer-checklist.md](../support/engineer-checklist.md) and [data-access.md](data-access.md)):

```powershell
# Add migration
dotnet ef migrations add {MigrationName} `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn

# Apply to local database
dotnet ef database update `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn

# Generate idempotent script (for production deployments)
dotnet ef migrations script --idempotent `
  --project src/Infrastructure/{Project}.Infrastructure.Repositories `
  --startup-project src/{Host}/{Host}.Api `
  --context {App}DbContextTrxn `
  -o migrations.sql
```

---

## Data Migrations

### Schema-Only in Migration Files

Migration files should contain **schema changes only** — `CreateTable`, `AddColumn`, `AlterColumn`, `DropColumn`, etc.

### Data Backfill via Job

For data transformations, use a one-time background job with idempotency guard:

```csharp
// One-time data migration job
public class BackfillCategoryColorHandler : IScheduledJobHandler
{
    public async Task ExecuteAsync(IServiceProvider sp, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<ICategoryRepositoryTrxn>();
        var unset = await repo.GetCategoriesWithoutColorAsync(ct);
        foreach (var category in unset)
        {
            category.Update(colorHex: "#808080"); // default gray
        }
        await repo.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
    }
}
```

Use `migrationBuilder.Sql()` **only** for simple, safe data operations (e.g., setting a non-null default on a new column):

```csharp
migrationBuilder.Sql("UPDATE Categories SET ColorHex = '#808080' WHERE ColorHex IS NULL");
```

---

## Rollback Strategy

### Development

```powershell
dotnet ef database update {PreviousMigrationName} `
  --context {App}DbContextTrxn
```

### Production

- Use **idempotent scripts** (`--idempotent`) for forward-only deployment
- Blue-green deploy: apply migration to new slot, verify, swap traffic
- Never delete a migration that has been applied to any shared environment

---

## Zero-Downtime Schema Changes

Use the **expand/contract** pattern for breaking schema changes:

### Phase 1 — Expand
Add new column (nullable or with default). Deploy code that **writes to both** old and new columns.

```csharp
// Migration: 20260310_AddNewStatusColumn
migrationBuilder.AddColumn<string>("StatusV2", "TodoItems", nullable: true);
```

### Phase 2 — Backfill
Run data migration to populate new column from old column values.

```csharp
// Migration: 20260312_BackfillStatusV2
migrationBuilder.Sql("UPDATE TodoItems SET StatusV2 = Status WHERE StatusV2 IS NULL");
```

### Phase 3 — Contract
Remove old column. Deploy code that **reads only** from new column.

```csharp
// Migration: 20260315_RemoveOldStatusColumn
migrationBuilder.DropColumn("Status", "TodoItems");
```

> Each phase is a **separate migration + deployment cycle**. Never combine expand and contract in one deployment.

---

## Multi-Store Consistency

For solutions using SQL + Cosmos/Table hybrids:

1. **Migrations apply to SQL only** — EF migrations manage SQL Server schema
2. **Cosmos schema changes are code-level** — add property to C# model + set default value for existing documents
3. **Coordination workflow:**
   - Deploy code first (handles both old and new document shapes via default values)
   - Then run SQL migration
   - Document stores are schema-flexible — no separate migration step needed

---

## Verification Checklist

- [ ] Migration name follows `YYYYMMDD_Description` format
- [ ] One migration per feature/slice — no mega-migrations
- [ ] CLI commands use `--context {App}DbContextTrxn` (never query context)
- [ ] Design-time factory exists per [data-access.md](data-access.md)
- [ ] Data backfill uses background job (not inline migration SQL) for complex transforms
- [ ] Breaking schema changes use expand/contract across multiple deployments
- [ ] Production deployments use idempotent scripts
- [ ] No migration renamed after sharing
- [ ] Multi-store changes deploy code before SQL migration

