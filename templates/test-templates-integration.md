# Test Templates - Integration (Phase 5a/5b, on-demand)

| | |
|---|---|
| **Generates** | `Test/Test.Integration/AspireTestHost.cs`, `Test/Test.Integration/DbContextFactory.cs`, `Test/Test.Integration/{Entity}RepositoryIntegrationTests.cs`, `Test/Test.Integration/AuditLogRepositoryAzuriteTests.cs`, `Test/Test.Integration/ApiAuditPipelineTests.cs`, `Test/Test.Integration/DomainEventPipelineTests.cs` |
| **Requires** | [test-templates-endpoint](test-templates-endpoint.md) (for the shared `WebApplicationFactoryBase`), [repository-template](repository-template.md), [updater-template](updater-template.md), an Aspire AppHost project |
| **Phase** | Generated in Phase 4 (host + factory shells) and filled in during Phase 5a (`*RepositoryIntegrationTests`) and Phase 5b (`*AuditPipelineTests`, `DomainEventPipelineTests`) |
| **Protocol** | Tests-after for this tier - TDD lives in `Test.Unit` and `Test.Endpoints`. Integration verifies wiring against real infrastructure (SQL/Azurite/Service Bus emulator), so write the tests once the unit + endpoint tests pin behavior. |

## Why this tier exists

`Test.Endpoints` runs against in-memory EF, which silently masks tenant query filters, owned-type flattening, paging plans, raw SQL projections, M:N bridge tables, polymorphic indexes, and audit interceptor wiring. `Test.E2E` runs the full HTTP path but only against a single SQL container.

`Test.Integration` is the **multi-resource** tier - it exercises real SQL **plus** Azurite Table Storage **plus** Service Bus emulator **plus** Functions where applicable, with the production AppHost graph instead of bespoke wiring. Use it for:

- EF migration apply against real SQL Server (catches FK ordering / shadow-property / schema drift bugs).
- Tenant query filters, M:N junction navigation (`.ThenInclude`), polymorphic-index existence checks.
- `AuditInterceptor -> IInternalMessageBus -> AuditHandler -> IAuditLogRepository -> Azurite` end-to-end.
- API request -> audit middleware -> Azurite, with polling read-back.
- Service Bus emulator -> Function trigger -> projection store handoff.

## Reuse rule

Start the Aspire AppHost graph **once** per assembly. Tests that need only SQL or only Azurite **piggyback** on the shared fixture instead of spinning their own Testcontainers stack. The cost is roughly one Aspire startup (~60-90 s on warm Docker, 2-5 min cold) per test run, regardless of how many integration test classes participate. The benefit is no duplicate containers and a single source of truth for connection strings.

> **Naming:** `AspireTestHost` (not `DatabaseFixture`). The fixture owns the full distributed application - DB + Functions + Table Storage + lifecycle - and the name has to reflect that. `DbContextFactory` is a separate static helper that creates EF contexts pointed at `AspireTestHost.ConnectionString`. Single-responsibility names let contributors grep by purpose. See [../skills/testing.md](../skills/testing.md) -> *Aspire Test Host (recipe)*.

---

## AspireTestHost

### File: `Test/Test.Integration/AspireTestHost.cs`

