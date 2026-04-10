# Test Templates — Quality Gates (Phase 5e)

| | |
|---|---|
| **Generates** | `Test/Test.Architecture/**`, `Test/Test.PlaywrightUI/**`, `Test/Test.Load/**`, `Test/Test.Benchmarks/**` |
| **Requires** | All implementation phases complete (5a–5d) |
| **Phase** | 5e (Quality + Delivery) |
| **Protocol** | These tests are written AFTER implementation. Unit/endpoint/integration tests already exist from 5a/5b/5c/5d. Phase 5e adds quality gates and runs a full regression. |

---

## Architecture Tests (NetArchTest)

### File: `Test/Test.Architecture/BaseTest.cs`

```csharp
public abstract class BaseTest
{
    protected static readonly Assembly DomainModelAssembly = typeof(Domain.Model.{Entity}).Assembly;
    protected static readonly Assembly DomainSharedAssembly = typeof(Domain.Shared.Constants).Assembly;
    protected static readonly Assembly ApplicationServicesAssembly = typeof(Application.Services.{Entity}Service).Assembly;
    protected static readonly Assembly ApiAssembly = typeof(Program).Assembly;
}
```

### Files:
- `Test/Test.Architecture/DomainDependencyTests.cs`
- `Test/Test.Architecture/ApplicationDependencyTests.cs`
- `Test/Test.Architecture/ApiDependencyTests.cs`

```csharp
[TestClass]
public class DomainDependencyTests : BaseTest
{
    [TestMethod]
    public void Given_DomainModelAssembly_When_DependenciesChecked_Then_NoDependencyOnApplication()
    {
        var result = Types.InAssembly(DomainModelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Application", "Infrastructure", "EntityFrameworkCore")
            .GetResult();
        Assert.IsTrue(result.IsSuccessful);
    }
}

[TestClass]
public class ApplicationDependencyTests : BaseTest
{
    [TestMethod]
    public void Given_ApplicationAssembly_When_DependenciesChecked_Then_NoDependencyOnInfrastructure()
    {
        var result = Types.InAssembly(ApplicationServicesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Infrastructure", "EntityFrameworkCore")
            .GetResult();
        Assert.IsTrue(result.IsSuccessful);
    }
}
```

---

## E2E Tests (Playwright)

### File: `Test/Test.PlaywrightUI/Tests/{Entity}CrudTests.cs`

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
    public async Task Given_NewEntity_When_AddEditDelete_Then_AllOperationsSucceed(string baseName, string appendName)
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

### File: `Test/Test.PlaywrightUI/PageObjects/{Entity}PageObject.cs`

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

## Load Tests (NBomber)

### File: `Test/Test.Load/{Entity}LoadTests.cs`

```csharp
[TestClass]
public class {Entity}LoadTests
{
    [TestMethod]
    public void Given_SearchEndpoint_When_LoadApplied_Then_MeetsPerformanceBaseline()
    {
        var scenario = Scenario.Create("search", async context =>
        {
            var response = await _httpClient.GetAsync("api/v1/{entity}?pageIndex=1&pageSize=20");
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromSeconds(60)));

        NBomberRunner.RegisterScenarios(scenario).Run();
    }
}
```

---

## Benchmarks (BenchmarkDotNet)

### File: `Test/Test.Benchmarks/{Entity}Benchmarks.cs`

```csharp
[MemoryDiagnoser]
public class {Entity}Benchmarks
{
    private {Entity}Service _service = null!;

    [GlobalSetup]
    public void Setup() { /* seed in-memory context and create service */ }

    [Benchmark]
    public async Task Given_SearchRequest_When_Executed_Then_MeasurePerformance()
        => await _service.SearchAsync(new SearchRequest<{Entity}SearchFilter> { PageIndex = 1, PageSize = 20 });
}
```

---

## Phase 5e Regression Run

After writing quality gate tests, run the full suite to verify no regressions from 5a/5b/5c/5d:

```powershell
dotnet test
```

Profile gates:
- `minimal`: Unit + Endpoint pass
- `balanced`: Unit + Endpoint + Integration + Architecture pass
- `comprehensive`: Balanced + E2E/Load/Benchmark pass
