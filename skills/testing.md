# Testing

## Overview

Testing follows a structured pyramid with **seven test project types**: unit tests (MSTest + CleanMoq), repository tests (InMemory/SQLite), integration tests (Bootstrapper DI), endpoint tests (WebApplicationFactory + Testcontainers), E2E browser tests (Playwright), architecture tests (NetArchTest), and optional performance testing (BenchmarkDotNet, NBomber). A shared **Test.Support** library provides InMemory DB builders, integration test bases, DB management utilities, and seed helpers.

> **CleanMoq note:** The NuGet package `CleanMoq` is a thin wrapper around `Moq` that re-exports all Moq types. Code uses `using Moq;` — this is correct. The package name in Directory.Packages.props is `CleanMoq` (not `Moq`). Both `MockRepository`, `Mock<T>`, `It`, etc. come from the underlying Moq library.

## Testing Profiles (Recommended for New Solutions)

Use a profile to avoid over-scaffolding tests too early and keep generation cohesive with project maturity.

| Profile | Recommended Projects | When to Use |
|---|---|---|
| `minimal` | `Test.Unit`, `Test.Support` | MVP/prototype, fast iteration, domain/service logic still changing quickly |
| `balanced` (default for `full`) | `Test.Unit`, `Test.Integration` or `Test.Endpoints`, `Test.Support`, optional `Test.Architecture` | Most production-oriented new projects during first iterations |
| `comprehensive` | All of the above + `Test.PlaywrightUI` + optional `Test.Load` + `Test.Benchmarks` | Stabilized systems with release gates and non-functional test requirements |

**Defaults:**
- `scaffoldMode: lite` → start at `minimal`
- `scaffoldMode: full` → start at `balanced`
- promote to `comprehensive` only after core slices and deployment topology are stable

Use `testingProfile` in [domain-inputs.schema.md](../domain-inputs.schema.md) to select the initial footprint.

## Baseline Alignment

For new Aspire-first solutions, default to this cohesion level unless broader coverage is explicitly requested:

- `Test.Unit` + `Test.Integration` + `Test.Support` first
- add endpoint-specific `WebApplicationFactory` tests when API routes stabilize
- defer E2E/load/benchmarks until deployment topology and critical workflows are stable

When expanding beyond this baseline, use these established patterns:

- `DbIntegrationTestBase` + `Respawn` reset flow
- optional Testcontainers SQL lifecycle
- `CustomApiFactory<TProgram>` host overrides for endpoint tests
- explicit parallelism controls (`Parallelize`, `[DoNotParallelize]`) per suite

## Uno UI Cross-Platform Smoke Tests (Optional)

When `includeUnoUI: true`, you can add a dedicated smoke-test layer modeled after mature Uno UITest suites:

- use `Uno.UITest` for cross-platform smoke checks (Browser/Android/iOS)
- keep a tiny baseline suite (launch + one core navigation assertion)
- add retry support for flaky device/browser startup (attribute-based retries)
- collect screenshots/logs on teardown for CI diagnostics

Use this as a complement to Playwright (`Test.PlaywrightUI`), not a replacement:
- Playwright stays the primary browser E2E option
- Uno.UITest is valuable when validating native target behavior beyond browser-only flows

## Project Structure

> **Reference implementation:** See `sampleapp/src/Test/` for the full directory layout across all 7 test project types.

