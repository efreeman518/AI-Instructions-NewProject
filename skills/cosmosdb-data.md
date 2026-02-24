# Cosmos DB Data Access

## Prerequisites

- [package-dependencies.md](package-dependencies.md) — `EF.CosmosDb` package types
- [solution-structure.md](solution-structure.md) — project layout and Infrastructure layer conventions
- [bootstrapper.md](bootstrapper.md) — centralized DI registration
- [configuration.md](configuration.md) — appsettings and secrets management

## Overview

Cosmos DB data access uses `EF.CosmosDb` which provides a **repository abstraction** over the Azure Cosmos DB SDK v3. Entities that need document-store semantics — flexible schema, horizontal partitioning, global distribution, or **deeply nested JSON structures** — use Cosmos DB instead of EF Core/SQL.

> **When to use Cosmos DB vs SQL:** Use Cosmos DB for high-throughput, partition-friendly data (audit logs, event streams, denormalized read models, IoT telemetry, session/cart data) and for documents with deep/nested structure (embedded objects, arrays of child objects, dictionaries) that would require many tables and JOINs in SQL. Use EF Core/SQL for relational data with complex joins, transactions, and referential integrity.

---

## Entity Pattern

All Cosmos DB entities inherit from `CosmosDbEntity` (which extends `EntityBase`), and must define a `PartitionKey`:

```csharp
namespace {Project}.Domain.Model;

public class {Entity} : CosmosDbEntity
{
    // CosmosDB required — determines physical partitioning
    public override string PartitionKey => TenantId.ToString();

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedDate { get; private set; } = DateTime.UtcNow;

    // Factory Create pattern (same as SQL entities)
    public static DomainResult<{Entity}> Create(Guid tenantId, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DomainResult<{Entity}>.Failure("Name is required.");

        return DomainResult<{Entity}>.Success(new {Entity}
        {
            TenantId = tenantId,
            Name = name,
            Description = description ?? string.Empty
        });
    }
}
```

### Embedded / Nested Object Patterns

Cosmos DB documents can contain deeply nested JSON — embedded objects, arrays, and dictionaries — all stored and retrieved as a single document. Define these as C# classes or collections on the entity; they serialize naturally with `System.Text.Json`.

```csharp
// Embedded value object — stored as nested JSON in the document
public class Schedule
{
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
}

public class TeamInfo
{
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

// Embedded child collection — stored as JSON array in the document
public class ChecklistItem
{
    public string Description { get; set; } = null!;
    public bool IsComplete { get; set; }
    public int SortOrder { get; set; }
}

public class TodoItemDocument : CosmosDbEntity
{
    public override string PartitionKey => TenantId.ToString();
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;

    // Embedded object — single nested JSON object
    public Schedule Schedule { get; private set; } = new();

    // Embedded collection — JSON array of objects
    public List<ChecklistItem> ChecklistItems { get; private set; } = [];

    // Dynamic key-value bag — JSON object with variable keys
    public Dictionary<string, string> Metadata { get; private set; } = [];

    // Deeply nested — objects within objects
    public AssigneeInfo Assignee { get; private set; } = new();
}

public class AssigneeInfo
{
    public string DisplayName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public TeamInfo Team { get; set; } = new();       // nested within nested
    public List<string> Roles { get; set; } = [];      // simple array
}
```

**Resulting JSON document in Cosmos DB:**
```json
{
  "id": "a1b2c3d4-...",
  "partitionKey": "tenant-guid",
  "tenantId": "tenant-guid",
  "title": "Implement login page",
  "schedule": {
    "startDate": "2026-03-01T00:00:00Z",
    "dueDate": "2026-03-15T00:00:00Z"
  },
  "checklistItems": [
    { "description": "Create wireframe", "isComplete": true, "sortOrder": 1 },
    { "description": "Write unit tests", "isComplete": false, "sortOrder": 2 }
  ],
  "metadata": {
    "source": "sprint-planning",
    "sprint": "sprint-12"
  },
  "assignee": {
    "displayName": "Jane Doe",
    "email": "jane@example.com",
    "team": {
      "name": "Frontend Team",
      "department": "Engineering"
    },
    "roles": ["Developer", "Reviewer"]
  }
}
```

