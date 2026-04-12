# Test Templates — Endpoint (Phase 5b)

| | |
|---|---|
| **Generates** | `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs` |
| **Requires** | [endpoint-template](endpoint-template.md), CustomApiFactory from Phase 4, DTOs from Phase 4 |
| **Phase** | 5b (App Core TDD) |
| **Protocol** | Write these tests BEFORE implementing endpoints. See [../ai/tdd-protocol.md](../ai/tdd-protocol.md). |

## BDD Naming Convention

All test methods use `Given_When_Then`:
```csharp
[TestMethod]
public async Task Given_ValidPayload_When_PostEntity_Then_Returns201() { }
```

---

## CustomApiFactory

Generated in Phase 4. Located at `Test/Test.Integration/CustomApiFactory.cs`:

```csharp
public class CustomApiFactory<TProgram>(string? dbConnectionString = null)
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development").ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            DbSupport.ConfigureServicesTestDB<{App}DbContextTrxn, {App}DbContextQuery>(
                services,
                dbConnectionString,
                "Test.Integration.TestDB");
        });
    }
}
```

### Pooled DbContext Swap — Required Removals

When the Bootstrapper uses `AddPooledDbContextFactory` + `DbContextScopedFactory` + `AuditInterceptor` (per [data-layer-wiring.md](../patterns/data-layer-wiring.md)), the test factory's `ConfigureServices` (or `DbSupport`) must remove **all** of the following before re-registering in-memory contexts:

| Registration to Remove | Why |
|---|---|
| `AuditInterceptor<string, Guid?>` | Depends on `IInternalMessageBus` (EF.BackgroundServices), not registered in test host |
| `ConnectionNoLockInterceptor` | SQL-only interceptor, incompatible with InMemory provider |
| `IDbContextPool<T>` (internal) | Pooled factory creates singleton pools that conflict with scoped in-memory options |
| `DbContextScopedFactory<T, string, Guid?>` | Wraps `IDbContextFactory<T>` — must be removed and re-registered or left for re-resolution |
| `IDbContextFactory<T>` | Original pooled factory — must be replaced with in-memory factory |
| `DbContextOptions<T>` + `DbContextOptions` | Pool-registered options conflict with new in-memory options |

**Critical details:**
1. **Typed options per context.** Use `new DbContextOptionsBuilder<{App}DbContextTrxn>().UseInMemoryDatabase(name).Options` — do NOT use generic `DbContextOptions` when multiple contexts exist. `DbContextBase` constructors take `DbContextOptions` (non-generic base), but EF validates the generic type at runtime.
2. **Required member bypass.** `DbContextBase<TAuditIdType, TTenantIdType>` declares `required` members (e.g., `AuditId`). Use `ConstructorInfo.Invoke()` via reflection to bypass compile-time `required` enforcement when creating contexts from a test factory.
3. **Re-provide `IDbContextFactory<T>`.** `DbContextScopedFactory` resolves `IDbContextFactory<T>` — provide a test implementation that creates in-memory contexts.

```csharp
// Example: reflection-based context factory for required-member bypass
internal sealed class TestDbContextFactory<TContext>(DbContextOptions options)
    : IDbContextFactory<TContext> where TContext : DbContext
{
    public TContext CreateDbContext()
    {
        var genericOptionsType = typeof(DbContextOptions<>).MakeGenericType(typeof(TContext));
        var ctor = typeof(TContext).GetConstructor([genericOptionsType])
                ?? typeof(TContext).GetConstructor([typeof(DbContextOptions)]);
        return (TContext)ctor!.Invoke([options]);
    }
}
```

---

## Endpoint Tests

### File: `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs`

```csharp
[TestClass]
public class {Entity}EndpointsTests : EndpointTestBase
{
    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task Given_ValidPayload_When_PostEntity_Then_Returns201()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();
        var createDto = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = "NewEntity", TenantId = tenantId }
        };

        // Act
        var response = await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}", createDto);

        // Assert
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DefaultResponse<{Entity}Dto>>();
        Assert.IsNotNull(created?.Item);
        Assert.AreEqual("NewEntity", created.Item.Name);
    }

    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task Given_NonExistentId_When_GetEntity_Then_Returns404()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"v1/tenant/{tenantId}/{entities}/{nonExistentId}");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.IsNotNull(problemDetails);
        Assert.AreEqual(404, problemDetails.Status);
    }

    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task Given_ExistingEntities_When_SearchWithFilter_Then_ReturnsFilteredPage()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();

        // Seed
        var create1 = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = "SearchTarget", TenantId = tenantId }
        };
        var create2 = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = "OtherItem", TenantId = tenantId }
        };
        await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}", create1);
        await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}", create2);

        // Act
        var searchRequest = new SearchRequest<{Entity}SearchFilter>
        {
            PageIndex = 1,
            PageSize = 10,
            Filter = new {Entity}SearchFilter { SearchTerm = "SearchTarget" }
        };
        var response = await client.PostAsJsonAsync(
            $"v1/tenant/{tenantId}/{entities}/search", searchRequest);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<{Entity}Dto>>();
        Assert.IsNotNull(page);
        Assert.AreEqual(1, page.Total);
        Assert.AreEqual("SearchTarget", page.Data.First().Name);
    }

    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task Given_FullCrudCycle_When_AllOperationsExecuted_Then_AllSucceed()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();

        // Create
        var createDto = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = "CrudTest", TenantId = tenantId }
        };
        var createResponse = await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}", createDto);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DefaultResponse<{Entity}Dto>>();
        var entityId = created!.Item!.Id;

        // Read
        var getResponse = await client.GetAsync($"v1/tenant/{tenantId}/{entities}/{entityId}");
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);

        // Update
        var updateDto = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Id = entityId, Name = "Updated", TenantId = tenantId }
        };
        var updateResponse = await client.PutAsJsonAsync($"v1/tenant/{tenantId}/{entities}/{entityId}", updateDto);
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);

        // Delete
        var deleteResponse = await client.DeleteAsync($"v1/tenant/{tenantId}/{entities}/{entityId}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify deleted
        var verifyResponse = await client.GetAsync($"v1/tenant/{tenantId}/{entities}/{entityId}");
        Assert.AreEqual(HttpStatusCode.NotFound, verifyResponse.StatusCode);
    }

    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task Given_EmptyDatabase_When_SearchExecuted_Then_ReturnsEmptyPage()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();
        var searchRequest = new SearchRequest<{Entity}SearchFilter>
        {
            PageIndex = 1,
            PageSize = 10,
            Filter = new {Entity}SearchFilter()
        };

        // Act
        var response = await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}/search", searchRequest);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<{Entity}Dto>>();
        Assert.IsNotNull(page);
    }
}
```

---

## Test Configuration

### File: `Test/Test.Integration/appsettings-test.json`

```json
{
  "TestSettings": {
    "DBSource": "UseInMemoryDatabase",
    "DBName": "Test.Integration.TestDB"
  }
}
```

---

## Contention/Concurrency Scenario (Optional)

For high-contention domains (inventory, reservations, financial flows), add:

```csharp
[TestCategory("Endpoint")]
[TestCategory("Integration")]
[TestMethod]
public async Task Given_ConcurrentUpdates_When_Executed_Then_OptimisticConcurrencyEnforced()
{
    // Run parallel operations against the same entity
    // Assert: no duplicate side effects, concurrency behavior enforced
}
```