```
Test/
├── Test.Unit/
│   ├── Services/                    → sampleapp: TodoItemServiceTests.cs, CategoryServiceTests.cs
│   ├── Domain/                      → sampleapp: TodoItemTests.cs, CategoryTests.cs
│   │   └── Rules/                   → sampleapp: TodoItemStatusTransitionRuleTests.cs
│   ├── Repositories/                → sampleapp: TodoItemRepositoryQueryTests.cs
│   └── UnitTestBase.cs              (inherited from Test.Support)
├── Test.Integration/
│   ├── Endpoints/                   → sampleapp: TodoItemEndpointTests.cs, CategoryEndpointTests.cs
│   ├── CustomApiFactory.cs          → sampleapp: CustomApiFactory.cs
│   ├── EndpointTestBase.cs          → sampleapp: EndpointTestBase.cs
│   └── appsettings-test.json        → sampleapp: appsettings-test.json
├── Test.PlaywrightUI/
│   ├── Tests/                       → sampleapp: TodoItemCrudTests.cs
│   ├── PageObjects/                 → sampleapp: TodoItemPageObject.cs
│   └── README.md
├── Test.Architecture/
│   ├── DomainDependencyTests.cs     → sampleapp: DomainDependencyTests.cs
│   ├── ApplicationDependencyTests.cs
│   ├── ApiDependencyTests.cs
│   └── StructuralConventionTests.cs
├── Test.Support/
│   ├── UnitTestBase.cs              → sampleapp: UnitTestBase.cs
│   ├── InMemoryDbBuilder.cs         → sampleapp: InMemoryDbBuilder.cs
│   ├── IntegrationTestBase.cs       → sampleapp: IntegrationTestBase.cs
│   ├── DbIntegrationTestBase.cs     → sampleapp: DbIntegrationTestBase.cs
│   ├── DbSupport.cs                 → sampleapp: DbSupport.cs
│   └── Utility.cs                   → sampleapp: Utility.cs
├── Test.Load/                       (optional)
│   ├── Program.cs                   → sampleapp: Program.cs
│   ├── {Entity}LoadTest.cs          → sampleapp: TodoItemLoadTest.cs
│   └── Utility.cs                   → sampleapp: Utility.cs
└── Test.Benchmarks/                 (optional)
    ├── Program.cs                   → sampleapp: Program.cs
    ├── {Entity}Benchmarks.cs        → sampleapp: TodoItemBenchmarks.cs
    └── RepositoryBenchmarks.cs      → sampleapp: RepositoryBenchmarks.cs
```

## Test Project Dependencies

> **Reference implementation:** See `sampleapp/src/Test/Test.Unit/Test.Unit.csproj`, `sampleapp/src/Test/Test.Integration/Test.Integration.csproj`, `sampleapp/src/Test/Test.Architecture/Test.Architecture.csproj`, `sampleapp/src/Test/Test.PlaywrightUI/Test.PlaywrightUI.csproj`, `sampleapp/src/Test/Test.Support/Test.Support.csproj`, `sampleapp/src/Test/Test.Load/Test.Load.csproj`, `sampleapp/src/Test/Test.Benchmarks/Test.Benchmarks.csproj`

### Key dependency rules

| Project | Key Packages | Project References |
|---------|-------------|-------------------|
| `Test.Unit` | CleanMoq, MSTest, coverlet | Application.Services, Infrastructure.Repositories, Test.Support |
| `Test.Integration` | Microsoft.AspNetCore.Mvc.Testing, Respawn, Testcontainers.MsSql | Api project, Test.Support |
| `Test.PlaywrightUI` | Microsoft.Playwright.MSTest | None (pure HTTP/browser) |
| `Test.Architecture` | NetArchTest.Rules, MSTest | Domain.Model, Domain.Shared, Application.Services, Api |
| `Test.Support` | EF Core InMemory/Sqlite, Respawn, Testcontainers | Bootstrapper, Infrastructure |
| `Test.Load` | NBomber, NBomber.Http | None (console app, HTTP only) |
| `Test.Benchmarks` | BenchmarkDotNet | Application.Services, Infrastructure.Repositories, Test.Support |

## Parallelism Strategy

Each test project declares its parallelism in a single assembly attribute:

```csharp
// Test.Unit — max parallel, no shared state
[assembly: Parallelize(Workers = 5, Scope = ExecutionScope.MethodLevel)]

// Test.Integration — shared DB, sequential within class
[assembly: Parallelize(Workers = 1, Scope = ExecutionScope.ClassLevel)]

// Test.PlaywrightUI — each test gets own browser context
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

// Test.Architecture — stateless, max parallel
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]
```

