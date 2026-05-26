# Testing

Default test scaffolding skill for Phase 5a, 5b, and integration hosts. For Phase 5d quality gates and Playwright UI, load [testing-quality.md](testing-quality.md) instead.

Reference patterns: [../patterns/expected-output-index.md](../patterns/expected-output-index.md) (Testing).

## TDD Protocol

Phases 5a and 5b use test-first TDD: red -> green -> refactor. See [../ai/tdd-protocol.md](../ai/tdd-protocol.md).
Phase 5c is tests-after for optional hosts. Phase 5d adds quality gate suites and a full regression - see [testing-quality.md](testing-quality.md).

## BDD Naming Convention

All test methods use Given_When_Then:

```csharp
[TestMethod]
public async Task Given_ValidInput_When_EntityCreated_Then_ReturnsSuccess() { }
```

## Test Class Documentation Convention

Every `[TestClass]` carries a 3-6 line class-level `<summary>` answering:

1. **What is exercised** (one line).
2. **Tooling tier + why this tier** (what a lighter tier would miss).
3. **Non-obvious quirks** (only when applicable - retry loops, warm-up waits, fixture reuse).

Method-level docs are **not** the convention - Given/When/Then names encode scenarios. Add per-method comments only for non-obvious quirks.

```csharp
/// <summary>
/// Exercises the {Entity} create->search->update->delete flow over HTTP.
/// Tier: Test.E2E (WAF + Testcontainers SQL) - InMemory provider would miss
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

| Project | Harness | Test scope | Template |
|---|---|---|---|
| `Test.Unit` | Pure CLR + Moq | Domain rules, mappers, services with mocks | [test-templates-domain.md](../templates/test-templates-domain.md), [test-templates-repository.md](../templates/test-templates-repository.md), [test-templates-service.md](../templates/test-templates-service.md) |
| `Test.Endpoints` | `WebApplicationFactory<TProgram>` + EF InMemory | Single endpoint contract: status code, response shape, validation, auth | [test-templates-endpoint.md](../templates/test-templates-endpoint.md) |
| `Test.E2E` | `WebApplicationFactory<TProgram>` + Testcontainers SQL | Multi-endpoint workflows against real SQL: paged search distinct-page, projection round-trip, FK constraints, child aggregate lifecycle | [test-templates-e2e.md](../templates/test-templates-e2e.md) |
| `Test.Integration` | Aspire `DistributedApplicationTestingBuilder` | Multi-resource distributed-app workflows: SQL + Azurite + Service Bus + Functions; audit-pipeline + projection-pipeline | [test-templates-integration.md](../templates/test-templates-integration.md) |
| `Test.PlaywrightUI` | Real hosted stack (Aspire / docker-compose / preview) | Browser-driven UI | [testing-quality.md](testing-quality.md) section Hosted Browser UI |
| `Test.Architecture` | `NetArchTest.Rules` | Layer dependency rules | [test-templates-quality.md](../templates/test-templates-quality.md) |
| `Test.Load` | NBomber | Throughput / latency baselines | [test-templates-quality.md](../templates/test-templates-quality.md) |
| `Test.Benchmarks` | BenchmarkDotNet | Per-operation micro-benchmarks | [test-templates-quality.md](../templates/test-templates-quality.md) |

Rule: PlaywrightUI is a different harness. Never merge it with WAF tests.

**Tier ladder - pick the cheapest tier that catches the failure mode you're testing.**

```
Pure unit (Test.Unit)
  -> CustomApiFactory (Test.Endpoints, WAF + InMemory)
    -> SqlApiFactory (Test.E2E, WAF + Testcontainers SQL)
      -> AspireTestHost (Test.Integration, distributed app)
        -> Hosted Playwright (Test.PlaywrightUI)
```

Phase 4 generates the WAF base in `Test.Support` and the `CustomApiFactory` / `SqlApiFactory` / `AspireTestHost` / `DbContextFactory` shells in their respective test projects so the ladder is wired before any Phase 5 tests are written. See [../ai/contract-scaffolding.md](../ai/contract-scaffolding.md) (`### 4. Test Infrastructure`).

