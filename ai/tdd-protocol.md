# TDD Protocol

## Purpose

Defines the red/green TDD cycle used during Phase 5a (Foundation) and Phase 5b (App Core), and the tests-after protocol used during Phase 5c/5d (Infrastructure/Hosts).

Phase 4 has already generated the contract surface — interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. This protocol tells you how to use those contracts to write tests first, then implement to green.

---

## BDD Naming Convention

All test methods use the `Given_When_Then` pattern:

```csharp
[TestMethod]
[TestCategory("Unit")]
public async Task Given_ValidInput_When_EntityCreated_Then_ReturnsSuccess() { }

[TestMethod]
[TestCategory("Unit")]
public async Task Given_EmptyName_When_EntityCreated_Then_ReturnsDomainFailure() { }

[TestMethod]
[TestCategory("Endpoint")]
[TestCategory("Integration")]
public async Task Given_ValidPayload_When_PostEntity_Then_Returns201WithLocationHeader() { }
```

Rules:
- `Given` describes the precondition or initial state
- `When` describes the action under test
- `Then` describes the expected outcome
- Use PascalCase segments separated by underscores
- Keep names descriptive but concise — the test body provides full detail

---

## Phase 5a — Foundation TDD (Per Entity Slice)

Process each entity in the dependency order established in Phase 4 (parents first, then children).

### Step 1: Activate Entity Builder

Replace the `null!` placeholder in `{Entity}Builder.Build()`:

```csharp
// Before (shell from Phase 4):
public {Entity} Build() => null!;

// After (activated — requires entity Create() to work):
public {Entity} Build() => {Entity}.Create(_tenantId, _name).Value!;
```

> **Do not activate the builder yet** — it will fail until Step 5. Write it as part of Step 5 after entity logic is implemented.

### Step 2: Write Domain Entity Tests

Create `Test/Test.Unit/Domain/{Entity}Tests.cs` using `test-templates-domain.md`.

Minimum test methods:
- `Given_ValidInput_When_EntityCreated_Then_ReturnsSuccess`
- `Given_InvalidInput_When_EntityCreated_Then_ReturnsDomainFailure` (one per validation rule)
- `Given_ExistingEntity_When_Updated_Then_ReturnsUpdatedValues`
- `Given_NullUpdate_When_Updated_Then_OriginalValuesPreserved`
- Child collection tests if the entity has children

### Step 3: Write Domain Rule Tests

Create `Test/Test.Unit/Domain/{Entity}RulesTests.cs` using `test-templates-domain.md`.

### Step 4: Run Red

```powershell
dotnet test --filter "TestCategory=Unit"
```

**Expected:** Tests fail because entity shell methods throw `NotImplementedException`.

> If tests fail to **compile**, fix compilation errors first. The entity shell, interfaces, and DTOs from Phase 4 should provide all types needed. If a type is missing, check Phase 4 output before creating new types.

### Step 5: Implement Entity + Activate Builder

- Replace `NotImplementedException` bodies in entity `Create()`, `Update()`, and domain rule methods with real logic
- Implement domain rules in `Domain.Model/Rules/`
- Activate `{Entity}Builder.Build()` to call real `Create()`

### Step 6: Run Green

```powershell
dotnet test --filter "TestCategory=Unit"
```

**Expected:** All domain entity and rule tests pass.

### Step 7: Write Repository Tests

Create `Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs` and `{Entity}RepositoryQueryTests.cs` using `test-templates-repository.md`.

Repository tests use `InMemoryDbBuilder` from Test.Support (generated in Phase 4).

### Step 8: Implement EF Configuration + Repository

- Create `{Entity}Configuration.cs` (Fluent API)
- Implement `{Entity}RepositoryTrxn` and `{Entity}RepositoryQuery`
- Wire `DbSet<{Entity}>` in DbContext `OnModelCreating`
- Replace no-op repository stubs in `RegisterServices.cs` with real implementations

### Step 9: Run Green

```powershell
dotnet test --filter "TestCategory=Unit"
```

**Expected:** All unit tests pass including repository tests.

### Step 10: Scaffold Migration + Gate

```powershell
dotnet ef migrations add InitialCreate ...
dotnet build
dotnet test --filter "TestCategory=Unit"
```

Git checkpoint after gate passes.

---

## Phase 5b — App Core TDD (Per Entity Slice)

### Step 1: Write Service Unit Tests

Create `Test/Test.Unit/Services/{Entity}ServiceTests.cs` using `test-templates-service.md`.