## Test Categories

Use `[TestCategory]` for CI filtering:

```csharp
[TestCategory("Unit")]        // Fast, no external dependencies
[TestCategory("Integration")] // Requires DB (InMemory or container)
[TestCategory("Endpoint")]    // Requires WebApplicationFactory + DB
[TestCategory("E2E")]         // Requires running app + browser
[TestCategory("Architecture")]// Stateless dependency rule checks
[TestCategory("Load")]        // Requires running app — CI excluded
[TestCategory("Benchmark")]   // Long-running, release build only
```

CI pipeline example:
```bash
dotnet test --filter "TestCategory!=E2E&TestCategory!=Load&TestCategory!=Benchmark"
```

## Naming Convention

All test methods follow: `{Method}_{Scenario}_{ExpectedResult}`

```
Create_ValidInput_ReturnsSuccess
Create_EmptyName_ReturnsFailure
Get_CrossTenant_ReturnsFailure
Search_NonAdmin_ForcesTenantFilter
CRUD_InMemory_Pass
```

---

## 1. Test.Support — Shared Infrastructure

Provides base classes and utilities shared across all test projects. All implementations are in sampleapp — use them as templates, adapting the `{App}` and `{Entity}` placeholders.

### UnitTestBase

Base class for all unit tests. Uses `MockRepository` factory pattern with `DefaultValue.Mock` for auto-mocking.

> **Reference implementation:** See `sampleapp/src/Test/Test.Support/UnitTestBase.cs`

```csharp
// Compact signature — see sampleapp for full implementation
public abstract class UnitTestBase
{
    protected readonly MockRepository _mockFactory;
    protected UnitTestBase() =>
        _mockFactory = new MockRepository(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
}
```

### InMemoryDbBuilder

Fluent builder for constructing test databases (InMemory or SQLite).

> **Reference implementation:** See `sampleapp/src/Test/Test.Support/InMemoryDbBuilder.cs`

### Key API

```csharp
new InMemoryDbBuilder()
    .SeedDefaultEntityData()                          // Optional: seed baseline data
    .UseEntityData(ctx => ctx.Set<T>().Add(...))      // Optional: custom seed actions
    .BuildInMemory<TDbContext>(dbName?)                // InMemory provider
    .BuildSQLite<TDbContext>();                        // SQLite in-memory (supports SQL operations)
```

### IntegrationTestBase

Uses the real Bootstrapper DI registrations, replacing only what's needed for testing (logging, request context).

> **Reference implementation:** See `sampleapp/src/Test/Test.Support/IntegrationTestBase.cs`

### Key pattern

- Calls `RegisterInfrastructureServices`, `RegisterBackgroundServices`, `RegisterDomainServices`, `RegisterApplicationServices` — same DI as production
- Replaces `IRequestContext` with a test identity
- Uses `Utility.BuildConfiguration()` for config loading

### DbIntegrationTestBase

Extends `IntegrationTestBase` with 3-mode DB support — InMemory, Testcontainers.MsSql, or existing SQL Server.

> **Reference implementation:** See `sampleapp/src/Test/Test.Support/DbIntegrationTestBase.cs`

### Key features

- **DB mode selection:** reads `TestSettings:DBSource` — `"UseInMemoryDatabase"`, `"TestContainer"`, or a connection string
- **Testcontainers lifecycle:** starts `MsSqlContainer` if configured, replaces `master` with test DB name
- **Respawn integration:** `ResetDatabaseAsync(respawn: true)` for clean state between tests
- **Seed support:** `seedPaths` (SQL files) and `seedFactories` (in-memory factories) for flexible test data

### DbSupport

Generic DbContext swap utility that removes production registrations and registers InMemory or SQL Server test contexts.

> **Reference implementation:** See `sampleapp/src/Test/Test.Support/DbSupport.cs`

### Key rules

- Removes existing `DbContextOptions<T>` and `T` registrations before re-adding
- InMemory uses `ServiceLifetime.Singleton` for both context and options
- SQL Server uses `ApplicationIntent=ReadOnly` for query context, `EnableRetryOnFailure` for resilience