### Aspire Tier By Reuse (documented exception)

Single-service tests (SQL-only, Azurite-only) **MAY** piggyback on the shared `AspireTestHost` fixture instead of spinning a parallel Testcontainers stack - purely to avoid duplicate container cost. Required when used:

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

**Do not use FluentAssertions.** Version 8+ requires a commercial license. Do not add it as a NuGet reference under any circumstance. If `nuget.config` contains a `<package pattern="FluentAssertions" />` allowlist entry, remove it - its presence is a license-policy violation waiting to happen.

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

`[AssemblyInitialize]` methods must **never throw**. A throwing `AssemblyInitialize` causes MSTest to abort the entire assembly - including tests that have no dependency on the failed setup.

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
        // Do not rethrow - let individual tests mark themselves inconclusive
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

**Mapper Projection <-> ToDto agreement.** Every mapper with both an EF-translatable `Projection` expression and a `ToDto` method needs a pin test that asserts both produce equivalent DTOs for the same entity. This catches the silent divergence where `Projection` (used by `Search`/`Get` query paths) omits a field or flattens a value object differently than `ToDto` (used by `Create`/`Update` response paths). Watch especially for owned types (`DateRange`, `Money`) - the projection's flat property access often disagrees with `ToDto`'s nested record construction.

```csharp
[TestMethod]
public void Projection_And_ToDto_Agree()
{
    var entity = new {Entity}Builder().Build();
    var projected = new[] { entity }.AsQueryable().Select({Entity}Mapper.Projection).Single();
    var mapped    = {Entity}Mapper.ToDto(entity);
    Assert.AreEqual(JsonSerializer.Serialize(mapped), JsonSerializer.Serialize(projected));
}
```

**Tenant-admin bypass.** When `enableMultiTenant: true`, pin both paths: `X-{App}-Admin: true` flips `DbContext.BypassTenantFilter` end-to-end (admin sees cross-tenant rows); non-admin cross-tenant access returns 404. The negative path must be a separate test so a regression in either direction surfaces independently.

### Endpoint (Test.Endpoints)

Use `WebApplicationFactory` and validate status code, response shape, validation, and auth contract for one endpoint at a time.

### Workflow E2E (Test.E2E)

Use WAF + real SQL (often Testcontainers) for create->search->update->delete business flows through HTTP.

### Blazor - Three-Layer Coverage

When `includeBlazorUI: true`, scaffold three tiers so failures localize:

1. **In-isolation host smoke** (`Test.Endpoints/BlazorHostSmokeTests`) - `WebApplicationFactory<{Project}.Blazor.Program>` builds the host with no Refit backend. Catches DI / Refit registration / MudBlazor service-provider failures at startup. Fast (no Aspire, no SQL).
2. **Aspire-mesh smoke** (`Test.Integration/Aspire/BlazorMeshSmokeTests`) - Blazor opt-in via `{APP}_INCLUDE_BLAZOR=true`; verifies the full graph (Gateway routing + Refit + tenant header) by hitting one page that round-trips through the API. Use lazy `EnsureStartedAsync` startup.
3. **Hosted Playwright** (`Test.PlaywrightUI/BlazorSmokeTests`) - real browser against a hosted stack; comprehensive profile only.

Each tier owns a different failure mode. Without tier 1, MudBlazor DI breakage is invisible until tier 2 / tier 3 fails with a misleading "page didn't load" symptom.

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

Name the fixture for what it actually wraps. If it owns the full `DistributedApplication` (DB + Functions + Storage + lifecycle), call it `AspireTestHost` - not `DatabaseFixture`. Split DB-context creation helpers into a separate `DbContextFactory` static class. Test fixtures benefit from single-responsibility naming since contributors grep by purpose.

### Shared environment rules

