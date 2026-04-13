# Testing

Use this skill to scaffold tests by profile and phase.

Reference patterns: [../patterns/expected-output-index.md](../patterns/expected-output-index.md) (Testing).

## TDD Protocol

Phases 5a and 5b use **test-driven development**: write tests first (red), then implement to green. Phase 4 generates the contract surface (interfaces, DTOs, entity shells, test infrastructure) that makes this possible. See [../ai/tdd-protocol.md](../ai/tdd-protocol.md) for the step-by-step cycle.

Phases 5c and 5d use **tests-after**: implement infrastructure/hosts first, then write tests at the end of the same session.

Phase 5e adds **quality gate tests** (architecture, load, benchmarks) and runs a **full regression** — it does not author unit/endpoint/integration tests, which already exist from earlier phases.

## BDD Naming Convention

All test methods use `Given_When_Then`:

```csharp
[TestMethod]
public async Task Given_ValidInput_When_EntityCreated_Then_ReturnsSuccess() { }
```

- `Given` — precondition or initial state
- `When` — action under test
- `Then` — expected outcome
- PascalCase segments separated by underscores

## Split Test Templates

Test templates are split by layer for context-budget-friendly phase loading:

| Template | Phase | Contains |
|---|---|---|
| [test-templates-domain.md](../templates/test-templates-domain.md) | 5a | Entity tests, rule tests, builder activation |
| [test-templates-repository.md](../templates/test-templates-repository.md) | 5a | Repository CRUD, search, paging tests |
| [test-templates-service.md](../templates/test-templates-service.md) | 5b | Service unit tests, mapper tests |
| [test-templates-endpoint.md](../templates/test-templates-endpoint.md) | 5b | Endpoint integration tests, CustomApiFactory |
| [test-templates-quality.md](../templates/test-templates-quality.md) | 5e | Architecture, E2E, load, benchmarks |

The unified [test-templates.md](../templates/test-templates.md) remains as a complete reference but should not be loaded during phase work.

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

- Domain + rules: [../templates/test-templates-domain.md](../templates/test-templates-domain.md)
- Repository: [../templates/test-templates-repository.md](../templates/test-templates-repository.md)
- Service + mappers: [../templates/test-templates-service.md](../templates/test-templates-service.md)
- Endpoint integration: [../templates/test-templates-endpoint.md](../templates/test-templates-endpoint.md)
- Quality gates: [../templates/test-templates-quality.md](../templates/test-templates-quality.md)
- Complete on-demand reference: [../templates/test-templates.md](../templates/test-templates.md)

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

### Assertion Library Policy

**Do not use FluentAssertions.** Version 8+ carries a commercial (Xceed) license that is incompatible with open-source and most commercial project policies.

Use one of the following instead — discuss the choice with the developer before scaffolding:

| Option | Package | License | Style |
|---|---|---|---|
| **MSTest built-ins** (default) | none — included in MSTest | MIT | `Assert.AreEqual(...)`, `Assert.IsTrue(...)` |
| **Shouldly** | `Shouldly` | MIT | `value.ShouldBe(expected)` |
| **Custom helpers** | none — add to `Test.Support` | n/a | Team-defined; recommended for domain-specific assertions |

- **Default to MSTest built-ins.** They cover the vast majority of assertions without an extra dependency.
- **Prefer specific MSTest asserts over generic `Assert.IsTrue`.** MSTest v4+ analyzer rule MSTEST0037 flags these. Use:
  - `Assert.Contains(needle, haystack)` instead of `Assert.IsTrue(x.Contains(...))` — note: first arg is the substring to find, second is the string to search in
  - `Assert.IsEmpty(collection)` instead of `Assert.AreEqual(0, collection.Count)`
  - `Assert.HasCount(expectedCount, collection)` instead of `Assert.AreEqual(n, collection.Count)` — note: count first, collection second
  - `Assert.IsGreaterThanOrEqualTo(a, b)` / `Assert.IsLessThan(a, b)` instead of `Assert.IsTrue(a >= b)` / `Assert.IsTrue(a < b)`