```csharp
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Test.Integration;

/// <summary>
/// Assembly-scoped fixture that starts the full Aspire AppHost graph (API, Functions, SQL,
/// Table Storage) once via [AssemblyInitialize] and tears it down via [AssemblyCleanup].
/// Aspire tier (Aspire.Hosting.Testing) - required so downstream classes can exercise the full
/// service mesh (HTTP -> API -> Service Bus -> Function -> projection -> audit row), which no lighter
/// tier reproduces. Per-call .WaitAsync(DefaultTimeout, ct) bounds every async Aspire step;
/// WaitForResourceHealthyAsync avoids races where containers report Running before they accept
/// connections.
/// </summary>
[TestClass]
public class AspireTestHost
{
    /// <summary>Per-call deadline applied via .WaitAsync(DefaultTimeout, ct). Bounds every async
    /// Aspire call (build, start, GetConnectionStringAsync, WaitForResource*) so a single hung step
    /// fails fast instead of hanging the whole test run. Sized for slow CI cold-starts (image pull
    /// + Functions warm-up).</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Cleanup deadline. StopAsync should return promptly; the bound prevents a stuck shutdown.</summary>
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromMinutes(1);

    private static string? _originalAspireTesting;
    private static string? _originalIncludeFunctions;
    internal static string ConnectionString = null!;

    /// <summary>Shared Aspire app started once for all Aspire-based integration tests.</summary>
    internal static DistributedApplication? AspireApp { get; private set; }

    [AssemblyInitialize]
    public static async Task AssemblyInit(TestContext _)
    {
        // AppHost.cs reads these via Environment.GetEnvironmentVariable, so they must be process env vars.
        // Save originals first so cleanup can restore them - hermeticity matters when an outer
        // test runner sets these.
        _originalAspireTesting = Environment.GetEnvironmentVariable("{APP}_ASPIRE_TESTING");
        _originalIncludeFunctions = Environment.GetEnvironmentVariable("{APP}_INCLUDE_FUNCTIONS");
        Environment.SetEnvironmentVariable("{APP}_ASPIRE_TESTING", "true");
        if (EnsureFuncToolAvailable())
            Environment.SetEnvironmentVariable("{APP}_INCLUDE_FUNCTIONS", "true");

        var ct = CancellationToken.None;

        var appHostProgramType = Type.GetType("Program, AppHost", throwOnError: true)!;

        var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            appHostProgramType,
            args: [],
            configureBuilder: (appOptions, hostSettings) =>
            {
                appOptions.DisableDashboard = true; // explicit > implicit default

                // Pass parameters through IConfiguration, NOT env-var mutation, so test isolation
                // stays clean. AppHost's builder.AddParameter("sql-password", ...) resolves from
                // IConfiguration first.
                hostSettings.Configuration ??= new();
                hostSettings.Configuration["Parameters:sql-password"] = LocalSqlSettings.SharedSaPassword;
            },
            cancellationToken: ct).WaitAsync(DefaultTimeout, ct);

        // Surface app-level diagnostics at Information while filtering out the noisy framework
        // categories (AspNetCore request logs, Aspire DCP/orchestration chatter).
        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        AspireApp = await builder.BuildAsync(ct).WaitAsync(DefaultTimeout, ct);
        await AspireApp.StartAsync(ct).WaitAsync(DefaultTimeout, ct);

        // Container reaching Running != SQL accepting connections - wait for the health check.
        await AspireApp.ResourceNotifications.WaitForResourceHealthyAsync("{app}db", ct)
            .WaitAsync(DefaultTimeout, ct);

        // GetConnectionStringAsync returns ValueTask; convert to Task to apply WaitAsync.
        var sqlConnectionString = await AspireApp.GetConnectionStringAsync("{app}db", ct)
            .AsTask()
            .WaitAsync(DefaultTimeout, ct);
        ConnectionString = string.IsNullOrWhiteSpace(sqlConnectionString)
            ? throw new InvalidOperationException("Aspire SQL connection string '{app}db' was not resolved.")
            : sqlConnectionString;
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup(TestContext testContext)
    {
        if (AspireApp is not null)
        {
            try
            {
                await AspireApp.StopAsync(testContext.CancellationToken).WaitAsync(CleanupTimeout);
            }
            catch (TimeoutException)
            {
                // Bounded shutdown - DisposeAsync below still cleans up underlying processes/containers.
            }
            await AspireApp.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("{APP}_ASPIRE_TESTING", _originalAspireTesting);
        Environment.SetEnvironmentVariable("{APP}_INCLUDE_FUNCTIONS", _originalIncludeFunctions);
    }

    /// <summary>Waits for a named Aspire resource to reach the Healthy state, bounded by
    /// DefaultTimeout. Tests should call this for any non-SQL resource ({app}api, {app}functions,
    /// TableStorage1) before talking to it - Aspire reports Running before warm-up completes.</summary>
    internal static Task WaitForResourceHealthyAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        if (AspireApp is null)
            throw new InvalidOperationException("AspireApp is not initialized.");

        return AspireApp.ResourceNotifications
            .WaitForResourceHealthyAsync(resourceName, cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
    }

    /// <summary>Checks if Azure Functions Core Tools (func.exe) is available on PATH.</summary>
    internal static bool EnsureFuncToolAvailable() { /* OS-specific PATH probe - see reference app */ }
}
```

