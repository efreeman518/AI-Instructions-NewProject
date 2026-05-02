# Testing Core

Use this file for default test scaffolding decisions and fast feedback loops.

Reference patterns: [../patterns/expected-output-index.md](../patterns/expected-output-index.md) (Testing).

## When to Load

- Load in Phase 5a and 5b by default.
- Load for harness selection, categories, profile choice, and assertion policy.
- Do not load for hosted Playwright UI specifics (use [testing-playwright-ui.md](testing-playwright-ui.md)).
- Do not load for Aspire shared-host integration fixture rules (use [testing-integration-hosts.md](testing-integration-hosts.md)).

## TDD Protocol

Phases 5a and 5b use test-first TDD: red -> green -> refactor. See [../ai/tdd-protocol.md](../ai/tdd-protocol.md).

Phase 5c is tests-after for optional hosts.

Phase 5d adds quality gate suites and full regression.

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
| `Test.E2E` | `WebApplicationFactory<TProgram>` in-memory | Multi-endpoint workflow chain |
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

Do not use FluentAssertions. Version 8+ uses a commercial license.

Allowed options:

| Option | Package | License |
|---|---|---|
| MSTest built-ins (default) | none | MIT |
| Shouldly | `Shouldly` | MIT |
| Custom helpers in Test.Support | none | n/a |

Prefer specific MSTest asserts over generic `Assert.IsTrue`.

## Categories and Command Split

Use these categories:

- `Unit`
- `Endpoint`
- `Integration`
- `E2E`
- `PlaywrightUI`
- `Architecture`
- `Load`
- `Benchmark`

```powershell
dotnet test --filter "TestCategory=Endpoint"
dotnet test --filter "TestCategory=Integration"
dotnet test --filter "TestCategory=E2E"
dotnet test --filter "TestCategory=PlaywrightUI"
```

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

Use WAF + real SQL (often Testcontainers) for create->search->update->delete business flows through HTTP.

## Test Data Builders

Place fluent builders in `Test.Support/Builders/`.

Rules:

- One builder per entity and per DTO.
- Defaults must be valid by domain rules.
- Tests override only scenario-relevant properties.

## Verification Checklist

- [ ] Unit tests pass.
- [ ] Endpoint tests run via WAF in-memory host.
- [ ] Harness split is respected (WAF vs hosted Playwright).
- [ ] Categories match intended command filters.
- [ ] Search tests always set `PageSize` and `PageIndex`.
- [ ] Rate limiter is disabled in test factory when API enables rate limiting.