### Utility

> **Reference implementation:** See `sampleapp/src/Test/Test.Support/Utility.cs`

Provides `BuildConfiguration()` (loads appsettings + environment overrides) and `RandomString()` for test data generation.

---

## 2. Unit Tests — Test.Unit

Unit tests use `UnitTestBase` with `MockRepository` for consistent mock creation. Tests are organized into folders by concern: `Services`, `Domain`, `Domain/Rules`, `Repositories`.

### Service Tests — Dual Strategy

Service tests use **two approaches** in the same test class:
1. **Mock-based** — Fast, isolated, verify interactions with `Verify()` and call counts
2. **InMemory DB** — Use real repositories with InMemory provider for end-to-end logic

> **Reference implementation:** See `sampleapp/src/Test/Test.Unit/Services/TodoItemServiceTests.cs`

```csharp
// Compact pattern — see sampleapp for full test class
[TestClass]
public class {Entity}ServiceTests : UnitTestBase
{
    // Mock repos for interaction-based tests
    private readonly Mock<I{Entity}RepositoryTrxn> _repoTrxnMock;
    // Real repos backed by InMemoryDbBuilder for state-based tests
    private readonly I{Entity}RepositoryTrxn _repoTrxn;

    [TestMethod]
    public async Task Create_ValidInput_ReturnsSuccess() { /* mock-based: Verify(Times.Once) */ }

    [TestMethod]
    public async Task CRUD_InMemory_Pass() { /* real repos: create → read → update → delete */ }

    [TestMethod]
    [DataRow("")]
    [DataRow("ab")]
    public async Task Create_InvalidName_ReturnsFailure(string name) { /* validation path */ }
}
```

### Domain Entity Tests

Test the rich domain model — factory methods, validation, property updates.

> **Reference implementation:** See `sampleapp/src/Test/Test.Unit/Domain/TodoItemTests.cs` and `sampleapp/src/Test/Test.Unit/Domain/CategoryTests.cs`

### Key test patterns

- `Create_ValidInput_ReturnsSuccess` — factory method returns `DomainResult.IsSuccess`
- `Create_InvalidName_ReturnsFailure` — `[DataRow]` with null, empty, too-short values
- `Create_EmptyTenant_ReturnsFailure` — `Guid.Empty` tenant validation
- `Update_SetsProperties` — mutate via `Update()`, assert new values

### Domain Rules / Specification Tests

Test individual specification rules and composite rules.

> **Reference implementation:** See `sampleapp/src/Test/Test.Unit/Domain/Rules/TodoItemStatusTransitionRuleTests.cs`

### Key test patterns

- Use `[DataRow]` parameters for rule boundary testing
- Test both single rules (`IsSatisfiedBy`) and composite rules
- Verify that `CompositeRule` correctly AND-combines child rules

### StructureValidators Tests

Validation uses `StructureValidators` (static class that returns `Result`) — **not** FluentValidation.

### Key test patterns

```csharp
[TestMethod]
[DataRow("", false)]
[DataRow("a", false)]
[DataRow("valid name", true)]
public void Validate_Name_ReturnsExpected(string name, bool expectedValid)
{
    var dto = new {Entity}Dto(null, name);
    var result = StructureValidators.Validate{Entity}Dto(dto);
    Assert.AreEqual(expectedValid, result.IsSuccess);
}
```

### Repository Tests

Test real repository implementations against InMemory or SQLite provider.

> **Reference implementation:** See `sampleapp/src/Test/Test.Unit/Repositories/TodoItemRepositoryQueryTests.cs`

### Key test patterns

- **CRUD test** — `repo.Create` → `SaveChangesAsync` → `GetEntityAsync` → `UpdateFull` → `DeleteAsync`
- **Search (InMemory)** — `SearchRequest` with filter, assert `TotalCount > 0`
- **Search (SQLite)** — for SQL-dependent operations (Like, Contains) that InMemory doesn't support
- **Projection** — `QueryPageProjectionAsync` with mapper's `Projector` expression