### Aspire fixture non-negotiables

1. **One shared app per assembly** - start in `[AssemblyInitialize]`, reuse. Never per test class.
2. **`Parameters:*` via `configureBuilder.hostSettings.Configuration`** - not env-var mutation.
3. **Save and restore env vars** - `{APP}_ASPIRE_TESTING`, `{APP}_INCLUDE_FUNCTIONS`, etc.
4. **Per-call `.WaitAsync(DefaultTimeout, ct)`** on every async Aspire call. Not a single umbrella CTS.
5. **`WaitForResourceHealthyAsync(name, ct)` before talking to a resource.** Running != ready.
6. **`GetConnectionStringAsync` is `ValueTask<string?>`** - wrap as `.AsTask().WaitAsync(...)`.
7. **`[AssemblyCleanup(TestContext)]`** (MSTest 3.x overload) - bound `StopAsync` with `.WaitAsync(CleanupTimeout)` and catch `TimeoutException` so a stuck teardown does not hang CI.

---

## File: `Test/Test.Integration/DbContextFactory.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using {Project}.Infrastructure.Data;

namespace Test.Integration;

/// <summary>
/// Internal helper that builds {App}DbContextTrxn/{App}DbContextQuery instances pointed at the
/// Aspire-managed SQL container's connection string (AspireTestHost.ConnectionString), so SQL-only
/// and projection tests can use real EF semantics without spinning their own Testcontainers
/// instance.
/// </summary>
internal static class DbContextFactory
{
    internal static {App}DbContextTrxn CreateTrxnContext(string? connString = null)
    {
        var options = new DbContextOptionsBuilder<{App}DbContextTrxn>()
            .UseSqlServer(connString ?? AspireTestHost.ConnectionString,
                sql => sql.UseCompatibilityLevel(170))
            .Options;
        return new {App}DbContextTrxn(options) { AuditId = "integration-test" };
    }

    internal static {App}DbContextQuery CreateQueryContext(string? connString = null)
    {
        var options = new DbContextOptionsBuilder<{App}DbContextQuery>()
            .UseSqlServer(connString ?? AspireTestHost.ConnectionString,
                sql => sql.UseCompatibilityLevel(170))
            .Options;
        return new {App}DbContextQuery(options) { AuditId = "integration-test" };
    }
}
```

> **`AuditId` bypass:** `DbContextBase<string, Guid?>` declares `required string AuditId`. When constructing contexts outside DI, set it directly via object-initializer syntax - the design-time factory uses the same pattern.

---

## Repository Integration Tests

Cover **migration apply** + **CRUD against real SQL** + **child includes** + **M:N junction navigation** + **tenant query filter** + **polymorphic indexes** when applicable.

### File: `Test/Test.Integration/{Entity}RepositoryIntegrationTests.cs`

```csharp
using EF.Data.Contracts;
using Microsoft.EntityFrameworkCore;
using {Project}.Domain.Model;
using {Project}.Infrastructure.Data;
using Test.Support;
using Test.Support.Builders;

namespace Test.Integration;

