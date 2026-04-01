# Test Templates

| | |
|---|---|
| **Generates** | `Test/Test.Unit/**`, `Test/Test.Integration/**`, `Test/Test.PlaywrightUI/**`, `Test/Test.Architecture/**`, `Test/Test.Load/**`, `Test/Test.Benchmarks/**` |
| **Requires** | [service-template](service-template.md), [repository-template](repository-template.md), [data-mapping-template](data-mapping-template.md), [data-mapping-template](data-mapping-template.md), [endpoint-template](endpoint-template.md) |

See [skills/testing.md](../skills/testing.md) for testing strategy and profile selection.

## Testing Strategy Overview

Tests are organized in four tiers, each building on the previous:

1. **Unit** — Fast, isolated tests with mocked dependencies. Covers domain entity logic, service orchestration, repository queries (in-memory DB), domain rules, and mappers.
2. **Integration** — Tests that exercise real HTTP endpoints through `WebApplicationFactory` with an in-memory or SQLite database. Covers CRUD flows, search/filter, error responses, and optional concurrency scenarios.
3. **E2E** — Playwright browser tests driven through Page Objects. Covers full user-facing flows (create/edit/delete, search, validation errors) against a running application.
4. **Quality Gates** — Architecture dependency tests (NetArchTest), load tests (NBomber), and microbenchmarks (BenchmarkDotNet). Enforces structural rules and performance baselines.

## Common Setup

### InMemoryDbBuilder

Shared between unit repository tests and integration tests for seeding in-memory or SQLite databases.

#### File: `Test/Test.Support/InMemoryDbBuilder.cs`

```csharp
public class InMemoryDbBuilder
{
    public InMemoryDbBuilder SeedDefaultEntityData() { /* flag */ return this; }
    public InMemoryDbBuilder UseEntityData(Action<DbContext> seedAction) { /* add seed */ return this; }
    public T BuildInMemory<T>(string? dbName = null) where T : DbContext { /* create + seed */ }
    public T BuildSQLite<T>() where T : DbContext { /* sqlite in-memory + EnsureCreated */ }
}
```

### UnitTestBase

Base class for unit tests providing a shared `MockRepository` with default mock behavior.

#### File: `Test/Test.Support/UnitTestBase.cs`

```csharp
public abstract class UnitTestBase
{
    protected readonly MockRepository _mockFactory =
        new(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
}
```

### EndpointTestBase

Base class for integration endpoint tests providing HTTP client factory and DB reset helpers.

#### File: `Test/Test.Integration/EndpointTestBase.cs`

```csharp
public abstract class EndpointTestBase : DbIntegrationTestBase
{
    protected static async Task<HttpClient> GetHttpClient(params DelegatingHandler[] handlers) { /* shared client factory */ }
}
```

### DbSupport

Wires up in-memory or SQL Server test databases and configures no-tracking query contexts.

#### File: `Test/Test.Support/DbSupport.cs`

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

### Utility

Configuration builder and random-string helper used across all test tiers.

#### File: `Test/Test.Support/Utility.cs`

```csharp
public static class Utility
{
    public static IConfigurationBuilder BuildConfiguration(string? path = "appsettings.json", bool includeEnvironmentVars = true) { }
    public static string RandomString(int length) { }
}
```

### Service Creation Helper

Use a single helper method in unit test classes to avoid repeated mock/bootstrap code.

```csharp
private {Entity}Service CreateService(
    I{Entity}RepositoryTrxn? trxn = null,
    I{Entity}RepositoryQuery? query = null)
{
    return new {Entity}Service(
        new NullLogger<{Entity}Service>(),
        _requestContextMock.Object,
        trxn ?? _repoTrxnMock.Object,
        query ?? _repoQueryMock.Object,
        _entityCacheMock.Object,
        _fusionCacheProviderMock.Object,
        _tenantBoundaryMock.Object);
}
```

---

## Unit Tests

Unit tests are fast, isolated, and use Moq for dependencies. Inherit from `UnitTestBase` for mock defaults and use `InMemoryDbBuilder` for repository tests.

### Service Tests

#### File: `Test/Test.Unit/Services/{Entity}ServiceTests.cs`

