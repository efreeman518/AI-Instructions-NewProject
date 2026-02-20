# Test Templates

| | |
|---|---|
| **Files** | `Test.Unit/Domain/{Entity}Tests.cs`, `Test.Unit/Services/{Entity}ServiceTests.cs`, `Test.Unit/Repositories/`, `Test.Unit/Mappers/`, `Test.Integration/` |
| **Depends on** | [entity-template](entity-template.md), [service-template](service-template.md), [endpoint-template](endpoint-template.md) |
| **Referenced by** | [testing.md](../skills/testing.md) |

## Unit Test — Service Tests

### File: Test/Test.Unit/Services/{Entity}ServiceTests.cs

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Test.Support;

namespace Test.Unit.Application.Services;

[TestClass]
public class {Entity}ServiceTests : UnitTestBase
{
    private readonly Mock<I{Entity}RepositoryTrxn> _repoTrxnMock;
    private readonly Mock<I{Entity}RepositoryQuery> _repoQueryMock;
    private readonly Mock<IRequestContext<string, Guid?>> _requestContextMock;
    private readonly Mock<IEntityCacheProvider> _entityCacheMock;
    private readonly Mock<IFusionCacheProvider> _fusionCacheProviderMock;
    private readonly Mock<ITenantBoundaryValidator> _tenantBoundaryMock;
    private readonly I{Entity}RepositoryTrxn _repoTrxn;
    private readonly I{Entity}RepositoryQuery _repoQuery;

