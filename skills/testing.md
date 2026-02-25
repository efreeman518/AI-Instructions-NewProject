# Testing

Use this skill to scaffold tests by profile and phase.

Reference implementation: `sampleapp/src/Test/` (7 test projects covering unit, integration, architecture, E2E, load, benchmarks).

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

## Verification

- [ ] Unit tests run cleanly
- [ ] Endpoint tests run against in-memory host via `WebApplicationFactory`
- [ ] Non-endpoint integration tests run against isolated data
- [ ] Architecture tests enforce layering rules
- [ ] Optional suites (E2E/load/benchmarks) only enabled when needed
- [ ] Test projects and categories align with selected profile