```csharp
[TestClass]
public class {Entity}ServiceTests : UnitTestBase
{
    [TestMethod]
    public async Task CreateAsync_WithValidDto_ReturnsSuccessResult()
    {
        // Arrange
        var dto = new {Entity}Dto
        {
            Name = "Test {Entity}",
            TenantId = _testTenantId,
            Description = "A test entity"
        };
        var request = new DefaultRequest<{Entity}Dto> { Item = dto };

        var createdEntity = {Entity}.Create(dto.TenantId, dto.Name).Value!;
        _repoTrxnMock.Setup(r => r.Create(ref It.Ref<{Entity}>.IsAny));
        _repoTrxnMock.Setup(r => r.UpdateFromDto(It.IsAny<{Entity}>(), It.IsAny<{Entity}Dto>()))
            .Returns(DomainResult<{Entity}>.Success(createdEntity));
        _repoTrxnMock.Setup(r => r.SaveChangesAsync(It.IsAny<OptimisticConcurrencyWinner>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tenantBoundaryMock.Setup(t => t.EnsureTenantBoundary(
                It.IsAny<ILogger>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Result.Success());

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value?.Item);
        Assert.AreEqual(dto.Name, result.Value.Item.Name);
    }

    [TestMethod]
    public async Task Update_NotFound_ReturnsNone()
    {
        // Arrange
        _repoTrxnMock.Setup(r => r.Get{Entity}Async(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(({Entity}?)null);
        var service = CreateService();
        var request = new DefaultRequest<{Entity}Dto> { Item = new {Entity}Dto { Id = Guid.NewGuid(), Name = "Test" } };

        // Act
        var result = await service.UpdateAsync(request);

        // Assert
        Assert.IsTrue(result.IsNone);
    }

    [TestMethod]
    public async Task Delete_ExistingEntity_ReturnsSuccess()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entity = {Entity}.Create(_testTenantId, "ToDelete").Value!;
        _repoTrxnMock.Setup(r => r.Get{Entity}Async(entityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repoTrxnMock.Setup(r => r.SaveChangesAsync(It.IsAny<OptimisticConcurrencyWinner>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tenantBoundaryMock.Setup(t => t.EnsureTenantBoundary(
                It.IsAny<ILogger>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Result.Success());
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(entityId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        _repoTrxnMock.Verify(r => r.Delete(entity), Times.Once);
    }
}
```

### Domain Entity Tests

#### File: `Test/Test.Unit/Domain/{Entity}Tests.cs`

```csharp
[TestClass]
public class {Entity}Tests
{
    [TestMethod]
    public void Create_ValidInput_ReturnsSuccess()
    {
        // Arrange & Act
        var result = {Entity}.Create(Guid.NewGuid(), "Valid Name");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Valid Name", result.Value.Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Create_WithEmptyName_ReturnsDomainFailure(string? name)
    {
        // Arrange & Act
        var result = {Entity}.Create(Guid.NewGuid(), name!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.IsTrue(result.ErrorMessage!.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Update_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var entity = {Entity}.Create(Guid.NewGuid(), "Original").Value!;

        // Act
        var result = entity.Update(name: "Updated");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Updated", result.Value!.Name);
    }

    [TestMethod]
    public void AddChild_Duplicate_ReturnsExisting()
    {
        // Arrange
        var entity = {Entity}.Create(Guid.NewGuid(), "Parent").Value!;
        var child = {ChildEntity}.Create(Guid.NewGuid(), "Child").Value!;
        entity.Add{ChildEntity}(child);

        // Act — add same child again
        var result = entity.Add{ChildEntity}(child);

        // Assert — idempotent, returns existing
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, entity.{ChildEntity}s.Count);
    }
}
```

### Domain Rule Tests

#### File: `Test/Test.Unit/Domain/{Entity}RulesTests.cs`

```csharp
[TestClass]
public class {Entity}RulesTests
{
    [TestMethod]
    public void TitleRequired_WhenTitleEmpty_IsNotSatisfied()
    {
        // Arrange
        var rule = new {Entity}NameRequiredRule();
        var entity = {Entity}.Create(Guid.NewGuid(), "").Value!;

        // Act
        var satisfied = rule.IsSatisfiedBy(entity);

        // Assert
        Assert.IsFalse(satisfied);
        Assert.IsFalse(string.IsNullOrEmpty(rule.ErrorMessage));
    }

    [TestMethod]
    public void CompositeRule_AllRules_WhenOneFails_ReturnsFalse()
    {
        // Arrange
        var rules = new IRule<{Entity}>[]
        {
            new {Entity}NameRequiredRule(),
            new {Entity}CannotDeactivateWithActiveChildrenRule()
        };
        var entity = {Entity}.Create(Guid.NewGuid(), "").Value!; // empty name

        // Act
        var result = rules.EvaluateAll(entity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.IsTrue(result.ErrorMessage!.Contains("name", StringComparison.OrdinalIgnoreCase));
    }
}
```

### Repository Tests

#### Files:
- `Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs`
- `Test/Test.Unit/Repositories/{Entity}RepositoryQueryTests.cs`

