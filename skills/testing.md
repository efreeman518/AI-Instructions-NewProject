# Testing

Default test scaffolding skill for Phase 5a, 5b, and integration hosts. For Phase 5d quality gates and Playwright UI, load [testing-quality.md](testing-quality.md) instead.

Reference patterns: [../patterns/expected-output-index.md](../patterns/expected-output-index.md) (Testing).

## TDD Protocol

Phases 5a and 5b use test-first TDD: red → green → refactor. See [../ai/tdd-protocol.md](../ai/tdd-protocol.md).
Phase 5c is tests-after for optional hosts. Phase 5d adds quality gate suites and a full regression — see [testing-quality.md](testing-quality.md).

## BDD Naming Convention

All test methods use Given_When_Then:

```csharp
[TestMethod]
public async Task Given_ValidInput_When_EntityCreated_Then_ReturnsSuccess() { }
```

## Profiles

| Profile | Include by default |
|---|---|
| `minimal` | Unit + Endpoint |
| `balanced` | Minimal + Integration + Architecture + Test.Support |
| `comprehensive` | Balanced + PlaywrightUI + Load + Benchmarks |

Rule: start balanced, then add hosted UI and performance suites when slices stabilize.

## Project Layout

```text
Test/
  Test.Support/
  Test.Unit/
  Test.Integration/
  Test.Endpoints/
  Test.E2E/
  Test.Architecture/
  Test.PlaywrightUI/
  Test.Load/
  Test.Benchmarks/
```

## Harness Tiers (Critical)

| Project | Harness | Test scope |
|---|---|---|
| `Test.Endpoints` | `WebApplicationFactory<TProgram>` in-memory | Single endpoint contract |
| `Test.E2E` | `WebApplicationFactory<TProgram>` + Testcontainers SQL | Multi-endpoint workflow chain against real DB |
| `Test.PlaywrightUI` | Real hosted stack (Aspire/docker-compose/preview) | Browser-driven UI |

Rule: PlaywrightUI is a different harness. Never merge it with WAF tests.

## Dependencies

- MSTest: `MSTest.TestFramework`, `MSTest.TestAdapter`
- Mocks: `Moq`
- Endpoint/E2E harness: `Microsoft.AspNetCore.Mvc.Testing`
- Architecture: `NetArchTest.Rules`
- Hosted UI: `Microsoft.Playwright.MSTest`
- Load: `NBomber`
- Benchmarks: `BenchmarkDotNet`

Keep versions centralized in `Directory.Packages.props`.

## Assertion Policy

**Do not use FluentAssertions.** Version 8+ requires a commercial license. Do not add it as a NuGet reference under any circumstance. If `nuget.config` contains a `<package pattern="FluentAssertions" />` allowlist entry, remove it — its presence is a license-policy violation waiting to happen.

Allowed options:

| Option | Package | License |
|---|---|---|
| MSTest built-ins (default) | none | MIT |
| Shouldly | `Shouldly` | MIT |
| `AssertionExtensions` in Test.Support | none | n/a |

The project-local `AssertionExtensions` class (in `Test.Support/AssertionExtensions.cs`) provides `.Should()` syntax via `global using` in `Test.Unit/GlobalUsings.cs`. Use it; do not import FluentAssertions to get the same syntax.

Prefer specific MSTest asserts over generic `Assert.IsTrue`.

## Categories and Command Split

Use these categories: `Unit`, `Endpoint`, `Integration`, `E2E`, `PlaywrightUI`, `Architecture`, `Load`, `Benchmark`.

```powershell
dotnet test --filter "TestCategory=Endpoint"
dotnet test --filter "TestCategory=Integration"
dotnet test --filter "TestCategory=E2E"
dotnet test --filter "TestCategory=PlaywrightUI"
```

## Test Class Field Declarations

Fields assigned inside `[TestInitialize]` (not the constructor) must be declared with `= null!` to suppress CS8618. The nullable analyzer does not recognise `[TestInitialize]` as a guaranteed initializer.

```csharp
private Mock<IMyDependency> _mockDep = null!;
private MyService _service = null!;

[TestInitialize]
public void Setup()
{
    _mockDep = new Mock<IMyDependency>();
    _service = new MyService(_mockDep.Object);
}
```

Apply `= null!` to every non-nullable field in every generated test class.

## Assembly Initializer Safety

`[AssemblyInitialize]` methods must **never throw**. A throwing `AssemblyInitialize` causes MSTest to abort the entire assembly — including tests that have no dependency on the failed setup.

For test assemblies that start external infrastructure (e.g., Testcontainers), apply this pattern:

```csharp
[AssemblyInitialize]
public static async Task AssemblyInit(TestContext context)
{
    try
    {
        await _fixture.InitializeAsync();
    }
    catch (Exception ex)
    {
        _startupError = ex;
        // Do not rethrow — let individual tests mark themselves inconclusive
    }
}
```