    public {Entity}ServiceTests() : base()
    {
        _repoTrxnMock = _mockFactory.Create<I{Entity}RepositoryTrxn>();
        _repoQueryMock = _mockFactory.Create<I{Entity}RepositoryQuery>();
        _requestContextMock = _mockFactory.Create<IRequestContext<string, Guid?>>();
        _entityCacheMock = _mockFactory.Create<IEntityCacheProvider>();
        _fusionCacheProviderMock = _mockFactory.Create<IFusionCacheProvider>();
        _tenantBoundaryMock = _mockFactory.Create<ITenantBoundaryValidator>();

        _repoTrxnMock.Setup(r =>
            r.SaveChangesAsync(It.IsAny<OptimisticConcurrencyWinner>(),
            It.IsAny<CancellationToken>())).Returns(Task.FromResult(1));

        // Setup fusion cache mock
        var fusionCacheMock = _mockFactory.Create<IFusionCache>();
        _fusionCacheProviderMock.Setup(p => p.GetCache(It.IsAny<string>()))
            .Returns(fusionCacheMock.Object);

        // Setup tenant boundary to succeed by default
        _tenantBoundaryMock.Setup(t => t.EnsureTenantBoundary(
            It.IsAny<ILogger>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Result.Success());

        var dbTrxn = new InMemoryDbBuilder()
            .SeedDefaultEntityData()
            .BuildInMemory<{App}DbContextTrxn>();
        var dbQuery = new InMemoryDbBuilder()
            .SeedDefaultEntityData()
            .BuildInMemory<{App}DbContextQuery>();
        _repoTrxn = new {Entity}RepositoryTrxn(dbTrxn);
        _repoQuery = new {Entity}RepositoryQuery(dbQuery);
    }

    // === Mock-based ===

    [TestMethod]
    public async Task Create_ValidInput_ReturnsSuccess()
    {
        {Entity}? captured = null;
        _repoTrxnMock.Setup(m => m.Create(ref It.Ref<{Entity}>.IsAny))
            .Callback((ref {Entity} item) => { captured = item; });

        var svc = new {Entity}Service(
            new NullLogger<{Entity}Service>(),
            _requestContextMock.Object,
            _repoTrxnMock.Object, _repoQueryMock.Object,
            _entityCacheMock.Object, _fusionCacheProviderMock.Object,
            _tenantBoundaryMock.Object);

        var dto = new {Entity}Dto { TenantId = Guid.NewGuid(), Name = "Test Name" };
        var request = new DefaultRequest<{Entity}Dto> { Item = dto };
        var result = await svc.CreateAsync(request);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(captured);
        _repoTrxnMock.Verify(r => r.Create(ref It.Ref<{Entity}>.IsAny), Times.Once);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ab")]
    public async Task Create_InvalidName_ReturnsFailure(string name)
    {
        var svc = new {Entity}Service(
            new NullLogger<{Entity}Service>(),
            _requestContextMock.Object,
            _repoTrxnMock.Object, _repoQueryMock.Object,
            _entityCacheMock.Object, _fusionCacheProviderMock.Object,
            _tenantBoundaryMock.Object);

        var dto = new {Entity}Dto { TenantId = Guid.NewGuid(), Name = name };
        var request = new DefaultRequest<{Entity}Dto> { Item = dto };
        var result = await svc.CreateAsync(request);
        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public async Task Update_NotFound_ReturnsNone()
    {
        _repoTrxnMock.Setup(r => r.Get{Entity}Async(
            It.IsAny<Guid>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(({Entity}?)null);

        var svc = new {Entity}Service(
            new NullLogger<{Entity}Service>(),
            _requestContextMock.Object,
            _repoTrxnMock.Object, _repoQueryMock.Object,
            _entityCacheMock.Object, _fusionCacheProviderMock.Object,
            _tenantBoundaryMock.Object);

        var dto = new {Entity}Dto { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Updated" };
        var request = new DefaultRequest<{Entity}Dto> { Item = dto };
        var result = await svc.UpdateAsync(request);
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value?.Item);
    }

    // === InMemory DB ===

    [TestMethod]
    public async Task CRUD_InMemory_Pass()
    {
        var svc = new {Entity}Service(
            new NullLogger<{Entity}Service>(),
            _requestContextMock.Object,
            _repoTrxn, _repoQuery,
            _entityCacheMock.Object, _fusionCacheProviderMock.Object,
            _tenantBoundaryMock.Object);

        // Create
        var dto = new {Entity}Dto { TenantId = Guid.NewGuid(), Name = "Test" };
        var request = new DefaultRequest<{Entity}Dto> { Item = dto };
        var result = await svc.CreateAsync(request);
        Assert.IsTrue(result.IsSuccess);
        var created = result.Value?.Item;
        Assert.IsNotNull(created?.Id);

        // Read
        var getResult = await svc.GetAsync(created.Id!.Value);
        Assert.IsTrue(getResult.IsSuccess);
        Assert.AreEqual("Test", getResult.Value!.Item!.Name);

        // Update
        var updateDto = created with { Name = "Updated" };
        var updateRequest = new DefaultRequest<{Entity}Dto> { Item = updateDto };
        var updateResult = await svc.UpdateAsync(updateRequest);
        Assert.IsTrue(updateResult.IsSuccess);
        Assert.AreEqual("Updated", updateResult.Value!.Item!.Name);

        // Delete
        var deleteResult = await svc.DeleteAsync(created.Id!.Value);
        Assert.IsTrue(deleteResult.IsSuccess);
    }

    [TestMethod]
    public async Task Search_ReturnsResults()
    {
        var svc = new {Entity}Service(
            new NullLogger<{Entity}Service>(),
            _requestContextMock.Object,
            _repoTrxn, _repoQuery,
            _entityCacheMock.Object, _fusionCacheProviderMock.Object,
            _tenantBoundaryMock.Object);

        var search = new SearchRequest<{Entity}SearchFilter> { PageSize = 10, Page = 1 };
        var result = await svc.SearchAsync(search);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.TotalCount > 0);
    }
}
```

---

## Unit Test — Domain Entity Tests

### File: Test/Test.Unit/Domain/{Entity}Tests.cs

```csharp
namespace Test.Unit.Domain;

[TestClass]
public class {Entity}Tests
{
    [TestMethod]
    public void Create_ValidInput_ReturnsSuccess()
    {
        var result = {Entity}.Create(Guid.NewGuid(), "Test");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Test", result.Value!.Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("ab")]
    public void Create_InvalidName_ReturnsFailure(string? name)
    {
        var result = {Entity}.Create(Guid.NewGuid(), name!);
        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Create_EmptyTenant_ReturnsFailure()
    {
        var result = {Entity}.Create(Guid.Empty, "Test");
        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Update_ValidInput_ReturnsSuccess()
    {
        var entity = {Entity}.Create(Guid.NewGuid(), "Original").Value!;
        var result = entity.Update(name: "Updated");
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Updated", entity.Name);
    }

    [TestMethod]
    public void Create_ValidInput_EntityIsValid()
    {
        // Valid() is private — exercised internally by Create()
        var result = {Entity}.Create(Guid.NewGuid(), "ValidName");
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
    }

    [TestMethod]
    public void AddChild_Duplicate_ReturnsExisting()
    {
        var entity = {Entity}.Create(Guid.NewGuid(), "Parent").Value!;
        var child = {Child}.Create("ChildA").Value!;
        entity.Add{Child}(child);
        var second = entity.Add{Child}(child);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(1, entity.{Children}.Count);
    }
}
```

---

## Unit Test — Domain Rules Tests

### File: Test/Test.Unit/Domain/{Entity}RulesTests.cs

```csharp
namespace Test.Unit.Domain;

[TestClass]
public class {Entity}RulesTests
{
    [TestMethod]
    [DataRow("asdfg", 6, false)]
    [DataRow("asdfg", 5, true)]
    [DataRow("longenoughname", 5, true)]
    public void NameLengthRule_ReturnsExpected(string name, int minLength, bool expectedValid)
    {
        var entity = {Entity}.Create(Guid.NewGuid(), name).Value!;
        var isValid = new {Entity}NameLengthRule(minLength).IsSatisfiedBy(entity);
        Assert.AreEqual(expectedValid, isValid);
    }

    [TestMethod]
    [DataRow("axyzghij", 5, "xyz", true)]
    [DataRow("short", 10, "xyz", false)]
    [DataRow("nopatternsdfg", 5, "xyz", false)]
    public void CompositeRule_ReturnsExpected(string name, int minLen, string pattern, bool expected)
    {
        var entity = {Entity}.Create(Guid.NewGuid(), name).Value!;
        var isValid = new {Entity}CompositeRule(minLen, pattern).IsSatisfiedBy(entity);
        Assert.AreEqual(expected, isValid);
    }
}
```

---

## Unit Test — Validator Tests

### File: Test/Test.Unit/Application/Validators/{Entity}ValidatorTests.cs

The application uses a custom `Validator` pattern that returns `Result` (not FluentValidation). The validator lives in the Application.Services layer and validates DTOs before the service processes them.

> **Note:** FluentValidation can be added later if richer validation pipelines are needed. The default pattern returns `Result.Success()` or `Result.Failure(message)` for simple, testable validation.

```csharp
namespace Test.Unit.Application.Validators;

[TestClass]
public class {Entity}ValidatorTests : UnitTestBase
{
    [TestMethod]
    [DataRow("", false)]
    [DataRow("a", false)]
    [DataRow("valid name", true)]
    [DataRow("another valid", true)]
    public void Validate_Name_ReturnsExpected(string name, bool expectedValid)
    {
        var dto = new {Entity}Dto { Name = name, TenantId = Guid.NewGuid() };
        var result = {Entity}Validator.Validate(dto);
        Assert.AreEqual(expectedValid, result.IsSuccess);
    }

    [TestMethod]
    public void Validate_EmptyTenantId_ReturnsFailure()
    {
        var dto = new {Entity}Dto { Name = "Valid", TenantId = Guid.Empty };
        var result = {Entity}Validator.Validate(dto);
        Assert.IsTrue(result.IsFailure);
    }
}
```

---

## Unit Test — Repository Tests

### File: Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs

```csharp
using Test.Support;

namespace Test.Unit.Repository;

[TestClass]
public class {Entity}RepositoryTrxnTests : UnitTestBase
{
    [TestMethod]
    public async Task CRUD_Pass()
    {
        var db = new InMemoryDbBuilder().BuildInMemory<{App}DbContextTrxn>();
        var repo = new {Entity}RepositoryTrxn(db);
        var createResult = {Entity}.Create(Guid.NewGuid(), "test item");
        Assert.IsTrue(createResult.IsSuccess);
        var entity = createResult.Value!;

        // Create
        repo.Create(ref entity);
        await repo.SaveChangesAsync(OptimisticConcurrencyWinner.Throw);
        var id = entity.Id;
        Assert.IsTrue(id != Guid.Empty);

        // Retrieve
        entity = await repo.GetEntityAsync<{Entity}>(filter: e => e.Id == id);
        Assert.IsNotNull(entity);
        Assert.AreEqual(id, entity.Id);

        // Update — uses domain Update() method (DomainResult), not direct property setters
        var updateResult = entity.Update(name: "updated");
        Assert.IsTrue(updateResult.IsSuccess);
        repo.UpdateFull(ref entity);
        await repo.SaveChangesAsync(OptimisticConcurrencyWinner.Throw);
        Assert.AreEqual("updated", entity.Name);

        // Delete
        await repo.DeleteAsync<{Entity}>(CancellationToken.None, id);
        await repo.SaveChangesAsync(OptimisticConcurrencyWinner.Throw);
        entity = await repo.GetEntityAsync<{Entity}>(filter: e => e.Id == id);
        Assert.IsNull(entity);
    }
}
```

### File: Test/Test.Unit/Repositories/{Entity}RepositoryQueryTests.cs

```csharp
using Test.Support;

namespace Test.Unit.Repository;

[TestClass]
public class {Entity}RepositoryQueryTests : UnitTestBase
{
    [TestMethod]
    public async Task Search_InMemory_ReturnsPage()
    {
        var db = new InMemoryDbBuilder()
            .SeedDefaultEntityData()
            .BuildInMemory<{App}DbContextQuery>();
        var repo = new {Entity}RepositoryQuery(db);

        var search = new SearchRequest<{Entity}SearchFilter> { PageSize = 10, Page = 1 };
        var response = await repo.Search{Entity}Async(search);
        Assert.IsNotNull(response);
        Assert.IsTrue(response.TotalCount > 0);
    }

    [TestMethod]
    public async Task GetPage_Projection_ReturnsDto()
    {
        var db = new InMemoryDbBuilder()
            .SeedDefaultEntityData()
            .BuildInMemory<{App}DbContextQuery>();
        var repo = new {Entity}RepositoryQuery(db);

        var page = await repo.QueryPageProjectionAsync(
            projector: {Entity}Mapper.Projector,
            pageSize: 10, pageNumber: 1);
        Assert.IsNotNull(page);
        Assert.IsTrue(page.Data.Count > 0);
    }
}
```

---

## Unit Test — Mapper Tests

### File: Test/Test.Unit/Mappers/{Entity}MapperTests.cs

```csharp
namespace Test.Unit.Mappers;

[TestClass]
public class {Entity}MapperTests
{
    [TestMethod]
    public void ToDto_MapsAllProperties()
    {
        var entity = {Entity}.Create(Guid.NewGuid(), "Test").Value!;
        var dto = entity.ToDto();
        Assert.AreEqual(entity.Id, dto.Id);
        Assert.AreEqual(entity.Name, dto.Name);
        Assert.AreEqual(entity.TenantId, dto.TenantId);
    }

    [TestMethod]
    public void ToEntity_ReturnsValidDomainResult()
    {
        var dto = new {Entity}Dto { TenantId = Guid.NewGuid(), Name = "Test" };
        var result = dto.ToEntity(dto.TenantId);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(dto.Name, result.Value!.Name);
    }
}
```

---

## Endpoint Tests

### File: Test/Test.Endpoints/CustomApiFactory.cs

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Test.Support;

namespace Test.Endpoints;

public class CustomApiFactory<TProgram>(string? dbConnectionString = null)
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IConfiguration config = null!;

        builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddJsonFile(
                    Path.Combine(Directory.GetCurrentDirectory(), "appsettings-test.json"));
                config = configuration.Build();
            })
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                var dbName = config.GetValue<string>("TestSettings:DBName")
                    ?? "Test.Endpoints.TestDB";
                DbSupport.ConfigureServicesTestDB<{App}DbContextTrxn, {App}DbContextQuery>(
                    services, dbConnectionString, dbName);
            });
    }
}
```

### File: Test/Test.Endpoints/EndpointTestBase.cs

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Test.Support;

namespace Test.Endpoints;

public abstract class EndpointTestBase : DbIntegrationTestBase
{
    private static CustomApiFactory<Program>? _factory;

    protected static async Task<HttpClient> GetHttpClient(
        params DelegatingHandler[] handlers)
    {
        _factory ??= new CustomApiFactory<Program>(
            TestConfigSection.GetValue("DBSource", "UseInMemoryDatabase"));

        return handlers.Length > 0
            ? _factory.CreateDefaultClient(new Uri("https://localhost"), handlers)
            : _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }
}
```

### File: Test/Test.Endpoints/Endpoints/{Entity}EndpointsTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;

[assembly: Parallelize(Workers = 1, Scope = ExecutionScope.ClassLevel)]

namespace Test.Endpoints.Endpoints;

[TestClass]
public class {Entity}EndpointsTests : EndpointTestBase
{
    private static HttpClient _httpClient = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
    {
        await ConfigureTestInstanceAsync("ClassInit");
        _httpClient = await GetHttpClient();
    }

    [ClassCleanup]
    public static async Task ClassCleanup() => await BaseClassCleanup();

    [TestMethod]
    [DoNotParallelize]
    public async Task CRUD_Pass()
    {
        await ResetDatabaseAsync(respawn: true,
            seedFactories: [() => DbContext.SeedEntityData()]);

        var urlBase = "api/v1/{entity}";
        var dto = new {Entity}Dto(null, $"test-{Guid.NewGuid()}");

        // POST
        var response = await _httpClient.PostAsJsonAsync(urlBase, dto);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<{Entity}Dto>();
        Assert.IsNotNull(created?.Id);

        // GET
        response = await _httpClient.GetAsync($"{urlBase}/{created.Id}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        // PUT
        var updated = created with { Name = "Updated" };
        response = await _httpClient.PutAsJsonAsync($"{urlBase}/{created.Id}", updated);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        // DELETE
        response = await _httpClient.DeleteAsync($"{urlBase}/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        // GET — 404
        response = await _httpClient.GetAsync($"{urlBase}/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPage_ReturnsOk()
    {
        await ResetDatabaseAsync(respawn: true,
            seedFactories: [() => DbContext.SeedEntityData()]);

        var response = await _httpClient.GetAsync("api/v1/{entity}?pageSize=10&page=1");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### File: Test/Test.Endpoints/appsettings-test.json

```json
{
  "TestSettings": {
    "DBSource": "UseInMemoryDatabase",
    "DBName": "Test.Endpoints.TestDB",
    "DBSnapshotCreate": false,
    "DBSnapshotName": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

---

## Playwright E2E Tests

### File: Test/Test.PlaywrightUI/Tests/{Entity}CrudTests.cs

```csharp
using Microsoft.Playwright.MSTest;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace Test.PlaywrightUI.Tests;

[TestClass]
public class {Entity}CrudTests : PageTest
{
    private const string BaseUrl = "https://localhost:44318";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
    };

    [TestInitialize]
    public async Task TestInitialize() => await Page.GotoAsync(BaseUrl);

    [TestMethod]
    [DataRow("item1", "suffix1")]
    [DataRow("item2", "suffix2")]
    public async Task AddEditDelete_Success(string baseName, string appendName)
    {
        var uniqueName = $"{baseName}-{DateTime.UtcNow.Ticks}";

        // Create
        await Page.FillAsync("#edit-name", uniqueName);
        await Page.ClickAsync("#btn-save");
        var item = await Page.WaitForSelectorAsync($"tr:has-text('{uniqueName}')");
        Assert.IsNotNull(item, "Item should appear in grid after creation");

        // Edit
        await Page.Locator($"tr:has-text('{uniqueName}') >> button.edit").ClickAsync();
        var editedName = $"{uniqueName}-{appendName}";
        await Page.FillAsync("#edit-name", editedName);
        await Page.ClickAsync("#btn-save");
        item = await Page.WaitForSelectorAsync($"tr:has-text('{editedName}')");
        Assert.IsNotNull(item, "Updated item should appear in grid");

        // Delete
        await Page.Locator($"tr:has-text('{editedName}') >> button.delete").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        try
        {
            await Page.WaitForSelectorAsync($"tr:has-text('{editedName}')",
                new PageWaitForSelectorOptions { Timeout = 2000 });
            Assert.Fail("Item should not exist after deletion");
        }
        catch { /* Expected */ }
    }
}
```

### File: Test/Test.PlaywrightUI/PageObjects/{Entity}PageObject.cs

```csharp
using Microsoft.Playwright;

namespace Test.PlaywrightUI.PageObjects;

public class {Entity}PageObject(IPage page)
{
    private readonly IPage _page = page;

    public async Task NavigateAsync(string baseUrl) =>
        await _page.GotoAsync($"{baseUrl}/{entity}");

    public async Task FillNameAsync(string name) =>
        await _page.FillAsync("#edit-name", name);

    public async Task ClickSaveAsync() =>
        await _page.ClickAsync("#btn-save");

    public async Task ClickDeleteAsync(string itemName) =>
        await _page.Locator($"tr:has-text('{itemName}') >> button.delete").ClickAsync();

    public async Task<bool> ItemExistsInGridAsync(string itemName) =>
        await _page.WaitForSelectorAsync(
            $"tr:has-text('{itemName}')",
            new PageWaitForSelectorOptions { Timeout = 5000 }) is not null;

    public async Task<bool> ItemNotInGridAsync(string itemName)
    {
        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await _page.WaitForSelectorAsync(
                $"tr:has-text('{itemName}')",
                new PageWaitForSelectorOptions { Timeout = 2000 });
            return false;
        }
        catch { return true; }
    }
}
```

---

## Architecture Tests

### File: Test/Test.Architecture/BaseTest.cs

```csharp
using System.Reflection;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace Test.Architecture;

public abstract class BaseTest
{
    protected static readonly Assembly DomainModelAssembly =
        typeof({App}.Domain.Model.{Entity}).Assembly;
    protected static readonly Assembly DomainSharedAssembly =
        typeof({App}.Domain.Shared.Constants).Assembly;
    protected static readonly Assembly ApplicationServicesAssembly =
        typeof({App}.Application.Services.{Entity}Service).Assembly;
    protected static readonly Assembly ApiAssembly =
        typeof(Program).Assembly;
}
```

### File: Test/Test.Architecture/DomainDependencyTests.cs

```csharp
using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class DomainDependencyTests : BaseTest
{
    [TestMethod]
    public void DomainModel_HasNoDependencyOn_Application()
    {
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Application", "Infrastructure", "EntityFrameworkCore")
            .GetResult();
        Assert.IsTrue(result.IsSuccessful,
            result.FailingTypeNames is not null
                ? string.Join(", ", result.FailingTypeNames) : null);
    }

    [TestMethod]
    public void DomainShared_HasNoDependencyOn_DomainModel()
    {
        var result = Types.InAssembly(DomainSharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Domain.Model", "Domain.Rules", "Application", "Infrastructure")
            .GetResult();
        Assert.IsTrue(result.IsSuccessful);
    }
}
```

### File: Test/Test.Architecture/ApplicationDependencyTests.cs

```csharp
using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class ApplicationDependencyTests : BaseTest
{
    [TestMethod]
    public void ApplicationServices_HasNoDependencyOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationServicesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Infrastructure", "EntityFrameworkCore", "{App}.Api")
            .GetResult();
        Assert.IsTrue(result.IsSuccessful,
            result.FailingTypeNames is not null
                ? string.Join(", ", result.FailingTypeNames) : null);
    }
}
```

### File: Test/Test.Architecture/ApiDependencyTests.cs

```csharp
using NetArchTest.Rules;

namespace Test.Architecture;

[TestClass]
public class ApiDependencyTests : BaseTest
{
    [TestMethod]
    public void Api_HasNoDependencyOn_DomainOrEF()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Domain", "EntityFrameworkCore", "Infrastructure.Data")
            .GetResult();
        Assert.IsTrue(result.IsSuccessful,
            result.FailingTypeNames is not null
                ? string.Join(", ", result.FailingTypeNames) : null);
    }
}
```

---

## Test.Support Infrastructure

### File: Test/Test.Support/UnitTestBase.cs

```csharp
using Moq;

[assembly: Parallelize(Workers = 5, Scope = ExecutionScope.MethodLevel)]

namespace Test.Support;

public abstract class UnitTestBase
{
    protected readonly MockRepository _mockFactory;

    protected UnitTestBase()
    {
        _mockFactory = new MockRepository(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
    }
}
```

### File: Test/Test.Support/InMemoryDbBuilder.cs

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Test.Support;

public class InMemoryDbBuilder
{
    private bool _seedDefaultData;
    private readonly List<Action<DbContext>> _seedActions = [];

    public InMemoryDbBuilder SeedDefaultEntityData()
    {
        _seedDefaultData = true;
        return this;
    }

    public InMemoryDbBuilder UseEntityData(Action<DbContext> seedAction)
    {
        _seedActions.Add(seedAction);
        return this;
    }

    public T BuildInMemory<T>(string? dbName = null) where T : DbContext
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase(dbName).Options;
        var dbContext = (Activator.CreateInstance(typeof(T), options) as T)!;

        if (_seedDefaultData) SeedDefaults(dbContext);
        foreach (var action in _seedActions) action(dbContext);
        dbContext.SaveChanges();
        return dbContext;
    }

    public T BuildSQLite<T>() where T : DbContext
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<T>().UseSqlite(connection).Options;
        var dbContext = (Activator.CreateInstance(typeof(T), options) as T)!;
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        if (_seedDefaultData) SeedDefaults(dbContext);
        foreach (var action in _seedActions) action(dbContext);
        dbContext.SaveChanges();
        return dbContext;
    }

    private void SeedDefaults(DbContext dbContext)
    {
        // Customize: add default test entities for your domain
    }
}
```

### File: Test/Test.Support/DbSupport.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test.Support;

public static class DbSupport
{
    public static void ConfigureServicesTestDB<TTrxn, TQuery>(
        IServiceCollection services, string? dbConnectionString, string dbName = "TestDB")
        where TTrxn : DbContext
        where TQuery : DbContext
    {
        if (string.IsNullOrEmpty(dbConnectionString)) return;

        services.RemoveAll<DbContextOptions<TTrxn>>();
        services.RemoveAll<TTrxn>();
        services.RemoveAll<DbContextOptions<TQuery>>();
        services.RemoveAll<TQuery>();

        if (dbConnectionString == "UseInMemoryDatabase")
        {
            services.AddDbContext<TTrxn>(opt => opt.UseInMemoryDatabase(dbName),
                ServiceLifetime.Singleton, ServiceLifetime.Singleton);
            services.AddDbContext<TQuery>(opt => opt.UseInMemoryDatabase(dbName),
                ServiceLifetime.Singleton, ServiceLifetime.Singleton);
        }
        else
        {
            services.AddDbContext<TTrxn>(opt =>
                opt.UseSqlServer(dbConnectionString, sql =>
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)),
                ServiceLifetime.Singleton, ServiceLifetime.Singleton);
            services.AddDbContext<TQuery>(opt =>
            {
                opt.UseSqlServer(dbConnectionString + ";ApplicationIntent=ReadOnly", sql =>
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null));
                opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);
        }
    }
}
```

### File: Test/Test.Support/Utility.cs

```csharp
using Microsoft.Extensions.Configuration;

namespace Test.Support;

public static class Utility
{
    public static IConfigurationBuilder BuildConfiguration(
        string? path = "appsettings.json", bool includeEnvironmentVars = true)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory());
        if (path is not null) builder.AddJsonFile(path);
        if (includeEnvironmentVars) builder.AddEnvironmentVariables();
        var config = builder.Build();
        var env = config.GetValue("ASPNETCORE_ENVIRONMENT", "development")!.ToLower();
        builder.AddJsonFile($"appsettings.{env}.json", optional: true);
        return builder;
    }

    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        Span<char> result = stackalloc char[length];
        for (var i = 0; i < length; i++)
            result[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(result);
    }
}
```