### Mapper Tests

```csharp
// Mapper test pattern — verify round-trip mapping
[TestMethod]
public void ToDto_MapsAllProperties()
{
    var entity = {Entity}.Create(Guid.NewGuid(), "Test").Value!;
    var dto = {Entity}Mapper.ToDto(entity);
    Assert.AreEqual(entity.Id, dto.Id);
    Assert.AreEqual(entity.Name, dto.Name);
}
```

---

## 3. Endpoint Tests — Test.Integration

### CustomApiFactory

`WebApplicationFactory<T>` subclass that overrides configuration and DI for testing.

> **Reference implementation:** See `sampleapp/src/Test/Test.Integration/CustomApiFactory.cs`

### Key features

- Loads `appsettings-test.json` via `ConfigureAppConfiguration`
- Removes all `IHostedService` registrations (background services not needed)
- Swaps DB to test DB via `DbSupport.ConfigureServicesTestDB`

### EndpointTestBase

Base class combining WebApplicationFactory with DB management.

> **Reference implementation:** See `sampleapp/src/Test/Test.Integration/EndpointTestBase.cs`

### Key pattern

- Lazy-initializes `CustomApiFactory<Program>` with test DB connection string
- Provides `GetHttpClient()` with optional `DelegatingHandler[]` for auth injection
- Inherits `DbIntegrationTestBase` for DB lifecycle management

### Integration TestServer Authentication for Forbid()

When writing endpoint tests that assert **403 Forbidden** responses (e.g., from `Results.Forbid()`), the test host must have a registered authentication scheme and the middleware pipeline must include `UseAuthentication` before `UseAuthorization`. Without this, `Results.Forbid()` produces a **500 Internal Server Error** instead of 403 because no authentication handler is available to issue the challenge/forbid response.

#### Setup Order (services + middleware)

```csharp
// In CustomApiFactory.ConfigureWebHost or test host builder:

// 1. Services — register a minimal test authentication scheme
services.AddAuthentication("TestScheme")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
services.AddAuthorization();

// 2. Middleware — order matters
app.UseAuthentication();   // Must come before UseAuthorization
app.UseAuthorization();
```