In each test that depends on the infrastructure, check readiness at the start:

```csharp
[TestInitialize]
public void TestSetup()
{
    if (_startupError != null)
        Assert.Inconclusive($"Infrastructure startup failed: {_startupError.Message}");
}
```

This isolates startup flakiness (e.g., `RegexMatchTimeoutException` from Testcontainers image parsing under CPU contention) to affected tests only, and keeps unrelated tests runnable.

## Core Patterns

### Test.Support

- `UnitTestBase` for shared mocks
- `InMemoryDbBuilder` for in-memory/sqlite with seed hooks
- `DbSupport` for runtime DB registration swap in tests
- Test utilities for config and data generation

### Unit (Test.Unit)

Cover domain invariants, rules/specifications, service success/failure/not-found paths, mapper consistency.

### Endpoint (Test.Endpoints)

Use `WebApplicationFactory` and validate status code, response shape, validation, and auth contract for one endpoint at a time.

### Workflow E2E (Test.E2E)

Use WAF + real SQL (often Testcontainers) for create→search→update→delete business flows through HTTP.

## Test Data Builders

Place fluent builders in `Test.Support/Builders/`.

- One builder per entity and per DTO.
- Defaults must be valid by domain rules.
- Tests override only scenario-relevant properties.

---

## Service-Level Integration vs Endpoint Tests

`Test.Integration` is **not** for endpoint contract tests.

- Integration: service/repository scenarios against real external services (SQL/Redis/broker emulator).
- Endpoint: HTTP contract tests via `WebApplicationFactory` in `Test.Endpoints`.

If the test posts JSON to an API and asserts HTTP response shape, it belongs in `Test.Endpoints`.

## Aspire SQL Fixture Rule

When AppHost already runs persistent SQL:

- Do not add a second `MsSqlBuilder` container to `DatabaseFixture`.
- Start AppHost in fixture and read connection via `GetConnectionStringAsync("sql")`.
- Set SQL password parameter before host startup.

```csharp
Environment.SetEnvironmentVariable("Parameters__sql-password", LocalSqlSettings.SharedSaPassword);
var connectionString = await app.GetConnectionStringAsync("sql");
```

## Aspire Shared Environment Rules

1. **One shared app per assembly.** Do not start/stop the distributed app per test class. Start once in `[AssemblyInitialize]` and reuse.
2. **Set `TASKFLOW_ASPIRE_TESTING` (or your project's equivalent flag) before `CreateAsync`.** Use `DistributedApplicationTestingBuilder.CreateAsync(...)` after the env var is set.
3. **Conditional Functions inclusion.** Detect `func.exe` once in fixture before startup. Set include flag there, not per test class.
4. **Timeout mandatory.** Use `[Timeout]` on every Aspire integration test method (`300000` for full Aspire end-to-end, `120000` for SQL container only).
5. **`local.settings.json` override trap.** Hardcoded DB connection strings in Functions `local.settings.json` override Aspire injection. Remove DB connection strings for tests (except safe Azurite-style values when needed).
6. **`using Aspire.Hosting.Testing;` directive.** Keep it in files calling `CreateHttpClient()` or `GetConnectionStringAsync()` extension methods.

---

## Template Map

| Template | Phase | Purpose |
|---|---|---|
| [../templates/test-templates-domain.md](../templates/test-templates-domain.md) | 5a | Domain entity + rule tests |
| [../templates/test-templates-repository.md](../templates/test-templates-repository.md) | 5a | Repository tests |
| [../templates/test-templates-service.md](../templates/test-templates-service.md) | 5b | Service + mapper tests |
| [../templates/test-templates-endpoint.md](../templates/test-templates-endpoint.md) | 5b | Endpoint integration tests via WAF |
| [../templates/test-templates-quality.md](../templates/test-templates-quality.md) | 5d | Quality gates — load `testing-quality.md` instead |
| [../templates/test-templates.md](../templates/test-templates.md) | on-demand | Full-reference fallback |

## Verification Checklist

- [ ] Unit tests pass.
- [ ] Endpoint tests run via WAF in-memory host.
- [ ] Harness split is respected (WAF vs hosted Playwright).
- [ ] Categories match intended command filters.
- [ ] Search tests always set `PageSize` and `PageIndex`.
- [ ] Rate limiter is disabled in test factory when API enables rate limiting.
- [ ] No FluentAssertions NuGet reference exists; no `<package pattern="FluentAssertions" />` in `nuget.config`.
- [ ] Every test field assigned in `[TestInitialize]` is declared with `= null!`.
- [ ] `[AssemblyInitialize]` does not throw; infrastructure failures mark dependent tests `Inconclusive`.
- [ ] Integration tests call service/repository layer directly (no endpoint-contract tests in `Test.Integration`).
- [ ] Shared Aspire app starts once per assembly with required env vars set before creation.
