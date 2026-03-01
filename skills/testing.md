# Testing

Use this skill to scaffold tests by profile and phase.

Reference implementation: `sample-app/src/Test/` (7 test projects covering unit, integration, architecture, E2E, load, benchmarks).

## Profiles

| Profile | Include by default |
|---|---|
| `minimal` | Unit + Endpoint |
| `balanced` | Minimal + Integration + Architecture + Test.Support |
| `comprehensive` | Balanced + Playwright E2E + Load + Benchmarks |

Rule: start `balanced`, then add E2E/load/benchmarks once core slices stabilize.

---

## Projects

```text
Test/
  Test.Support/
  Test.Unit/
  Test.Integration/
  Test.Architecture/
  Test.PlaywrightUI/      (optional)
  Test.Load/              (optional)
  Test.Benchmarks/        (optional)
```

## Templates

- Unit: [../templates/test-template-unit.md](../templates/test-template-unit.md)
- Integration: [../templates/test-template-integration.md](../templates/test-template-integration.md)
- E2E: [../templates/test-template-e2e.md](../templates/test-template-e2e.md)
- Quality gates: [../templates/test-template-quality.md](../templates/test-template-quality.md)

---

## Dependencies

- MSTest: `MSTest.TestFramework`, `MSTest.TestAdapter`
- Mocks: `Moq` (or team standard mock package)
- Integration: `Microsoft.AspNetCore.Mvc.Testing`
- Architecture: `NetArchTest.Rules`
- E2E: `Microsoft.Playwright.MSTest`
- Load: `NBomber`
- Benchmarks: `BenchmarkDotNet`

Keep package versions centralized in `Directory.Packages.props`.

---

## Parallelism Rules

- Unit: parallel by method/class
- Integration: generally serialized or class-level control if shared DB state
- E2E: parallel by method with isolated test data
- Architecture/load/benchmarks: no aggressive parallelism unless validated

Example:

```csharp
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]
```

---

## Categories

Use category tags for targeted commands:

- `Unit`
- `Endpoint`
- `Integration`
- `Architecture`
- `E2E`
- `Load`
- `Benchmark`

```csharp
[TestCategory("Unit")]
public void Validate_Name_ReturnsExpected() { }
```

### Endpoint vs Integration (Execution Contract)

- `Endpoint`: API endpoint behavior tested through `WebApplicationFactory` (request/response/auth/status contract).
- `Integration`: broader integration scenarios across infrastructure/services that are not endpoint contract tests.
- Endpoint tests in `Test.Integration` should include **both** categories:
  - `TestCategory("Endpoint")`
  - `TestCategory("Integration")`
- Non-endpoint integration tests should use only `TestCategory("Integration")`.

Command split:

```powershell
# Endpoint contract path (default CI path)
dotnet test --filter "TestCategory=Endpoint"

# Broader integration path (optional/gated)
dotnet test --filter "TestCategory=Integration&TestCategory!=Endpoint"
```

---

## Core Patterns

### 1) Test.Support

- `UnitTestBase` for shared mocks
- `InMemoryDbBuilder` for in-memory/sqlite test db creation + seed hooks
- `DbSupport` for swapping runtime DB registration in integration tests
- Utility helpers for configuration + random data

### 2) Unit tests (`Test.Unit`)

Cover:
- Domain create/update invariants
- Rule/specification logic
- Service success/failure/not-found paths
- Repository paging/filter/projection behavior
- Mapper round-trip consistency

Pattern:

```csharp
[TestMethod]
public async Task CRUD_InMemory_Pass() { }
```

### 3) Endpoint and Integration tests (`Test.Integration`)

- Use `CustomApiFactory<TProgram>` and test appsettings
- Remove `IHostedService` registrations in test host when needed
- Reset DB state per test class/scenario
- Validate status codes + payload shape + auth behavior

Pattern:

```csharp
[TestCategory("Endpoint")]
[TestCategory("Integration")]
[TestMethod]
public async Task CRUD_Pass() { }
```

### 4) Playwright E2E (`Test.PlaywrightUI`)

- Use Page Object Model
- Keep selectors stable (`data-testid` preferred)
- Isolate data per test (unique names/ids)
- Cover create/edit/delete/search flows

### 5) Architecture tests (`Test.Architecture`)

Assert boundaries:
- Domain does not depend on Infrastructure/Application/EF
- Application does not depend on Infrastructure
- API does not directly depend on Domain persistence concerns

### 6) Load tests (`Test.Load`)

- Start with one critical API scenario
- Use NBomber profile by requests/sec and duration
- Capture p50/p95/p99 and error rate thresholds

### 7) Benchmarks (`Test.Benchmarks`)