```csharp
// Minimal TestAuthHandler for integration tests
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Return success with a basic identity, or NoResult for anonymous
        var identity = new ClaimsIdentity("TestScheme");
        identity.AddClaim(new Claim(ClaimTypes.Name, "TestUser"));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

**Key points:**
- Without `AddAuthentication` + `UseAuthentication`, `Results.Forbid()` has no handler to produce a 403 and falls back to a 500.
- The test scheme can return a default authenticated identity; authorization policies decide the 403.
- If testing both authenticated and anonymous paths, conditionally return `AuthenticateResult.NoResult()` based on a header or token presence.

### Endpoint CRUD Test

> **Reference implementation:** See `sampleapp/src/Test/Test.Integration/Endpoints/TodoItemEndpointTests.cs` and `sampleapp/src/Test/Test.Integration/Endpoints/CategoryEndpointTests.cs`

### Key test patterns

- `[ClassInitialize]` calls `ConfigureTestInstanceAsync` + `GetHttpClient`
- `[DoNotParallelize]` for DB-dependent tests
- Full CRUD lifecycle: POST (201 Created) → GET (200 OK) → PUT (200 OK) → DELETE (204 NoContent) → GET (404 NotFound)
- Uses `ResetDatabaseAsync(respawn: true)` before each test

### Test appsettings

> **Reference implementation:** See `sampleapp/src/Test/Test.Integration/appsettings-test.json`

```json
{
  "TestSettings": {
    "DBSource": "UseInMemoryDatabase",
    "DBName": "Test.Integration.TestDB"
  }
}
```

---

## 4. Playwright E2E Tests — Test.PlaywrightUI

### Setup

Install Playwright browsers before first run:
```bash
pwsh bin/Debug/<target-framework>/playwright.ps1 install
```

The application must be running before executing Playwright tests.

### Page Object Model

Encapsulate page interactions for reusability.

> **Reference implementation:** See `sampleapp/src/Test/Test.PlaywrightUI/PageObjects/TodoItemPageObject.cs`

### Key rules

- One page object per entity or page area
- Constructor takes `IPage`
- Methods: `NavigateAsync`, `Fill{Field}Async`, `ClickSaveAsync`, `ClickDeleteAsync`, `ItemExistsInGridAsync`
- Use Uno WASM-compatible locators (automation IDs, `data-testid`, or text-based selectors)

### Playwright Test

> **Reference implementation:** See `sampleapp/src/Test/Test.PlaywrightUI/Tests/TodoItemCrudTests.cs`

### Key test patterns

- Inherit `PageTest` (Playwright.MSTest base class)
- Override `ContextOptions()` for `IgnoreHTTPSErrors = true`
- `[TestInitialize]` navigates to `BaseUrl`
- Use `[DataRow]` for parameterized CRUD scenarios
- Assert presence/absence with `WaitForSelectorAsync` + timeout-based absence check

---

## 5. Architecture Tests — Test.Architecture

Enforce Clean Architecture dependency rules using NetArchTest.

> **Reference implementation:** See `sampleapp/src/Test/Test.Architecture/DomainDependencyTests.cs`, `sampleapp/src/Test/Test.Architecture/ApplicationDependencyTests.cs`, `sampleapp/src/Test/Test.Architecture/ApiDependencyTests.cs`, `sampleapp/src/Test/Test.Architecture/StructuralConventionTests.cs`

### Key rules enforced

| Test | Rule |
|------|------|
| `DomainModel_HasNoDependencyOn_Application` | Domain.Model must not reference Application, Infrastructure, or EF Core |
| `DomainShared_HasNoDependencyOn_DomainModel` | Domain.Shared must not reference Domain.Model, Application, or Infrastructure |
| `ApplicationServices_HasNoDependencyOn_Infrastructure` | Services must not reference Infrastructure, EF Core, or Api |
| `Api_HasNoDependencyOn_DomainOrEF` | Api must not reference Domain entities, EF Core, or Infrastructure.Data |

### Key pattern

```csharp
// Base class provides assembly references via known types
public abstract class ArchitectureTestBase
{
    protected static readonly Assembly DomainModelAssembly = typeof(TodoItem).Assembly;
    protected static readonly Assembly ApplicationServicesAssembly = typeof(TodoItemService).Assembly;
    // ...
}
```

---

## 6. Load Tests — Test.Load (Optional)

Console application using NBomber for API load testing.

> **Reference implementation:** See `sampleapp/src/Test/Test.Load/TodoItemLoadTest.cs`, `sampleapp/src/Test/Test.Load/Program.cs`, `sampleapp/src/Test/Test.Load/Utility.cs`

### Key patterns

- Multi-step scenario: GET page → POST create → GET by ID
- `RampingInject` warm-up followed by steady `Inject` load
- `ReportFormat.Html` output for CI artifacts
- Utility class handles `HttpClient` factory, bearer token acquisition, self-signed cert support

---

## 7. Benchmark Tests — Test.Benchmarks (Optional)

Console application using BenchmarkDotNet.

> **Reference implementation:** See `sampleapp/src/Test/Test.Benchmarks/TodoItemBenchmarks.cs`, `sampleapp/src/Test/Test.Benchmarks/RepositoryBenchmarks.cs`, `sampleapp/src/Test/Test.Benchmarks/Program.cs`

### Key patterns

- `[MemoryDiagnoser]` + `[RankColumn]` for memory/speed ranking
- `[Params(10, 100, 1000)]` for parameterized data size benchmarks
- Service-layer benchmarks: Search, GetById, Create
- Repository-layer benchmarks: SearchWithFilter, GetById, GetAllProjection (inherits `DbIntegrationTestBase`)

---

## Mutation Testing (Stryker.NET)

Add `stryker-config.json` to `Test.Unit` for mutation testing:

```json
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/src/Stryker.Core/Stryker.Core/stryker-config.schema.json",
  "stryker-config": {
    "project": "{App}.Application.Services.csproj",
    "reporters": ["html", "progress"],
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 40
    }
  }
}
```

Run:
```bash
dotnet tool install --global dotnet-stryker
dotnet stryker
```

---

## coverlet.runsettings

Place at the solution root for code coverage:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>opencover</Format>
          <Exclude>[Test.*]*,[*.Tests]*</Exclude>
          <ExcludeByAttribute>GeneratedCode,CompilerGenerated</ExcludeByAttribute>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

---

## Testing Conventions Summary

| Concern | Convention |
|---------|-----------|
| **Framework** | MSTest (all projects) |
| **Mocking** | CleanMoq with `MockRepository` factory |
| **DB builder** | `InMemoryDbBuilder` fluent API for InMemory/SQLite |
| **Naming** | `{Method}_{Scenario}_{ExpectedResult}` |
| **Parallelism** | Method-level for unit/arch, class-level single-worker for endpoints |
| **Categories** | `[TestCategory]` for CI filtering |
| **DB reset** | Respawn for SQL, re-seed for InMemory |
| **Test containers** | Testcontainers.MsSql for real SQL tests |
| **Architecture** | NetArchTest for dependency enforcement |
| **E2E** | Playwright MSTest with Page Object Model |
| **Load** | NBomber console app (optional) |
| **Benchmark** | BenchmarkDotNet console app (optional) |
| **Mutation** | Stryker.NET (optional) |
| **Coverage** | coverlet with opencover format |

---

## Test Profile Decision Matrix

Use this matrix to decide what to scaffold for each testing profile:

| Test Project | `minimal` | `balanced` | `comprehensive` |
|-------------|:---------:|:----------:|:----------------:|
| `Test.Unit` | ✅ | ✅ | ✅ |
| `Test.Support` | ✅ | ✅ | ✅ |
| `Test.Integration` | — | ✅ | ✅ |
| `Test.Endpoints` | — | ✅ | ✅ |
| `Test.Architecture` | — | optional | ✅ |
| `Test.PlaywrightUI` | — | — | ✅ |
| `Test.Load` | — | — | optional |
| `Test.Benchmarks` | — | — | optional |
| Stryker mutation | — | — | optional |
| coverlet.runsettings | ✅ | ✅ | ✅ |

**Key:**
- ✅ = always scaffold
- optional = scaffold if explicitly requested in domain inputs
- — = do not scaffold at this profile

**CI test filtering by profile:**

| Profile | CI Filter | Approximate CI Time |
|---------|-----------|-------------------|
| `minimal` | `--filter "TestCategory=Unit"` | < 2 min |
| `balanced` | `--filter "TestCategory=Unit\|TestCategory=Endpoint\|TestCategory=Architecture"` | 3–8 min |
| `comprehensive` | All categories except `Load` and `Benchmark` | 8–20 min |

---

## Verification

After scaffolding test projects, verify:

- [ ] `dotnet build` compiles all test projects with zero errors
- [ ] `dotnet test --filter "TestCategory=Unit"` discovers and runs unit tests
- [ ] `InMemoryDbBuilder` creates a valid in-memory database with seed data
- [ ] Repository tests pass with both InMemory and SQLite providers
- [ ] Service tests mock repositories via `MockRepository` (CleanMoq)
- [ ] Mapper tests verify entity → DTO round-trip for each entity
- [ ] If `balanced` or above: endpoint tests run with `CustomApiFactory<Program>`
- [ ] If `balanced` or above: `Respawn` resets the database between test classes
- [ ] Architecture tests enforce dependency rules (Domain → no Infrastructure)
- [ ] Test categories (`[TestCategory("Unit")]`, etc.) are applied consistently
- [ ] `coverlet.runsettings` exists at the solution root