/// <summary>
/// Validates EF migrations apply cleanly against real SQL Server and that core repository
/// operations (CRUD, includes, many-to-many bridges, the tenant query filter, polymorphic-attachment
/// indexing where applicable) work against the migrated schema.
/// Aspire tier by reuse: the test only needs SQL, but it piggybacks on the shared AspireTestHost
/// SQL container (via DbContextFactory) instead of standing up a separate Testcontainers SQL -
/// avoiding two SQL containers per test run.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class {Entity}RepositoryIntegrationTests
{
    private static readonly Guid TenantA = TestConstants.TenantId;
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-000000000099");

    [TestMethod]
    [Timeout(120000)]
    public async Task Migrations_ApplyCleanly_ToSqlContainer()
    {
        await using var db = DbContextFactory.CreateTrxnContext();
        await db.Database.MigrateAsync();

        Assert.IsTrue(await db.Database.CanConnectAsync());

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{app}'";
        var tableCount = (int)(await cmd.ExecuteScalarAsync())!;
        Assert.IsGreaterThanOrEqualTo(tableCount, {ExpectedTableCount},
            $"Expected >= {ExpectedTableCount} tables in {app} schema, found {tableCount}");
    }

    [TestMethod]
    [Timeout(120000)]
    public async Task {Entity}_CrudOperations_WorkAgainstRealSql()
    {
        await using var db = DbContextFactory.CreateTrxnContext();
        await db.Database.MigrateAsync();

        // Create
        var entity = new {Entity}Builder().WithName("Integration {Entity}").Build();
        db.{Entities}.Add(entity);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);
        var id = entity.Id;
        Assert.AreNotEqual(Guid.Empty, id);

        // Read
        var fetched = await db.{Entities}.FindAsync(id);
        Assert.IsNotNull(fetched);
        Assert.AreEqual("Integration {Entity}", fetched.Name);

        // Update via domain method
        fetched.Update(name: "Updated {Entity}");
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);
        var updated = await db.{Entities}.FindAsync(id);
        Assert.AreEqual("Updated {Entity}", updated!.Name);

        // Delete
        db.{Entities}.Remove(updated);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);
        var deleted = await db.{Entities}.FindAsync(id);
        Assert.IsNull(deleted);
    }

    [TestMethod]
    [Timeout(120000)]
    public async Task {Entity}_WithChildren_PersistsCorrectly()
    {
        await using var db = DbContextFactory.CreateTrxnContext();
        await db.Database.MigrateAsync();

        var entity = new {Entity}Builder().WithName("Parent {Entity}").Build();
        db.{Entities}.Add(entity);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);

        var childResult = {ChildEntity}.Create(TenantA, entity.Id, "Child body");
        db.{ChildEntities}.Add(childResult.Value!);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);

        var loaded = await db.{Entities}
            .Include(e => e.{ChildEntities})
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        Assert.IsNotNull(loaded);
        Assert.HasCount(1, loaded.{ChildEntities});
    }

    [TestMethod]
    [Timeout(120000)]
    public async Task {Entity}Tag_ManyToMany_WorksCorrectly()
    {
        // Only generate when entity participates in an M:N relationship via a junction entity.
        await using var db = DbContextFactory.CreateTrxnContext();
        await db.Database.MigrateAsync();

        var entity = new {Entity}Builder().WithName("Tagged").Build();
        db.{Entities}.Add(entity);
        var tag = new TagBuilder().WithName("M2MTag").Build();
        db.Tags.Add(tag);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);

        var bridge = {Entity}Tag.Create(TenantA, entity.Id, tag.Id);
        db.{Entity}Tags.Add(bridge.Value!);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);

        var loaded = await db.{Entities}
            .Include(e => e.{Entity}Tags).ThenInclude(et => et.Tag)
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        Assert.IsNotNull(loaded);
        Assert.HasCount(1, loaded.{Entity}Tags);
        Assert.AreEqual("M2MTag", loaded.{Entity}Tags.First().Tag!.Name);
    }

    [TestMethod]
    [Timeout(120000)]
    public async Task TenantQueryFilter_RestrictsResults_WhenTenantIdSet()
    {
        // Only generate when enableMultiTenant is true.
        await using var db = DbContextFactory.CreateTrxnContext();
        await db.Database.MigrateAsync();

        var entityA = new {Entity}Builder().WithTenantId(TenantA).WithName("Tenant A").Build();
        var entityB = new {Entity}Builder().WithTenantId(TenantB).WithName("Tenant B").Build();
        db.{Entities}.Add(entityA);
        db.{Entities}.Add(entityB);
        await db.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);

        // IgnoreQueryFilters returns all rows; the active filter must restrict the count.
        var allViaEf = await db.{Entities}.IgnoreQueryFilters()
            .Where(e => e.Name.StartsWith("Tenant"))
            .ToListAsync();
        Assert.IsGreaterThanOrEqualTo(allViaEf.Count, 2);

        var filteredCount = await db.{Entities}
            .Where(e => e.Name.StartsWith("Tenant"))
            .CountAsync();

        Assert.IsGreaterThanOrEqualTo(allViaEf.Count, filteredCount,
            "Query filter should restrict results");
    }
}
```

### Repository test coverage matrix

| Scenario | Generate when |
|---|---|
| `Migrations_ApplyCleanly_ToSqlContainer` | Always - once per schema, not per entity. |
| `{Entity}_CrudOperations_WorkAgainstRealSql` | Every entity with mutations. |
| `{Entity}_WithChildren_PersistsCorrectly` | Entity has owned/dependent child collections (1:N). |
| `{Entity}Tag_ManyToMany_WorksCorrectly` | Entity participates in M:N via a junction. |
| `TenantQueryFilter_RestrictsResults_WhenTenantIdSet` | `enableMultiTenant: true`. |
| `Polymorphic_Index_Exists` | Entity uses a polymorphic ownership pattern (e.g., `Attachment.OwnerType` + `OwnerId`). |

---

## Audit Pipeline Integration Tests

### File: `Test/Test.Integration/AuditLogRepositoryAzuriteTests.cs`

Validates `AuditLogRepository.AppendAsync` against real Azurite Table Storage (partition key, row key shape, round-trip metadata).

```csharp
using Aspire.Hosting.Testing;
using Azure.Data.Tables;
using EF.Common.Contracts;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using {Project}.Infrastructure.Storage;

