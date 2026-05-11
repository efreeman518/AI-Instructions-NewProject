# Contract Scaffolding — Phase 4

## Purpose

Generate the full solution structure, contract surface (interfaces, DTOs, entity shells), test infrastructure, and no-op DI stubs so that `dotnet build` succeeds on the entire solution — including test projects — before any real implementation begins in Phase 5.

This phase produces the compilable skeleton that enables TDD red/green cycles in Phase 5a and 5b.

## Inputs

- `domain-specification.yaml` (from Phase 1)
- `UBIQUITOUS-LANGUAGE.md` (from Phase 1)
- `DESIGN-DECISIONS.md` (from Phase 1)
- `resource-implementation.yaml` (from Phase 2)
- `implementation-plan.md` (from Phase 3)

## Pre-Gate

Before generating projects, verify shared base-type readiness for the configured `packageStrategy` (set in `resource-implementation.yaml`):

```powershell
dotnet restore
```

- If `packageStrategy: feed` or `hybrid` — fix `nuget.config` (and confirm `NUGET_AUTH_TOKEN` is set) before generating code. Do not scaffold local replacements for layers the feed provides.
- If `packageStrategy: local` or `hybrid` — confirm `packagePrefix` is set and every layer in `localPackageLayers` matches an entry in [`../support/ef-packages-reference.md`](../support/ef-packages-reference.md). These will be generated under `src/Packages/<packagePrefix>.<Layer>` as packable projects.

## Loaded Skills

- [solution-structure.md](../skills/solution-structure.md) — canonical folder layout, `.slnx`, dependency direction (and optional `src/Packages/`)
- [package-dependencies.md](../skills/package-dependencies.md) — shared base-type contracts and feed/local rules
- [placeholder-tokens.md](placeholder-tokens.md) — token substitution glossary
- [../support/ef-packages-reference.md](../support/ef-packages-reference.md) — base-type contract surface (do not regenerate into application/domain/host layers)

---

## What to Generate

### 1. Solution Structure

Follow `solution-structure.md` exactly:
- `.slnx`, `Directory.Packages.props`, `global.json`, `nuget.config`
- All project folders and `.csproj` files per the canonical layout
- Project references wired per the dependency direction contract
- Test projects: `Test.Support`, `Test.Unit`, `Test.Integration`, `Test.Endpoints`, `Test.E2E`, plus profile-specific projects (`Test.Architecture`, `Test.PlaywrightUI`, `Test.Load`, `Test.Benchmarks`) per `testingProfile`

### 2. Contracts (Per Entity)

For each entity defined in `resource-implementation.yaml`:

**Interfaces:**
```csharp
// Application.Contracts/Services/I{Entity}Service.cs
public interface I{Entity}Service
{
    Task<Result<DefaultResponse<{Entity}Dto>>> CreateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> UpdateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResponse<{Entity}Dto>>> SearchAsync(SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default);
}
```

```csharp
// Application.Contracts/Repositories/I{Entity}RepositoryTrxn.cs
// Application.Contracts/Repositories/I{Entity}RepositoryQuery.cs
```

Derive interface signatures from the entity's properties, relationships, and operations in `resource-implementation.yaml`. Use shared base types from `<packagePrefix>.*` (`IRepositoryBase`, `SearchRequest<T>`, `PagedResponse<T>`, etc.) — sourced from `customNugetFeeds` packages or `src/Packages/<packagePrefix>.*` projects per `packageStrategy`.

**DTOs:**
```csharp
// Application.Models/{Entity}Dto.cs
public class {Entity}Dto : IEntityBaseDto
{
    public Guid? Id { get; set; }
    // properties from resource-implementation.yaml
}

// Application.Models/{Entity}SearchFilter.cs
public class {Entity}SearchFilter
{
    public string? SearchTerm { get; set; }
    // filter-specific properties
}
```

**Enums:**
```csharp
// Domain.Shared/Enums/{Entity}Flags.cs (if defined)
```

### Integration Events

When externally published. Externally published event records belong in `Application.Contracts.Events`, not `Domain`. Generate one record per event the entity will publish across process boundaries:

```csharp
// Application.Contracts.Events/{Entity}CreatedIntegrationEvent.cs
public record {Entity}CreatedIntegrationEvent(Guid Id, Guid TenantId, /* fields safe to publish */);
```

Domain-only events (raised inside aggregates, handled in-process before integration mapping) stay in `Domain`. Do not publish `Domain` namespace events directly over transport — see [../skills/messaging.md](../skills/messaging.md) § Event Boundary Rule.

