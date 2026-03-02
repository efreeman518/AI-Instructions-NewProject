# Test Template — Integration

See [skills/testing.md](../skills/testing.md) for testing strategy and profile selection.

## Common Setup

Keep API factory/bootstrap and DB reset helpers centralized; integration classes should only contain scenario setup and assertions.

## Endpoint Tests

### File: `Test/Test.Integration/CustomApiFactory.cs`

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

### File: `Test/Test.Integration/EndpointTestBase.cs`

```csharp
public abstract class EndpointTestBase : DbIntegrationTestBase
{
    protected static async Task<HttpClient> GetHttpClient(params DelegatingHandler[] handlers) { /* shared client factory */ }
}
```

### File: `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs`

```csharp
[TestClass]
public class {Entity}EndpointsTests : EndpointTestBase
{
    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task Search_ReturnsFilteredResults()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();

        // Seed — create two entities
        var createDto1 = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = "SearchTarget", TenantId = tenantId }
        };
        var createDto2 = new DefaultRequest<{Entity}Dto>
        {
            Item = new {Entity}Dto { Name = "OtherItem", TenantId = tenantId }
        };
        await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}", createDto1);
        await client.PostAsJsonAsync($"v1/tenant/{tenantId}/{entities}", createDto2);

        // Act — search with filter
        var searchRequest = new SearchRequest<{Entity}SearchFilter>
        {
            Page = 1,
            PageSize = 10,
            Filter = new {Entity}SearchFilter { SearchTerm = "SearchTarget" }
        };
        var response = await client.PostAsJsonAsync(
            $"v1/tenant/{tenantId}/{entities}/search", searchRequest);

        // Assert — 200 with filtered results
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<{Entity}Dto>>();
        Assert.IsNotNull(page);
        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("SearchTarget", page.Items.First().Name);
    }

    [TestCategory("Endpoint")]
    [TestCategory("Integration")]
    [TestMethod]
    public async Task GetById_NotFound_Returns404()
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
    public async Task CRUD_Pass()
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
    public async Task GetPage_ReturnsOk()
    {
        // Arrange
        using var client = await GetHttpClient();
        var tenantId = Guid.NewGuid();
        var searchRequest = new SearchRequest<{Entity}SearchFilter>
        {
            Page = 1,
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

### File: `Test/Test.Integration/appsettings-test.json`

```json
{
  "TestSettings": {
    "DBSource": "UseInMemoryDatabase",
    "DBName": "Test.Integration.TestDB"
  }
}
```

## Contention/Concurrency Scenario (Optional)

For inventory/reservation/financial flows, add an integration test that runs parallel operations against the same aggregate and asserts:

- optimistic concurrency behavior is enforced
- no duplicate side effects (`no-oversell`, `no-double-reserve`, `no-double-charge`)
- retries/merge behavior is deterministic

## Test.Support Infrastructure

### File: `Test/Test.Support/UnitTestBase.cs`

```csharp
public abstract class UnitTestBase
{
    protected readonly MockRepository _mockFactory =
        new(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
}
```

### File: `Test/Test.Support/InMemoryDbBuilder.cs`

```csharp
public class InMemoryDbBuilder
{
    public InMemoryDbBuilder SeedDefaultEntityData() { /* flag */ return this; }
    public InMemoryDbBuilder UseEntityData(Action<DbContext> seedAction) { /* add seed */ return this; }
    public T BuildInMemory<T>(string? dbName = null) where T : DbContext { /* create + seed */ }
    public T BuildSQLite<T>() where T : DbContext { /* sqlite in-memory + EnsureCreated */ }
}
```

### File: `Test/Test.Support/DbSupport.cs`

```csharp
public static class DbSupport
{
    public static void ConfigureServicesTestDB<TTrxn, TQuery>(
        IServiceCollection services,
        string? dbConnectionString,
        string dbName = "TestDB")
        where TTrxn : DbContext where TQuery : DbContext
    {
        // in-memory or sqlserver wiring + no-tracking query context
    }
}
```

### File: `Test/Test.Support/Utility.cs`

```csharp
public static class Utility
{
    public static IConfigurationBuilder BuildConfiguration(string? path = "appsettings.json", bool includeEnvironmentVars = true) { }
    public static string RandomString(int length) { }
}
```