#### Embedded Structure Rules

| Pattern | C# Type | JSON Shape | When to Use |
|---------|---------|------------|-------------|
| Single nested object | `Schedule` (POCO) | `{ "startDate": ..., "dueDate": ... }` | Value objects, date ranges, configuration sections |
| Collection of objects | `List<ChecklistItem>` | `[ { ... }, { ... } ]` | Child items that always load with parent, no independent access needed |
| Simple array | `List<string>`, `List<Guid>` | `[ "a", "b" ]` | Tags, labels, roles |
| Dictionary / dynamic bag | `Dictionary<string, string>` | `{ "key1": "val1" }` | Metadata, preferences, feature flags, variable attributes |
| Deeply nested | Object containing objects | `{ "a": { "b": { } } }` | When the domain naturally nests (assignee → team → department) |
| Mixed | Any combination above | Complex document | Real-world documents often combine all patterns |

> **SQL vs Cosmos DB nesting rule of thumb:** If a nested structure would require 3+ tables and JOINs in SQL, it's a strong signal to use Cosmos DB where the entire aggregate is one document. If nested children need independent querying, indexing, or cross-entity relationships, keep them in SQL as separate entities.

### Entity Rules

1. **Inherit `CosmosDbEntity`** — Provides `Id` (Guid), `RowVersion`, and the lowercase `id` property required by Cosmos DB
2. **Override `PartitionKey`** — Must return a string; typically `TenantId.ToString()` for multi-tenant, or a domain-specific partition strategy
3. **Container name = entity type name** — The repository uses `typeof(T).Name` as the container name automatically
4. **No EF configuration classes** — Cosmos DB is schema-less; no `IEntityTypeConfiguration<T>` needed
5. **No migrations** — Containers are created at runtime or via IaC; no EF migration chain
6. **Rich domain model still applies** — Factory `Create()`, private setters, `DomainResult<T>` validation

### Partition Key Design

| Strategy | Example | Best For |
|----------|---------|----------|
| Tenant-based | `TenantId.ToString()` | Multi-tenant with tenant-scoped queries |
| Entity-based | `Id.ToString()` | Point reads, single-document operations |
| Category-based | `Category` | Queries within a logical grouping |
| Composite | `$"{TenantId}:{Region}"` | Cross-dimensional queries |

> **Key rule:** Choose the partition key based on your most common query pattern. Cosmos DB is most efficient when queries target a single partition.

---

## Repository Pattern

### Interface

The repository interface is provided by the package:

```csharp
public interface ICosmosDbRepository
{
    Task<T> SaveItemAsync<T>(T item) where T : CosmosDbEntity;
    Task<T?> GetItemAsync<T>(string id, string partitionKey) where T : CosmosDbEntity;
    Task DeleteItemAsync<T>(string id, string partitionKey);
    Task DeleteItemAsync<T>(T item) where T : CosmosDbEntity;

    // LINQ-based paged query with filter and sort
    Task<(List<TProject>, int, string?)> QueryPageProjectionAsync<TSource, TProject>(
        string? continuationToken = null, int pageSize = 10,
        Expression<Func<TProject, bool>>? filter = null,
        List<Sort>? sorts = null, bool includeTotal = false,
        int maxConcurrency = -1, CancellationToken cancellationToken = default);

    // SQL-based paged query
    Task<(List<TProject>, int, string?)> QueryPageProjectionAsync<TSource, TProject>(
        string? continuationToken = null, int pageSize = 10,
        string? sql = null, string? sqlCount = null,
        Dictionary<string, object>? parameters = null,
        int maxConcurrency = -1, CancellationToken cancellationToken = default);

    // Container/database management
    Task<Container> GetOrAddContainerAsync(string containerId, string? partitionKeyPath = null);
    Task<HttpStatusCode?> DeleteContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<HttpStatusCode> SetOrCreateDatabaseAsync(string dbId, int? throughput = null, CancellationToken cancellationToken = default);
    Task<HttpStatusCode> DeleteDatabaseAsync(string? dbId = null, CancellationToken cancellationToken = default);
}
```