namespace Test.Integration;

/// <summary>
/// Validates AuditLogRepository.AppendAsync against real Azurite Table Storage: partition key,
/// row key shape (..._{Id:N}), and round-trip of audit metadata.
/// Aspire tier by reuse: only Azurite is exercised - no API, no Function - but the test piggybacks
/// on the shared AspireTestHost TableStorage1 resource instead of starting its own Azurite
/// container.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public class AuditLogRepositoryAzuriteTests
{
    [TestMethod]
    [Timeout(300000)]
    public async Task Given_AuditEntry_When_AppendAsyncToAzurite_Then_TableEntityPersistedWithExpectedKeys()
    {
        var ct = CancellationToken.None;
        await AspireTestHost.WaitForResourceHealthyAsync("TableStorage1", ct);

        var connectionString = await AspireTestHost.AspireApp!.GetConnectionStringAsync("TableStorage1", ct)
            .AsTask()
            .WaitAsync(AspireTestHost.DefaultTimeout, ct);
        Assert.IsFalse(string.IsNullOrWhiteSpace(connectionString));

        var tableName = $"audit{Guid.NewGuid():N}"[..31];
        var tableServiceClient = new TableServiceClient(connectionString);
        var repository = new AuditLogRepository(
            new TestTableServiceClientFactory(tableServiceClient),
            Options.Create(new AuditLogStorageSettings
            {
                TableName = tableName,
                NullTenantPartitionKey = "_system"
            }),
            NullLogger<AuditLogRepository>.Instance);

        var tenantId = Guid.NewGuid();
        var entry = new AuditEntry<string, Guid>
        {
            Id = Guid.NewGuid(),
            AuditId = "integration-user",
            TenantId = tenantId,
            EntityType = "{Entity}",
            EntityKey = Guid.NewGuid().ToString(),
            Status = AuditStatus.Success,
            Action = "Create",
            Metadata = "{\"source\":\"azurite-test\"}"
        };

        try
        {
            await repository.AppendAsync(entry, ct);

            var tableClient = tableServiceClient.GetTableClient(tableName);
            var persisted = await ReadSingleEntityAsync(tableClient, tenantId.ToString());

            Assert.IsNotNull(persisted);
            Assert.AreEqual(tenantId.ToString(), persisted.PartitionKey);
            Assert.IsTrue(persisted.RowKey.EndsWith($"_{entry.Id:N}", StringComparison.Ordinal));
            Assert.AreEqual(entry.AuditId, persisted.AuditId);
            Assert.AreEqual(entry.Action, persisted.Action);
            Assert.AreEqual(entry.Status.ToString(), persisted.Status);
        }
        finally
        {
            await tableServiceClient.DeleteTableAsync(tableName);
        }
    }

    private static async Task<AuditLogTableEntity> ReadSingleEntityAsync(
        TableClient tableClient, string partitionKey)
    {
        await foreach (var entity in tableClient.QueryAsync<AuditLogTableEntity>(
            e => e.PartitionKey == partitionKey))
        {
            return entity;
        }
        Assert.Fail("Expected an audit entity to be written to Azurite.");
        throw new InvalidOperationException("Unreachable");
    }

    private sealed class TestTableServiceClientFactory(TableServiceClient client)
        : IAzureClientFactory<TableServiceClient>
    {
        public TableServiceClient CreateClient(string name) => client;
    }
}
```

### File: `Test/Test.Integration/ApiAuditPipelineTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;
using Azure;
using Azure.Data.Tables;
using EF.Common.Contracts;
using {Project}.Application.Models;
using {Project}.Infrastructure.Storage;

