# Test Template — Quality Gates

See [skills/testing.md](../skills/testing.md) for testing strategy and profile selection.

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

## Load Tests (NBomber)

### File: `Test/Test.Load/{Entity}LoadTests.cs`

```csharp
[TestMethod]
public void SearchEndpoint_LoadProfile()
{
    var scenario = Scenario.Create("search", async context =>
    {
        var response = await _httpClient.GetAsync("api/v1/{entity}?page=1&pageSize=20");
        return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
    })
    .WithLoadSimulations(Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromSeconds(60)));

    NBomberRunner.RegisterScenarios(scenario).Run();
}
```

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
    public async Task SearchPage() => await _service.SearchAsync(new SearchRequest<{Entity}SearchFilter> { Page = 1, PageSize = 20 });
}
```