### Concrete Repository

Create a project-specific repository that inherits from the base:

```csharp
namespace {Project}.Infrastructure.Repositories;

public class {Project}CosmosDbRepository : CosmosDbRepositoryBase, I{Project}CosmosDbRepository
{
    public {Project}CosmosDbRepository(
        ILogger<{Project}CosmosDbRepository> logger,
        IOptions<{Project}CosmosDbRepositorySettings> settings)
        : base(logger, settings)
    {
    }
}

public interface I{Project}CosmosDbRepository : ICosmosDbRepository { }
```

### Settings

```csharp
namespace {Project}.Infrastructure.Repositories;

public class {Project}CosmosDbRepositorySettings : CosmosDbRepositorySettingsBase { }
```

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "CosmosDb1": ""
  },
  "{Project}CosmosDbRepositorySettings": {
    "CosmosDbId": "{project}-db"
  }
}
```

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "CosmosDb1": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
  }
}
```

> The Development connection string above is the well-known Cosmos DB Emulator default. For deployed environments, use managed identity or Key Vault.

---

## DI Registration (in Bootstrapper)

```csharp
// In RegisterInfrastructureServices
private static void AddCosmosDbServices(IServiceCollection services, IConfiguration config)
{
    // Register Azure CosmosClient via IAzureClientFactory
    services.AddAzureClients(builder =>
    {
        builder.AddCosmosServiceClient(config.GetConnectionString("CosmosDb1")!)
            .WithName("{Project}CosmosClient");
    });

    // Bind settings and inject CosmosClient
    services.Configure<{Project}CosmosDbRepositorySettings>(options =>
    {
        config.GetSection("{Project}CosmosDbRepositorySettings").Bind(options);
        // CosmosClient is resolved from the named client factory at registration time
    });

    // Register repository
    services.AddScoped<I{Project}CosmosDbRepository, {Project}CosmosDbRepository>();
}
```

### Alternative: Direct CosmosClient Injection

If not using `IAzureClientFactory`, inject `CosmosClient` directly in settings:

```csharp
services.Configure<{Project}CosmosDbRepositorySettings>(options =>
{
    config.GetSection("{Project}CosmosDbRepositorySettings").Bind(options);
    options.CosmosClient = new CosmosClient(config.GetConnectionString("CosmosDb1"));
});
```

---

## Service Layer Usage

Services consume Cosmos DB repositories exactly like SQL repositories — through the interface:

```csharp
public class {Entity}Service(
    I{Project}CosmosDbRepository cosmosRepo,
    IRequestContext<string, Guid?> requestContext,
    ILogger<{Entity}Service> logger) : I{Entity}Service
{
    public async Task<Result<{Entity}>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await cosmosRepo.GetItemAsync<{Entity}>(
            id.ToString(), requestContext.TenantId.ToString()!);
        return entity is not null ? Result<{Entity}>.Success(entity) : Result<{Entity}>.None();
    }

    public async Task<Result<{Entity}>> SaveAsync({Entity}Dto dto, CancellationToken ct = default)
    {
        return {Entity}.Create(requestContext.TenantId!.Value, dto.Name, dto.Description)
            .Map(async entity =>
            {
                var saved = await cosmosRepo.SaveItemAsync(entity);
                return Result<{Entity}>.Success(saved);
            });
    }

    public async Task<Result<PagedResponse<{Entity}>>> SearchAsync(
        SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default)
    {
        var tenantId = requestContext.TenantId!.Value;
        var (items, total, _) = await cosmosRepo.QueryPageProjectionAsync<{Entity}, {Entity}>(
            pageSize: request.PageSize,
            filter: e => e.TenantId == tenantId,
            includeTotal: true,
            cancellationToken: ct);

        return Result<PagedResponse<{Entity}>>.Success(new PagedResponse<{Entity}>
        {
            Data = items,
            TotalCount = total,
            PageSize = request.PageSize,
            Page = request.Page
        });
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await cosmosRepo.DeleteItemAsync<{Entity}>(
            id.ToString(), requestContext.TenantId.ToString()!);
        return Result.Success();
    }
}
```