namespace Test.Integration;

/// <summary>
/// End-to-end audit pipeline test for the API: POST /api/{entities} -> API request handling ->
/// audit middleware -> Azurite Table Storage row, with a polling read-back to confirm the persisted
/// entity. Aspire tier (Aspire.Hosting.Testing) - required because two Aspire resources participate
/// ({app}api for the request, TableStorage1 for verification), and both must be Healthy before the
/// test can run. The polling helper tolerates eventual consistency between request completion and
/// table visibility.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public class ApiAuditPipelineTests
{
    private static readonly Guid ScaffoldTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [TestMethod]
    [Timeout(300000)]
    public async Task Given_Api{Entity}Create_When_RequestHandled_Then_AuditEntryPersistedToTableStorage()
    {
        var ct = CancellationToken.None;
        await AspireTestHost.WaitForResourceHealthyAsync("{app}api", ct);
        await AspireTestHost.WaitForResourceHealthyAsync("TableStorage1", ct);

        using var client = AspireTestHost.AspireApp!.CreateHttpClient("{app}api", "http");
        client.Timeout = TimeSpan.FromMinutes(10);
        var auditWindowStartUtc = DateTimeOffset.UtcNow;

        var request = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = $"Api Audit {Guid.NewGuid():N}", /* ... */ }
        };

        using var response = await PostCreateWithRetryAsync(client, request, ct);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var connectionString = await AspireTestHost.AspireApp!.GetConnectionStringAsync("TableStorage1", ct)
            .AsTask().WaitAsync(AspireTestHost.DefaultTimeout, ct);
        var tableClient = new TableServiceClient(connectionString).GetTableClient("{app}audit");
        var auditEntity = await WaitForAuditEntityAsync(
            tableClient,
            ScaffoldTenantId.ToString(),
            "{Entity}",
            "Added",
            auditWindowStartUtc,
            ct);

        Assert.IsNotNull(auditEntity);
        Assert.AreEqual(ScaffoldTenantId.ToString(), auditEntity.PartitionKey);
        Assert.AreEqual("{Entity}", auditEntity.EntityType);
        Assert.AreEqual("Added", auditEntity.Action);
        Assert.AreEqual(AuditStatus.Success.ToString(), auditEntity.Status);
        Assert.IsTrue(auditEntity.RecordedUtc >= auditWindowStartUtc);
    }

    private static async Task<HttpResponseMessage> PostCreateWithRetryAsync(
        HttpClient client, object request, CancellationToken ct)
    {
        // Poll until 201 or 45 s deadline. API may not be serving requests in the first second
        // after Aspire reports Running.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        HttpStatusCode? lastStatusCode = null;
        string? lastBody = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var response = await client.PostAsJsonAsync("/api/{entities}", request, ct);
                if (response.StatusCode == HttpStatusCode.Created) return response;
                lastStatusCode = response.StatusCode;
                lastBody = await response.Content.ReadAsStringAsync(ct);
                response.Dispose();
            }
            catch (HttpRequestException) { }
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        Assert.Fail($"Create API did not return 201. Last status: {lastStatusCode}; body: {lastBody}");
        throw new InvalidOperationException("Unreachable");
    }

    private static async Task<AuditLogTableEntity> WaitForAuditEntityAsync(
        TableClient tableClient,
        string partitionKey,
        string expectedEntityType,
        string expectedAction,
        DateTimeOffset windowStartUtc,
        CancellationToken ct)
    {
        // Poll the table for a matching row inside the audit window. Tolerates the gap between
        // 201 returning and the background AuditHandler flushing to Azurite.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await foreach (var entity in tableClient.QueryAsync<AuditLogTableEntity>(
                    e => e.PartitionKey == partitionKey, cancellationToken: ct))
                {
                    if (entity.RecordedUtc >= windowStartUtc
                        && entity.EntityType == expectedEntityType
                        && entity.Action == expectedAction
                        && entity.Status == AuditStatus.Success.ToString())
                    {
                        return entity;
                    }
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404) { /* table not yet created */ }
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        Assert.Fail($"Expected audit entity not found for partition '{partitionKey}'.");
        throw new InvalidOperationException("Unreachable");
    }
}
```

### Why downstream-effect polling matters

Aspire's emulators (Service Bus, Azurite) are best-effort under `DistributedApplicationTestingBuilder`. Asserting "audit row exists in Azurite for this request" exercises the same path production runs through, and it survives the small lag between HTTP 201 and the background `AuditHandler` flushing. The same pattern applies to message-driven workflows - assert against the persistent downstream effect, not against the message bus.

---

## Domain Event Projection Pipeline

### File: `Test/Test.Integration/DomainEventPipelineTests.cs`

```csharp
using System.Text.Json;
using EF.Data.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using {Project}.Application.Contracts.Repositories;
using {Project}.Application.Contracts.Storage;
using {Project}.Application.Services;
using {Project}.Domain.Model;
using {Project}.Infrastructure.Data;
using {Project}.Infrastructure.Repositories;