1. **One shared app per assembly.** Start once in `[AssemblyInitialize]` and reuse. Never per test class.
2. **Set scoped flags (e.g., `TASKFLOW_ASPIRE_TESTING`, `TASKFLOW_INCLUDE_FUNCTIONS`) before `CreateAsync`** - only for things AppHost reads via `Environment.GetEnvironmentVariable`. **Save and restore originals** in cleanup for hermeticity.
3. **Pass parameters via `configureBuilder`, not env-var mutation.** AppHost binds `Parameters:*` through `IConfiguration` - write them into `hostSettings.Configuration` so test isolation stays clean.
4. **Conditional Functions inclusion.** Detect `func.exe` once in fixture before startup. Set the include flag there, not per test class. Tests that require Functions call `Assert.Inconclusive` when the resource is absent.
5. **Timeout mandatory.** `[Timeout]` on every Aspire integration test method (`300000` for full multi-service, `120000` for single-service).
6. **`local.settings.json` override trap.** Hardcoded DB connection strings in Functions `local.settings.json` beat Aspire injection. Remove them (keep safe Azurite-style values only).
7. **Keep `using Aspire.Hosting.Testing;`** in every file calling `CreateHttpClient()` or `GetConnectionStringAsync()` (they are extension methods).

### Assertion Surface - Prefer Downstream Effects

When the Aspire mesh test needs to verify that a message flowed (an event was published, a webhook was processed), **assert against a persistent downstream effect** - a row in SQL, a document in Cosmos, an entry in Table Storage - rather than against the message bus itself.

```csharp
// PREFER - poll the audit row that the message handler writes
await Wait.Until(
    () => tableClient.QueryAsync<AuditRow>(r => r.PartitionKey == correlationId).AnyAsync(),
    timeout: TimeSpan.FromSeconds(30));

// AVOID - poll the topic/queue directly
await Wait.Until(
    async () => await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2)) is not null,
    timeout: TimeSpan.FromSeconds(180));
```

**Why.** Aspire's Service Bus emulator under `DistributedApplicationTestingBuilder` does not always propagate topic->subscription routing within bounded test windows; queue trigger plumbing on Functions is similarly best-effort under emulator-mode. Verifying via the downstream artifact is robust against this class of tooling gap *and* exercises more of the production path (the message handler actually ran end-to-end). When the downstream effect is genuinely unavailable (no consumer wired in this test scope), `[Ignore]` the test with a reason rather than asserting against the bus and accepting flakes.

### Lazy Aspire Fixture Startup

When only a subset of test classes in an assembly need the Aspire mesh (and the rest run against `CustomApiFactory` / `SqlApiFactory`), wrap startup in an `EnsureStartedAsync()` helper called from `[ClassInitialize]` instead of unconditionally starting in `[AssemblyInitialize]`:

```csharp
public static class AspireTestHost
{
    private static DistributedApplication? _app;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public static async Task<DistributedApplication> EnsureStartedAsync(TestContext ctx)
    {
        if (_app is not null) return _app;
        await _gate.WaitAsync(ctx.CancellationToken);
        try
        {
            if (_app is not null) return _app;
            _app = await BuildAndStartAsync(ctx);
            return _app;
        }
        finally { _gate.Release(); }
    }
}

[TestClass]
public class BlazorMeshSmokeTests
{
    [ClassInitialize]
    public static Task ClassInit(TestContext ctx) => AspireTestHost.EnsureStartedAsync(ctx);
}
```

Assemblies that always need the mesh (`Test.E2E` style) keep the `[AssemblyInitialize]` start. Mixed-tier assemblies (e.g., `Test.Integration` running both store-tier Testcontainers tests and a few Aspire-mesh tests) pay the ~60-90 s mesh startup only when an Aspire-tagged class actually runs. Pair with `IntegrationTestSetup.AssemblyCleanup` so the SQL/Redis/Azurite store fixtures and the Aspire graph stop together regardless of which path warmed up.

### Opt-In Graph Scope via Env Flag