```csharp
[TestMethod]
public async Task CRUD_Pass()
{
    var db = new InMemoryDbBuilder().BuildInMemory<{App}DbContextTrxn>();
    var repo = new {Entity}RepositoryTrxn(db);
    // create -> update via domain method -> delete
}

[TestMethod]
public async Task SearchAsync_WithFilter_ReturnsMatchingEntities()
{
    // Arrange
    var db = new InMemoryDbBuilder()
        .UseEntityData(ctx =>
        {
            var tenantId = Guid.NewGuid();
            ctx.Set<{Entity}>().Add({Entity}.Create(tenantId, "Alpha").Value!);
            ctx.Set<{Entity}>().Add({Entity}.Create(tenantId, "Beta").Value!);
            ctx.Set<{Entity}>().Add({Entity}.Create(tenantId, "AlphaTwo").Value!);
            ctx.SaveChanges();
        })
        .BuildInMemory<{App}DbContextQuery>();
    var repo = new {Entity}RepositoryQuery(db);

    // Act
    var page = await repo.Search{Entity}Async(
        new SearchRequest<{Entity}SearchFilter>
        {
            PageIndex = 1,
            PageSize = 10,
            Filter = new {Entity}SearchFilter { SearchTerm = "Alpha" }
        });

    // Assert
    Assert.AreEqual(2, page.Total);
    Assert.IsTrue(page.Data.All(i => i.Name.Contains("Alpha")));
}
```

### Mapper Tests

#### File: `Test/Test.Unit/Mappers/{Entity}MapperTests.cs`

```csharp
[TestClass]
public class {Entity}MapperTests
{
    [TestMethod]
    public void ToDto_MapsAllProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entity = {Entity}.Create(tenantId, "Test Name").Value!;

        // Act
        var dto = entity.ToDto();

        // Assert
        Assert.AreEqual(entity.Id, dto.Id);
        Assert.AreEqual(entity.Name, dto.Name);
        Assert.AreEqual(entity.TenantId, dto.TenantId);
        Assert.AreEqual(entity.Flags, dto.Flags);
        // Add assertions for each additional mapped property
        // Note: Audit fields (CreatedDate, etc.) are NOT mapped — managed by AuditInterceptor
    }

    [TestMethod]
    public void ToEntity_ReturnsValidDomainResult()
    {
        // Arrange
        var dto = new {Entity}Dto { Name = "From DTO", TenantId = Guid.NewGuid() };

        // Act
        var result = dto.ToEntity(dto.TenantId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(dto.Name, result.Value!.Name);
    }
}
```

---

## Integration Tests

Integration tests exercise real HTTP endpoints through `WebApplicationFactory` with in-memory or SQLite databases. Inherit from `EndpointTestBase` and keep test methods focused on scenario setup and assertions.

### CustomApiFactory

#### File: `Test/Test.Integration/CustomApiFactory.cs`

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

### Endpoint Tests

#### File: `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs`

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
            PageIndex = 1,
            PageSize = 10,
            Filter = new {Entity}SearchFilter { SearchTerm = "SearchTarget" }
        };
        var response = await client.PostAsJsonAsync(
            $"v1/tenant/{tenantId}/{entities}/search", searchRequest);

        // Assert — 200 with filtered results
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<{Entity}Dto>>();
        Assert.IsNotNull(page);
        Assert.AreEqual(1, page.Total);
        Assert.AreEqual("SearchTarget", page.Data.First().Name);
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

### Test Configuration

#### File: `Test/Test.Integration/appsettings-test.json`

```json
{
  "TestSettings": {
    "DBSource": "UseInMemoryDatabase",
    "DBName": "Test.Integration.TestDB"
  }
}
```

### Contention/Concurrency Scenario (Optional)

For inventory/reservation/financial flows, add an integration test that runs parallel operations against the same aggregate and asserts:

- optimistic concurrency behavior is enforced
- no duplicate side effects (`no-oversell`, `no-double-reserve`, `no-double-charge`)
- retries/merge behavior is deterministic

---

## E2E Tests

E2E tests use Playwright with Page Objects for selectors/flows. Keep test methods focused on scenario intent (create/edit/delete, search, validation errors).

### Playwright CRUD Tests

#### File: `Test/Test.PlaywrightUI/Tests/{Entity}CrudTests.cs`