- Use BenchmarkDotNet with realistic setup
- Benchmark hot paths (search/projection/mapping)
- Track regressions over time

---

## Optional Extras

### Mutation testing (Stryker)

Use mutation testing for high-value domain/services once baseline tests are stable.

### Coverage settings

Keep a `coverlet.runsettings` for stable include/exclude behavior across CI.

---

## Decision Matrix

| Need | Recommended profile |
|---|---|
| Fast startup | `minimal` |
| Team default | `balanced` |
| Release hardening | `comprehensive` |

## Slice Gate by Profile

Use these minimum test gates for a completed vertical slice:

- `minimal`: Unit + Endpoint
- `balanced`: Unit + Endpoint + Integration + Architecture
- `comprehensive`: Balanced + E2E/load/benchmark (when scenario is enabled)

If a slice spans multiple entities or stores, run at least one integration path that exercises the full composite flow.

## Contention / Concurrency Scenarios

For high-contention domains (inventory, reservations, metering, financial posting):

- add parallel-operation tests for critical commands
- assert optimistic concurrency behavior and retry/merge outcome
- verify no duplicate side effects (`no-oversell`, `no-double-reserve`, `no-double-charge` patterns)

## TestContainer / WebApplicationFactory Gotchas

Hard-won patterns from real test failures. Apply these when using `WebApplicationFactory` with TestContainers (Docker SQL).

### 1) Tenant Query Filter
The DbContext applies global query filters on `ITenantEntity<Guid>`. In tests, `IRequestContext.TenantId` defaults to null — all tenant-scoped queries return empty. **Fix:** Override `IRequestContext<string, Guid?>` in `ConfigureTestServices` with a fixed `TestTenantId`, and use that same ID in all test entity creation.

```csharp
services.AddScoped<IRequestContext<string, Guid?>>(provider =>
    new EF.Common.Contracts.RequestContext<string, Guid?>(
        Guid.NewGuid().ToString(), "Test.Endpoints", TestTenantId, []));
```

### 2) SearchRequest Defaults
`SearchRequest<T>` from `EF.Common.Contracts` has `PageSize = 0` and `PageIndex = 0` by default. Sending `new { }` returns zero results. `PageIndex = 0` with nonzero `PageSize` generates a negative SQL OFFSET. **Always send `{ PageSize = 100, PageIndex = 1 }` in search tests.**

### 3) Rate Limiting
If the API registers a global rate limiter, long test sequences (e.g., full state machine walkthroughs with 7+ requests) will hit 429. **Fix:** Override with `GetNoLimiter` in the test factory. Requires `FrameworkReference Include="Microsoft.AspNetCore.App"` in the test `.csproj`.

```csharp
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        _ => RateLimitPartition.GetNoLimiter("test"));
});
```

### 4) Schema Rebuild
When entity configurations change (e.g., adding `EntityBaseConfiguration`), call `EnsureDeletedAsync()` before `EnsureCreatedAsync()` in test initialization to force schema rebuild.

### 5) SaveChangesAsync Overload
`DbContextBase.SaveChangesAsync(CancellationToken)` throws `NotImplementedException` by design. Always use `SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct)` in services. InMemory test mode without the full audit pipeline will fail all writes.

### 6) FK Test Data
Tests that reference related entities (e.g., assigning a TodoItem to a TeamMember) must create real related records first. Using `Guid.NewGuid()` for FK values causes SQL FK violations.

### 7) Namespace Ambiguity
`Test.Support` may define its own `RequestContext<,>` — causes CS0104 ambiguity with `EF.Common.Contracts.RequestContext<,>`. Use fully qualified names: `new EF.Common.Contracts.RequestContext<string, Guid?>(...)`.

### 8) ProblemDetails Diagnostic Leak
Test factories often add `AddProblemDetails` with `ex.ToStringDemystified()` for easier debugging. This leaks full stack traces in non-debug builds. Wrap the block in `#if DEBUG` / `#endif` so CI/release builds don't expose internals:

```csharp
#if DEBUG
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
    {
        // Expose exception detail only in DEBUG builds
    });
#endif
```


- [ ] Unit tests run cleanly
- [ ] Endpoint tests run against in-memory host via `WebApplicationFactory`
- [ ] Non-endpoint integration tests run against isolated data
- [ ] Architecture tests enforce layering rules
- [ ] Optional suites (E2E/load/benchmarks) only enabled when needed
- [ ] Test projects and categories align with selected profile
- [ ] TestContainer tests override `IRequestContext` with a fixed `TestTenantId`
- [ ] Search tests specify `PageSize` and `PageIndex` (never send empty `{}`)
- [ ] Rate limiter is disabled in test factory if API uses rate limiting