When the production AppHost graph includes resources that aren't needed for every test run (Gateway + Blazor UI, React/Vite UI, Function App, Notifications), gate their `AddProject` / `AddViteApp` calls in `AppHost.cs` on an env var the test fixture sets **before** `CreateAsync`:

```csharp
// AppHost.cs
if (Environment.GetEnvironmentVariable("{APP}_INCLUDE_BLAZOR") == "true")
{
    builder.AddProject<Projects.{App}_Blazor>("blazor").WithReference(gateway);
}

// Test fixture
SetEnvVar("{APP}_INCLUDE_BLAZOR", "true");
```

This mirrors the reference app's `TASKFLOW_INCLUDE_FUNCTIONS`/`TASKFLOW_ASPIRE_TESTING` flags. Each opt-in flag is **default-off in tests** (kept on for `dotnet run --project AppHost`). The `IsAspireTesting()` check in AppHost decides the default-off; the test fixture flips one flag per resource it needs. Document the flag set in `HANDOFF.md` so the next session knows the env-var contract.

For React/Vite, use the same pattern around `AddViteApp(...)` and pass the Gateway endpoint (or API endpoint when Gateway is disabled) through `VITE_API_BASE_URL`. Browser tests still use the actual Vite resource URL as their base URL.

### Async call discipline

- **Per-call `.WaitAsync(timeout, ct)` on every async Aspire call.** Not a single umbrella `CancellationTokenSource(timeout)` - per-call so a hung step fails *that* step.
- **Gate on health, not status.** Aspire reports `Running` before SQL accepts connections / Azurite serves first request / Functions warms up. Call `WaitForResourceHealthyAsync(name, ct)` before talking to a resource.
- **`GetConnectionStringAsync` returns `ValueTask<string?>`** - wrap as `.AsTask().WaitAsync(timeout, ct)`. `ValueTask` has no `WaitAsync` extension.
- **Bound shutdown.** `[AssemblyCleanup(TestContext)]` (MSTest 3.x overload - use `testContext.CancellationToken`); call `StopAsync(...).WaitAsync(CleanupTimeout)` and catch `TimeoutException` so a stuck teardown does not hang CI.

### Fixture skeleton

```csharp
[AssemblyInitialize]
public static async Task AssemblyInit(TestContext context)
{
    // Save originals first - restore in cleanup
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
| [../templates/test-templates-repository.md](../templates/test-templates-repository.md) | 5a | Repository tests (in-memory unit) |
| [../templates/test-templates-integration.md](../templates/test-templates-integration.md) | 5a / 5b | `AspireTestHost`, `DbContextFactory`, `{Entity}RepositoryIntegrationTests`, `AuditLogRepositoryAzuriteTests`, `ApiAuditPipelineTests`, `DomainEventPipelineTests` |
| [../templates/test-templates-service.md](../templates/test-templates-service.md) | 5b | Service + mapper tests + consolidated `MapperProjectionParityTests` |
| [../templates/test-templates-endpoint.md](../templates/test-templates-endpoint.md) | 5b | Endpoint contract tests via WAF + InMemory; `WebApplicationFactoryBase` reference |
| [../templates/test-templates-e2e.md](../templates/test-templates-e2e.md) | 5b | `SqlApiFactory` + multi-endpoint `{Entity}WorkflowTests` against Testcontainers SQL |
| [../templates/test-templates-quality.md](../templates/test-templates-quality.md) | 5d | Architecture / Playwright / Load / Benchmarks - load `testing-quality.md` instead |
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

## CQRS Test Routing

For `applicationStyle: switch`, run endpoint and E2E tests in both modes by overriding `Application:Style` or `<APP>_APPLICATION_STYLE`. The same HTTP contract tests should pass against service endpoints and CQRS endpoints. For `applicationStyle: cqrs`, run the same HTTP contract suite against CQRS endpoints as the only mapped endpoint set.

Add CQRS handler tests for use-case flow and validation decorator tests where custom validators exist.