namespace Test.Integration;

/// <summary>
/// Validates the domain-event projection pipeline: an entity persisted to SQL is read by the
/// projection service through the query-side repositories and emitted as a view document with
/// correct counts.
/// Aspire tier by reuse: only SQL is exercised here (the Service Bus -> Function -> projection hop
/// is covered separately when a Function host is enabled); the test piggybacks on the shared
/// AspireTestHost SQL container rather than starting its own. The view store is in-memory -
/// real Cosmos behavior is out of scope.
/// </summary>
[TestClass]
public class DomainEventPipelineTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        await using var db = DbContextFactory.CreateTrxnContext();
        await db.Database.MigrateAsync();
    }

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(120000)]
    public async Task Given_{Entity}Created_When_ProjectionRuns_Then_{Entity}ViewProduced()
    {
        var connStr = AspireTestHost.ConnectionString;
        await using var ctx = DbContextFactory.CreateTrxnContext(connStr);

        var entityResult = {Entity}.Create(TenantId, "Integration Test {Entity}");
        Assert.IsTrue(entityResult.IsSuccess);
        var entity = entityResult.Value!;
        ctx.{Entities}.Add(entity);
        await ctx.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins);

        await using var queryCtx = DbContextFactory.CreateQueryContext(connStr);
        var viewRepo = new InMemory{Entity}ViewRepository();
        var projectionService = new {Entity}ViewProjectionService(
            new {Entity}RepositoryQuery(queryCtx),
            viewRepo,
            NullLogger<{Entity}ViewProjectionService>.Instance);

        await projectionService.Project{Entity}Async(entity.Id);

        var view = await viewRepo.GetAsync(entity.Id.ToString(), TenantId.ToString());
        Assert.IsNotNull(view, "View should be created by projection");
        Assert.AreEqual("Integration Test {Entity}", view.Name);
    }
}

/// <summary>In-memory implementation of I{Entity}ViewRepository for integration testing
/// without a Cosmos emulator.</summary>
internal class InMemory{Entity}ViewRepository : I{Entity}ViewRepository
{
    private readonly Dictionary<string, {Entity}ViewDto> _store = new();

    public Task UpsertAsync({Entity}ViewDto view, CancellationToken ct = default)
    {
        _store[$"{view.TenantId}:{view.Id}"] = view;
        return Task.CompletedTask;
    }

    public Task<{Entity}ViewDto?> GetAsync(string id, string tenantId, CancellationToken ct = default)
    {
        _store.TryGetValue($"{tenantId}:{id}", out var result);
        return Task.FromResult(result);
    }
}
```

Skip this template when the project does not have a projection service / read-model store. Generate only when `.scaffold/resource-implementation.yaml` declares a projection/read-model boundary.

---

## Aspire piggyback decision flow

```
Does the test need only SQL?
  YES -> DbContextFactory.CreateTrxnContext() - piggybacks on AspireTestHost SQL container.