### 3. Entity Shells

Generate entity classes with the correct shape but no domain logic:

```csharp
// Domain.Model/{Entity}/{Entity}.cs
public class {Entity} : EntityBase, ITenantEntity<Guid>
{
    // Properties (from resource-implementation.yaml)
    public Guid TenantId { get; init; }
    public string Name { get; private set; } = null!;
    // ... all properties

    // SHELL: Phase 5a will implement domain logic
    private {Entity}() { }

    public static DomainResult<{Entity}> Create(Guid tenantId, string name)
        => throw new NotImplementedException("Shell — implement in Phase 5a");

    public DomainResult<{Entity}> Update(string? name = null)
        => throw new NotImplementedException("Shell — implement in Phase 5a");
}
```

**Shell rules:**
- Include all properties with correct types and access modifiers
- Include private parameterless constructor (EF requirement)
- Factory `Create()` and mutation `Update()` methods throw `NotImplementedException`
- Add comment `// SHELL: Phase 5a will implement domain logic` at top of class
- Child entity shells follow the same pattern
- Do NOT implement domain rules, validation, or business logic

### 4. Test Infrastructure

**Test.Support:**
- `UnitTestBase.cs` — shared `MockRepository` (see `test-templates.md` Common Setup as on-demand reference)
- `InMemoryDbBuilder.cs` — fluent in-memory/SQLite DB builder
- `DbSupport.cs` — test DB wiring for integration tests
- `Utility.cs` — config builder + random string helper
- `TestConstants.cs` — `DefaultTenantId`, `SystemUserId`

**Test Data Builders (per entity):**
```csharp
// Test.Support/Builders/{Entity}Builder.cs
public class {Entity}Builder
{
    private Guid _tenantId = TestConstants.DefaultTenantId;
    private string _name = "Test {Entity}";

    public {Entity}Builder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
    public {Entity}Builder WithName(string name) { _name = name; return this; }

    // SHELL: Returns null! until Phase 5a activates entity.Create()
    public {Entity} Build() => null!;
}
```

```csharp
// Test.Support/Builders/{Entity}DtoBuilder.cs — fully functional
public class {Entity}DtoBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test {Entity}";

    public {Entity}DtoBuilder WithId(Guid id) { _id = id; return this; }
    public {Entity}DtoBuilder WithName(string name) { _name = name; return this; }

    public {Entity}Dto Build() => new() { Id = _id, Name = _name };
}
```

**WebApplicationFactory base classes (consumed by `Test.Endpoints` and `Test.E2E`):**
- `CustomApiFactory<TProgram>` — `WebApplicationFactory` with in-memory DB and no `IHostedService`. Place in `Test.Endpoints` initially; the follow-up refactor (see plan) consolidates it into `Test.Support` so both `Test.Endpoints` and `Test.E2E` derive from a single shared base.
- `EndpointTestBase` / `DbIntegrationTestBase` — HTTP client factory + DB reset helpers

**Empty test project shells:**
- `Test.Unit/` — project file with MSTest + Moq references, no test classes yet (Phase 5a adds them)
- `Test.Integration/` — project file with MSTest + Testcontainers + EF, no test classes yet (Phase 5d adds service-level integration tests against real external services as part of the quality regression)
- `Test.Endpoints/` — project file with MSTest + `Microsoft.AspNetCore.Mvc.Testing`, no test classes yet (Phase 5b adds endpoint contract tests via WebApplicationFactory)
- `Test.E2E/` — project file with MSTest + `Microsoft.AspNetCore.Mvc.Testing` + Testcontainers, no test classes yet (Phase 5d adds multi-endpoint workflow tests against Testcontainers SQL)

### 5. No-Op DI Stubs

For every interface, generate a no-op implementation:

```csharp
// Infrastructure/{Project}.Infrastructure.Stubs/NoOp{Entity}Service.cs
public class NoOp{Entity}Service : I{Entity}Service
{
    public Task<Result<DefaultResponse<{Entity}Dto>>> CreateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default)
        => Task.FromResult(Result<DefaultResponse<{Entity}Dto>>.Success(new DefaultResponse<{Entity}Dto>()));

    // ... all interface methods with safe default returns
}
```

**No-Op method bodies must return safe defaults — never `throw new NotImplementedException()`.** This holds even for entities the scaffold contracts but does not activate (`TryAddSingleton`/`TryAddScoped` fallback wiring). Throwing inside a No-Op breaks the final-scaffold checklist and converts a silently inactive entity into a runtime crash if anything ever does resolve it. Use these safe-default shapes:

