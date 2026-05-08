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

## Test Class Documentation Convention

Every `[TestClass]` carries a 3–6 line class-level `<summary>` answering:

1. **What is exercised** (one line).
2. **Tooling tier + why this tier** (what a lighter tier would miss).
3. **Non-obvious quirks** (only when applicable — retry loops, warm-up waits, fixture reuse).

Method-level docs are **not** the convention — Given/When/Then names encode scenarios. Add per-method comments only for non-obvious quirks.

```csharp
/// <summary>
/// Exercises the {Entity} create→search→update→delete flow over HTTP.
/// Tier: Test.E2E (WAF + Testcontainers SQL) — InMemory provider would miss
/// shadow properties, raw SQL projection, and concurrency token behavior.
/// </summary>
[TestClass]
public class {Entity}WorkflowTests { ... }
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

Default tier ladder: pure unit → `CustomApiFactory` (WAF + InMemory) → `SqlApiFactory` (WAF + Testcontainers SQL) → Aspire (multi-service).

### Aspire Tier By Reuse (documented exception)

Single-service tests (SQL-only, Azurite-only) **MAY** piggyback on the shared `AspireTestHost` fixture instead of spinning a parallel Testcontainers stack — purely to avoid duplicate container cost. Required when used:

- Class-level `<summary>` calls out the choice ("piggybacks on shared Aspire host to avoid second SQL container") so the deviation does not read as drift.
- Test still gates on the specific resource via `WaitForResourceHealthyAsync`, not the whole app.

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

## Aspire Test Host (recipe)

Name the fixture for what it actually wraps. If it owns the full `DistributedApplication` (DB + Functions + Storage + lifecycle), call it `AspireTestHost` — not `DatabaseFixture`. Split DB-context creation helpers into a separate `DbContextFactory` static class. Test fixtures benefit from single-responsibility naming since contributors grep by purpose.

### Shared environment rules

1. **One shared app per assembly.** Start once in `[AssemblyInitialize]` and reuse. Never per test class.
2. **Set scoped flags (e.g., `TASKFLOW_ASPIRE_TESTING`, `TASKFLOW_INCLUDE_FUNCTIONS`) before `CreateAsync`** — only for things AppHost reads via `Environment.GetEnvironmentVariable`. **Save and restore originals** in cleanup for hermeticity.
3. **Pass parameters via `configureBuilder`, not env-var mutation.** AppHost binds `Parameters:*` through `IConfiguration` — write them into `hostSettings.Configuration` so test isolation stays clean.
4. **Conditional Functions inclusion.** Detect `func.exe` once in fixture before startup. Set the include flag there, not per test class. Tests that require Functions call `Assert.Inconclusive` when the resource is absent.
5. **Timeout mandatory.** `[Timeout]` on every Aspire integration test method (`300000` for full multi-service, `120000` for single-service).
6. **`local.settings.json` override trap.** Hardcoded DB connection strings in Functions `local.settings.json` beat Aspire injection. Remove them (keep safe Azurite-style values only).
7. **Keep `using Aspire.Hosting.Testing;`** in every file calling `CreateHttpClient()` or `GetConnectionStringAsync()` (they are extension methods).

### Async call discipline

- **Per-call `.WaitAsync(timeout, ct)` on every async Aspire call.** Not a single umbrella `CancellationTokenSource(timeout)` — per-call so a hung step fails *that* step.
- **Gate on health, not status.** Aspire reports `Running` before SQL accepts connections / Azurite serves first request / Functions warms up. Call `WaitForResourceHealthyAsync(name, ct)` before talking to a resource.
- **`GetConnectionStringAsync` returns `ValueTask<string?>`** — wrap as `.AsTask().WaitAsync(timeout, ct)`. `ValueTask` has no `WaitAsync` extension.
- **Bound shutdown.** `[AssemblyCleanup(TestContext)]` (MSTest 3.x overload — use `testContext.CancellationToken`); call `StopAsync(...).WaitAsync(CleanupTimeout)` and catch `TimeoutException` so a stuck teardown does not hang CI.

### Fixture skeleton

```csharp
[AssemblyInitialize]
public static async Task AssemblyInit(TestContext context)
{
    // Save originals first — restore in cleanup
    SetEnvVar("TASKFLOW_ASPIRE_TESTING", "true");
    SetEnvVar("TASKFLOW_INCLUDE_FUNCTIONS", FuncToolAvailable() ? "true" : "false");

    var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
        args: [],
        configureBuilder: (appOptions, hostSettings) =>
        {
            appOptions.DisableDashboard = true; // explicit > implicit default
            hostSettings.Configuration["Parameters:sql-password"] = LocalSqlSettings.SharedSaPassword;
        });

    builder.Services.AddLogging(b => b
        .SetMinimumLevel(LogLevel.Information)
        .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
        .AddFilter("Aspire.", LogLevel.Warning));

    AspireApp = await builder.BuildAsync().WaitAsync(StartupTimeout, context.CancellationToken);
    await AspireApp.StartAsync().WaitAsync(StartupTimeout, context.CancellationToken);
    await AspireApp.ResourceNotifications
        .WaitForResourceHealthyAsync("sql", context.CancellationToken)
        .WaitAsync(StartupTimeout, context.CancellationToken);

    ConnectionString = await AspireApp.GetConnectionStringAsync("sql", context.CancellationToken)
        .AsTask().WaitAsync(StartupTimeout, context.CancellationToken);
}

[AssemblyCleanup]
public static async Task Cleanup(TestContext context)
{
    try { await AspireApp.StopAsync(context.CancellationToken).WaitAsync(CleanupTimeout); }
    catch (TimeoutException) { /* logged; do not block other assemblies */ }
    finally { RestoreEnvVars(); }
}
```

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
- [ ] Every test class has a class-level `<summary>` (scope / tier + why / quirks).
- [ ] Aspire fixture passes `Parameters:*` via `configureBuilder.hostSettings.Configuration`, not env vars.
- [ ] Every async Aspire call has its own `.WaitAsync(timeout, ct)` (no umbrella CTS).
- [ ] Tests gate on `WaitForResourceHealthyAsync` before touching a resource.
- [ ] `GetConnectionStringAsync` is wrapped via `.AsTask().WaitAsync(...)`.
- [ ] `[AssemblyCleanup]` uses the `TestContext` overload and bounds `StopAsync` with `.WaitAsync(CleanupTimeout)`.
- [ ] Env vars set for AppHost are saved/restored in cleanup.
- [ ] Aspire-tier fixture is named for what it wraps (`AspireTestHost` for full distributed app, not `DatabaseFixture`).