```csharp
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

[TestClass]
public class {Entity}CrudTests : PageTest
{
    private const string BaseUrl = "https://localhost:44318";

    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true };

    [TestInitialize]
    public async Task TestInitialize() => await Page.GotoAsync(BaseUrl);

    [TestMethod]
    [DataRow("item1", "suffix1")]
    [DataRow("item2", "suffix2")]
    public async Task AddEditDelete_Success(string baseName, string appendName)
    {
        // Arrange
        var pageObject = new {Entity}PageObject(Page);
        await pageObject.NavigateAsync(BaseUrl);

        // Act — Create
        await Page.ClickAsync("#btn-add");
        await pageObject.FillNameAsync(baseName);
        await pageObject.ClickSaveAsync();

        // Assert — row appears in list
        Assert.IsTrue(await pageObject.ItemExistsInGridAsync(baseName));

        // Act — Edit
        await Page.Locator($"tr:has-text('{baseName}')").ClickAsync();
        await pageObject.FillNameAsync(baseName + appendName);
        await pageObject.ClickSaveAsync();

        // Assert — updated name in list
        Assert.IsTrue(await pageObject.ItemExistsInGridAsync(baseName + appendName));
        Assert.IsTrue(await pageObject.ItemNotInGridAsync(baseName));

        // Act — Delete
        await pageObject.ClickDeleteAsync(baseName + appendName);

        // Assert — removed from list
        Assert.IsTrue(await pageObject.ItemNotInGridAsync(baseName + appendName));
    }
}
```

### Page Objects

#### File: `Test/Test.PlaywrightUI/PageObjects/{Entity}PageObject.cs`

```csharp
public class {Entity}PageObject(IPage page)
{
    public Task NavigateAsync(string baseUrl) => page.GotoAsync($"{baseUrl}/{entity}");
    public Task FillNameAsync(string name) => page.FillAsync("#edit-name", name);
    public Task ClickSaveAsync() => page.ClickAsync("#btn-save");
    public Task ClickDeleteAsync(string itemName) => page.Locator($"tr:has-text('{itemName}') >> button.delete").ClickAsync();
    public async Task<bool> ItemExistsInGridAsync(string itemName)
    {
        try
        {
            await page.Locator($"tr:has-text('{itemName}')").WaitForAsync(new() { Timeout = 5000 });
            return true;
        }
        catch (TimeoutException) { return false; }
    }

    public async Task<bool> ItemNotInGridAsync(string itemName)
    {
        try
        {
            await page.Locator($"tr:has-text('{itemName}')").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
            return true;
        }
        catch (TimeoutException) { return false; }
    }
}
```

---

## Quality Gates

Quality gates enforce architectural rules, performance baselines, and load profiles. These tests run outside the normal unit/integration cycle.

### Architecture Tests (NetArchTest)

#### File: `Test/Test.Architecture/BaseTest.cs`

```csharp
public abstract class BaseTest
{
    protected static readonly Assembly DomainModelAssembly = typeof(Domain.Model.{Entity}).Assembly;
    protected static readonly Assembly DomainSharedAssembly = typeof(Domain.Shared.Constants).Assembly;
    protected static readonly Assembly ApplicationServicesAssembly = typeof(Application.Services.{Entity}Service).Assembly;
    protected static readonly Assembly ApiAssembly = typeof(Program).Assembly;
}
```

#### Files:
- `Test/Test.Architecture/DomainDependencyTests.cs`
- `Test/Test.Architecture/ApplicationDependencyTests.cs`
- `Test/Test.Architecture/ApiDependencyTests.cs`

```csharp
[TestMethod]
public void DomainModel_HasNoDependencyOn_Application()
{
    var result = Types.InAssembly(DomainModelAssembly)
        .ShouldNot()
        .HaveDependencyOnAny("Application", "Infrastructure", "EntityFrameworkCore")
        .GetResult();
    Assert.IsTrue(result.IsSuccessful);
}
```

### Load Tests (NBomber)

#### File: `Test/Test.Load/{Entity}LoadTests.cs`

```csharp
[TestMethod]
public void SearchEndpoint_LoadProfile()
{
    var scenario = Scenario.Create("search", async context =>
    {
        var response = await _httpClient.GetAsync("api/v1/{entity}?pageIndex=1&pageSize=20");
        return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
    })
    .WithLoadSimulations(Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromSeconds(60)));

    NBomberRunner.RegisterScenarios(scenario).Run();
}
```

### Benchmarks (BenchmarkDotNet)

#### File: `Test/Test.Benchmarks/{Entity}Benchmarks.cs`

```csharp
[MemoryDiagnoser]
public class {Entity}Benchmarks
{
    private {Entity}Service _service = null!;

    [GlobalSetup]
    public void Setup() { /* seed in-memory context and create service */ }

    [Benchmark]
    public async Task SearchPage() => await _service.SearchAsync(new SearchRequest<{Entity}SearchFilter> { PageIndex = 1, PageSize = 20 });
}
```