Tests mock interfaces from Phase 4 using Moq:
```csharp
var repoTrxnMock = _mockFactory.Create<I{Entity}RepositoryTrxn>();
var repoQueryMock = _mockFactory.Create<I{Entity}RepositoryQuery>();
```

Minimum test methods:
- `Given_ValidDto_When_CreateAsync_Then_ReturnsSuccessResult`
- `Given_NonExistentEntity_When_UpdateAsync_Then_ReturnsNone`
- `Given_ExistingEntity_When_DeleteAsync_Then_ReturnsSuccessAndCallsDelete`
- `Given_ExistingEntity_When_GetAsync_Then_ReturnsMappedDto`
- `Given_SearchFilter_When_SearchAsync_Then_ReturnsFilteredPage`

### Step 2: Run Red

```powershell
dotnet test --filter "TestCategory=Unit"
```

**Expected:** New service tests fail (no-op stub returns empty/default results that don't match assertions).

### Step 3: Implement Service + Mapper + Validator

- Create `{Entity}Service` implementing `I{Entity}Service`
- Create static mapper extensions (`ToDto()`, `ToEntity()`)
- Create `{Entity}StructureValidator` for input validation
- Replace no-op service stub in `RegisterServices.cs` with real implementation

### Step 4: Run Green (Unit)

```powershell
dotnet test --filter "TestCategory=Unit"
```

### Step 5: Write Endpoint Integration Tests

Create `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs` using `test-templates-endpoint.md`.

Uses `CustomApiFactory<TProgram>` from Test.Integration (generated in Phase 4).

Minimum test methods:
- `Given_ValidPayload_When_PostEntity_Then_Returns201`
- `Given_NonExistentId_When_GetEntity_Then_Returns404`
- `Given_ExistingEntities_When_SearchWithFilter_Then_ReturnsFilteredPage`
- `CRUD_Pass` (full create → read → update → delete cycle)

### Step 6: Implement Endpoints

- Create `{Entity}Endpoints.cs` with minimal API endpoint mappings
- Wire endpoint registration in `WebApplicationBuilderExtensions.cs`

### Step 7: Run Green (Endpoint)

```powershell
dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"
```

Git checkpoint after gate passes.

---

## Phase 5c/5d — Tests-After Protocol

Infrastructure and optional host phases do not follow TDD. Instead, implement first, then write tests at the end of the session to verify behavior.

### 5c — Runtime/Edge Tests

After implementing infrastructure concerns, write:
- Health check integration tests (verify health endpoint returns 200)
- Configuration loading tests (verify absent config → no-op passthrough)
- Caching behavior tests if caching is enabled

```powershell
dotnet test
```

### 5d — Optional Hosts Tests

After implementing each optional host, write:
- Scheduler: job registration and trigger tests
- Function App: function trigger smoke tests
- Uno UI: client layer unit tests (if applicable)

```powershell
dotnet test
```

---

## Red State Troubleshooting

If tests fail to compile (not just fail assertions):

1. **Missing type**: Check Phase 4 output. The type should exist as a contract/shell.
2. **Missing project reference**: Add `<ProjectReference>` to the test project's `.csproj`.
3. **Missing package**: Add to `Directory.Packages.props` and restore.
4. **Wrong namespace**: Verify against `placeholder-tokens.md` substitutions.

If tests pass unexpectedly (should be red but are green):

1. **No-op stub returns a value that satisfies the assertion**: Tighten assertions. Assert on specific property values, not just success/non-null.
2. **Wrong test target**: Verify the test is calling the code you think it's calling.

---

## Replacing No-Op Stubs

When implementing a real class that replaces a no-op stub:

1. Create the real implementation class
2. Update `RegisterServices.cs` — change the no-op registration to the real class:
   ```csharp
   // Before (no-op from Phase 4):
   services.AddScoped<I{Entity}Service, NoOp{Entity}Service>();
   
   // After (real implementation):
   services.AddScoped<I{Entity}Service, {Entity}Service>();
   ```
3. The no-op stub class can be left in place (it's harmless) or deleted — your choice
4. Verify `dotnet build` still succeeds after the swap

---

## Slice Completion Checklist

A vertical slice is TDD-complete when:

- [ ] Entity tests exist and pass (5a)
- [ ] Domain rule tests exist and pass (5a)
- [ ] Repository tests exist and pass (5a)
- [ ] Service tests exist and pass (5b)
- [ ] Endpoint tests exist and pass (5b)
- [ ] All no-op stubs for this entity are replaced with real implementations
- [ ] `{Entity}Builder.Build()` is activated and returns a valid entity
- [ ] `{Entity}DtoBuilder.Build()` returns a valid DTO (should already work from 4)