- `Result<T>` → `Result<T>.Success(default!)` or a constructed empty payload
- `Task` → `Task.CompletedTask`
- `Task<T>` → `Task.FromResult(default(T)!)`
- `IEnumerable<T>` / `IList<T>` → `Array.Empty<T>()` / `new List<T>()`
- `bool` → `false`

Throwing is permitted **only** when no safe default exists for the return shape (e.g., a non-nullable abstract type with no parameterless ctor) — in which case the file becomes part of the scaffold-skipped surface allowed by `support/final-scaffold-checklist.md`.

Register in `RegisterServices.cs`:
```csharp
// Bootstrapper/RegisterServices.cs — no-op stubs (replaced with real implementations in Phase 5a/5b)
services.AddScoped<I{Entity}RepositoryTrxn, NoOp{Entity}RepositoryTrxn>();
services.AddScoped<I{Entity}RepositoryQuery, NoOp{Entity}RepositoryQuery>();
services.AddScoped<I{Entity}Service, NoOp{Entity}Service>();
```

### 6. DbContext Shells

```csharp
// Infrastructure.Data/{App}DbContextTrxn.cs
public class {App}DbContextTrxn : DbContextBase<Guid, Guid>
{
    public DbSet<{Entity}> {Entities} => Set<{Entity}>();
    // ... per entity
    // SHELL: OnModelCreating with empty configuration (Phase 5a adds EF configs)
}

// Infrastructure.Data/{App}DbContextQuery.cs
public class {App}DbContextQuery : {App}DbContextTrxn
{
    // read-only context configuration (Phase 5a finalizes)
}
```

---

## Entity Ordering

Generate entities in dependency order: parent entities first, then children. Use the relationship graph from `resource-implementation.yaml` to determine ordering. An entity with no parent dependencies is generated first.

---

## Gate

```powershell
dotnet restore
dotnet build
```

The entire solution — including all test projects — must compile successfully. `dotnet restore` must succeed against the configured private feed (with `NUGET_AUTH_TOKEN` set). All tests are either absent (empty projects) or trivially pass. No `dotnet test` run is required at this gate. Developer reviews the scaffolded shape against the verification checklist below.

---

## Post-Gate

1. Git checkpoint (commit the contract scaffold).
2. Update `HANDOFF.md`:
   - `currentPhase: "5"`
   - `currentSubPhase: "5a"`
   - `contractsScaffolded: true`
   - Record the entity ordering used and any deviations from `resource-implementation.yaml`.
3. Close session.

---

## What NOT to Generate

- **No domain logic** — entity `Create()`/`Update()` methods are shells
- **No EF configurations** — `OnModelCreating` is empty (Phase 5a)
- **No repository implementations** — only interfaces + no-op stubs (Phase 5a)
- **No service implementations** — only interfaces + no-op stubs (Phase 5b)
- **No API endpoints** — endpoint classes are Phase 5b
- **No mapper implementations** — mappers are Phase 5b
- **No test methods** — test projects exist but are empty (Phase 5a/5b write tests)

---

## Verification

- [ ] `.slnx` exists and includes all projects
- [ ] `dotnet build` succeeds from solution root
- [ ] Every entity from `resource-implementation.yaml` has: interface, DTO, entity shell, builders
- [ ] All no-op stubs satisfy their interfaces (no abstract/unimplemented methods)
- [ ] Test.Support contains `UnitTestBase`, `InMemoryDbBuilder`, `DbSupport`, `Utility`, `TestConstants`
- [ ] Test data `{Entity}DtoBuilder` returns valid DTOs
- [ ] `RegisterServices.cs` wires all no-op stubs
- [ ] No domain logic in entity shells (only `throw new NotImplementedException`)
- [ ] No local reimplementation of `<packagePrefix>.*` shared base types into application/domain/host layers (they live in feed packages or `src/Packages/<packagePrefix>.*` only)
- [ ] `dotnet restore` exits 0. For `feed`/`hybrid`: `NUGET_AUTH_TOKEN` is set and all feed-supplied `<packagePrefix>.*` packages resolve. For `local`/`hybrid`: every layer in `localPackageLayers` exists as a project under `src/Packages/<packagePrefix>.<Layer>` and is referenced via `<ProjectReference>`
- [ ] Developer reviews the scaffolded shape against the items above
- [ ] Token placeholders follow [placeholder-tokens.md](placeholder-tokens.md)