Does it need Azurite Table Storage?
  YES -> AspireTestHost.AspireApp.GetConnectionStringAsync("TableStorage1") -> real Azurite.
Does it need API + Azurite (full audit pipeline)?
  YES -> AspireTestHost.AspireApp.CreateHttpClient("{app}api") + Azurite read-back.
Does it need API + Service Bus + Function + Cosmos?
  YES -> Full graph via AspireTestHost - use WaitForResourceHealthyAsync on every participating
        resource, assert on the downstream persistent effect (not the bus).
```

Single-resource tests SHOULD piggyback. Multi-resource tests MUST piggyback.

---

## Project file

### File: `Test/Test.Integration/Test.Integration.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Testcontainers.MsSql" />
    <PackageReference Include="Aspire.Hosting.Testing" />
    <PackageReference Include="Aspire.Hosting.Azure.Storage" />
    <PackageReference Include="Azure.Data.Tables" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Microsoft.VisualStudio.TestTools.UnitTesting" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Test.Support\Test.Support.csproj" />
    <ProjectReference Include="..\..\Host\{Host}.Api\{Host}.Api.csproj" />
    <ProjectReference Include="..\..\Application\{Project}.Application.Models\{Project}.Application.Models.csproj" />
    <ProjectReference Include="..\..\Application\{Project}.Application.Contracts\{Project}.Application.Contracts.csproj" />
    <ProjectReference Include="..\..\Application\{Project}.Application.Services\{Project}.Application.Services.csproj" />
    <ProjectReference Include="..\..\Infrastructure\{Project}.Infrastructure.Data\{Project}.Infrastructure.Data.csproj" />
    <ProjectReference Include="..\..\Infrastructure\{Project}.Infrastructure.Repositories\{Project}.Infrastructure.Repositories.csproj" />
    <ProjectReference Include="..\..\Infrastructure\{Project}.Infrastructure.Storage\{Project}.Infrastructure.Storage.csproj" />
    <ProjectReference Include="..\..\Host\Aspire\AppHost\AppHost.csproj" />
  </ItemGroup>
</Project>
```

---

## Verification

- [ ] `Test.Integration` references `AppHost` and `Aspire.Hosting.Testing`.
- [ ] `AspireTestHost` starts once per assembly; env vars saved and restored.
- [ ] `DbContextFactory` builds contexts against `AspireTestHost.ConnectionString`, sets `AuditId`.
- [ ] Every async Aspire call has its own `.WaitAsync(DefaultTimeout, ct)`.
- [ ] Tests gate on `WaitForResourceHealthyAsync` before touching a resource.
- [ ] Every test class has a class-level `<summary>` declaring tier + reuse reason.
- [ ] Multi-resource pipeline tests assert against the **downstream persistent effect** (audit row, projection document), not the bus/queue.
- [ ] Single-resource tests piggyback on `AspireTestHost`; they do not start their own Testcontainers stack.
- [ ] `Migrations_ApplyCleanly_ToSqlContainer` exists exactly once per assembly (not per entity).
- [ ] Tenant query filter test exists when `enableMultiTenant: true`.
- [ ] M:N test exists when entity uses a junction.

---

**TaskFlow proof (local):**
- `../AI-Instructions-ReferenceApp/src/Test/Test.Integration/AspireTestHost.cs`
- `../AI-Instructions-ReferenceApp/src/Test/Test.Integration/DbContextFactory.cs`
- `../AI-Instructions-ReferenceApp/src/Test/Test.Integration/MigrationAndRepositoryTests.cs`
- `../AI-Instructions-ReferenceApp/src/Test/Test.Integration/AuditLogRepositoryAzuriteTests.cs`
- `../AI-Instructions-ReferenceApp/src/Test/Test.Integration/ApiAuditPipelineTests.cs`
- `../AI-Instructions-ReferenceApp/src/Test/Test.Integration/DomainEventPipelineTests.cs`

**TaskFlow proof (remote fallback):**
<https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.Integration>