- **Recommend Shouldly** when the developer explicitly wants expressive/fluent assertion syntax.
- **Avoid TUnit assertions standalone** — TUnit is an alternative test runner, not a drop-in assertion library for MSTest projects.
- If migrating an existing project that uses FluentAssertions, replace usages with the MSTest or Shouldly equivalent and remove the package reference.

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

**When tests are written:** Unit and endpoint tests are written during Phase 5a/5b (TDD). Infrastructure tests are written during 5c/5d (tests-after). Architecture/load/benchmark tests are written in Phase 5e (quality gates). Phase 5e runs a full regression across all categories.

## Contention / Concurrency Scenarios

For high-contention domains (inventory, reservations, metering, financial posting):

- add parallel-operation tests for critical commands
- assert optimistic concurrency behavior and retry/merge outcome
- verify no duplicate side effects (`no-oversell`, `no-double-reserve`, `no-double-charge` patterns)

## TestContainer / WebApplicationFactory Gotchas

Use [troubleshooting.md](../support/troubleshooting.md) for the canonical test failure catalog and fixes.

---

## Test Data Builders

Use fluent builder helpers in `Test.Support` to construct valid domain entities and DTOs with minimal boilerplate. Builders set all required fields to valid defaults — tests override only what matters for that scenario.

### Entity Builder

```csharp
public class {Entity}Builder
{
    private Guid _id = Guid.NewGuid();
    private Guid _tenantId = TestConstants.DefaultTenantId;
    private string _name = "Test {Entity}";
    // add more property defaults as needed

    public {Entity}Builder WithId(Guid id) { _id = id; return this; }
    public {Entity}Builder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
    public {Entity}Builder WithName(string name) { _name = name; return this; }

    public {Entity} Build()
    {
        var result = {Entity}.Create(_tenantId, _name);
        // If Create returns DomainResult, unwrap:
        // var entity = result.Value;
        // Override Id if needed via reflection or internal setter
        return result.Value;
    }
}
```

### DTO Builder

```csharp
public class {Entity}DtoBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test {Entity}";

    public {Entity}DtoBuilder WithId(Guid id) { _id = id; return this; }
    public {Entity}DtoBuilder WithName(string name) { _name = name; return this; }

    public {Entity}Dto Build() => new()
    {
        Id = _id,
        Name = _name
    };
}
```

### Usage in Tests

```csharp
// Default valid entity — no ceremony
var entity = new {Entity}Builder().Build();

// Override only the scenario-relevant field
var entity = new {Entity}Builder()
    .WithName("")   // empty name → triggers validation failure
    .Build();

// DTO for integration test payloads
var dto = new {Entity}DtoBuilder()
    .WithName("Updated Name")
    .Build();
```

### Rules

- Place builders in `Test.Support/Builders/`.
- One builder per entity + one per DTO.
- Default values must pass all domain validation (valid entity out of the box).
- Tests should only call `.With*()` for the property under test — keep test intent clear.
- For child collections, add `.WithChild(child)` / `.WithChildren(list)` methods.
- Reuse `TestConstants` for shared values (`DefaultTenantId`, `SystemUserId`).

---

## Verification Checklist

- [ ] Unit tests run cleanly
- [ ] Endpoint tests run against in-memory host via `WebApplicationFactory`
- [ ] Non-endpoint integration tests run against isolated data
- [ ] Architecture tests enforce layering rules
- [ ] Optional suites (E2E/load/benchmarks) only enabled when needed
- [ ] Test projects and categories align with selected profile
- [ ] TestContainer tests override `IRequestContext` with a fixed `TestTenantId`
- [ ] Search tests specify `PageSize` and `PageIndex` (never send empty `{}`)
- [ ] Rate limiter is disabled in test factory if API uses rate limiting