---

## Querying Patterns

### LINQ-Based (Type-Safe)

```csharp
// Paged with filter and sort
var (items, total, continuationToken) = await cosmosRepo
    .QueryPageProjectionAsync<{Entity}, {Entity}Dto>(
        pageSize: 20,
        filter: e => e.TenantId == tenantId && e.Status == "Active",
        sorts: [new Sort { PropertyName = "CreatedDate", Direction = SortDirection.Descending }],
        includeTotal: true,
        cancellationToken: ct);
```

### SQL-Based (Complex Queries)

```csharp
// SQL with parameterized query
var (items, total, continuationToken) = await cosmosRepo
    .QueryPageProjectionAsync<{Entity}, {Entity}Dto>(
        pageSize: 20,
        sql: "SELECT * FROM c WHERE c.TenantId = @tenantId AND c.Status = @status",
        sqlCount: "SELECT VALUE COUNT(1) FROM c WHERE c.TenantId = @tenantId AND c.Status = @status",
        parameters: new Dictionary<string, object>
        {
            ["@tenantId"] = tenantId.ToString(),
            ["@status"] = "Active"
        },
        cancellationToken: ct);
```

### Streaming (Large Result Sets)

```csharp
// LINQ stream
IAsyncEnumerable<{Entity}> stream = cosmosRepo.GetStream<{Entity}>(
    filter: e => e.TenantId == tenantId);

await foreach (var item in stream)
{
    // Process each item without loading all into memory
}
```

---

## Aspire Integration

In `AppHost/Program.cs`:

```csharp
var cosmos = builder.AddAzureCosmosDB("CosmosDb1")
    .RunAsEmulator();  // Uses Cosmos DB Emulator locally

var cosmosDb = cosmos.AddDatabase("{project}-db");

var api = builder.AddProject<Projects.{Project}_Api>("{project}-api")
    .WithReference(cosmosDb);
```

---

## Differences from EF Core/SQL Data Access

| Concern | EF Core/SQL (`data-access.md`) | Cosmos DB (this skill) |
|---------|-------------------------------|----------------------|
| Entity base class | `EntityBase` | `CosmosDbEntity` (adds `PartitionKey`, `id`) |
| Configuration | `IEntityTypeConfiguration<T>` classes | None (schema-less) |
| DbContext | Split `DbContextTrxn` / `DbContextQuery` | None — direct repository |
| Repository base | `RepositoryBase<TContext, TAuditId, TTenantId>` | `CosmosDbRepositoryBase` |
| Migrations | EF Core `dotnet ef migrations` | None — containers created at runtime or via IaC |
| Tenant filter | Global query filter in `OnModelCreating` | Manual filter in LINQ/SQL queries |
| Paging | `PagedResponse<T>` via EF LINQ | `(List<T>, int, string?)` with continuation token |
| Transactions | EF `SaveChangesAsync` (unit of work) | Single-document atomic; cross-document requires stored procedures |
| Relationships | Navigation properties, FK constraints | Embedded documents or manual references |
| Updater pattern | `CollectionUtility.SyncCollectionWithResult` | Upsert entire document (`SaveItemAsync`) |

---

## Verification

After generating Cosmos DB data access code, confirm:

- [ ] Entity inherits `CosmosDbEntity` and overrides `PartitionKey`
- [ ] Repository inherits `CosmosDbRepositoryBase` with project-specific settings
- [ ] Settings class inherits `CosmosDbRepositorySettingsBase` with `CosmosDbId` configured
- [ ] DI registers `CosmosClient` (named or direct) and binds settings
- [ ] Connection string in `appsettings.json` with Cosmos DB Emulator default for Development
- [ ] Service layer uses `GetItemAsync` with `id` + `partitionKey` (not just `id`)
- [ ] Paged queries handle continuation tokens for multi-page results
- [ ] No EF configuration classes, no migrations, no DbContext for Cosmos entities
- [ ] Cross-references: entities match [domain-model.md](domain-model.md) rich entity rules; services follow [application-layer.md](application-layer.md) patterns
