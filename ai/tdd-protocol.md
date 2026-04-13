# TDD Protocol

## Purpose

Defines the red/green loop for Phase 5a and Phase 5b, plus the tests-after protocol for Phase 5c and Phase 5d.

Phase 4 already generated interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Use those contracts to write tests first, then implement to green.

---

## TDD Enforcement Rules (Non-Negotiable)

> RED confirmation is mandatory. Do not skip it.

1. **Write tests FIRST.** Do not write any production code until the test file(s) for the current slice exist and compile.
2. **Confirm RED before implementing.** Run `dotnet test` and verify the new tests **fail with assertion errors**. If they pass against no-op stubs, tighten assertions until they fail. Record the failing test count.
3. **Implement ONLY enough to pass.** Write the minimum production code needed to make the failing tests pass. Do not add untested behavior.
4. **Confirm GREEN immediately.** Run `dotnet test` after implementation. All tests must pass. If any fail, fix before moving to the next slice.
5. **Never batch multiple slices.** Complete the full RED → GREEN cycle for one entity slice before starting the next.
6. **No simultaneous test + implementation files.** In a single file-generation pass, produce either test files OR implementation files — not both. The only exception is activating `{Entity}Builder.Build()` alongside entity implementation (Step 5 of Phase 5a).
7. **Do not accept compile-fail as RED.** Fix compile issues first, then confirm assertion-fail RED.

---

## BDD Naming Convention

All test methods use `Given_When_Then`.

Rules:
- `Given` describes the precondition or initial state
- `When` describes the action under test
- `Then` describes the expected outcome
- Use PascalCase segments separated by underscores
- Keep names descriptive but concise

---

## Phase 5a — Foundation TDD (Per Entity Slice)

Process each entity in the dependency order established in Phase 4 (parents first, then children).

1. **Write domain entity tests** in `Test/Test.Unit/Domain/{Entity}Tests.cs` from `test-templates-domain.md`.
2. **Write domain rule tests** in `Test/Test.Unit/Domain/{Entity}RulesTests.cs`.
3. **Run RED**:

```powershell
dotnet test --filter "TestCategory=Unit"
```

   Expected: tests fail with assertions or `NotImplementedException`, not compile errors.
4. **Implement entity + rules**:

- Replace `NotImplementedException` bodies in `Create()`, `Update()`, and rule methods.
- Activate `{Entity}Builder.Build()` only after `Create()` works.

5. **Run GREEN**:

```powershell
dotnet test --filter "TestCategory=Unit"
```

6. **Write repository tests** from `test-templates-repository.md`.
7. **Implement EF configuration + repositories**:

- Create `{Entity}Configuration.cs`.
- Implement `{Entity}RepositoryTrxn` and `{Entity}RepositoryQuery`.
- Wire `DbSet<{Entity}>` and swap the no-op DI registrations.

8. **Run GREEN again**:

```powershell
dotnet test --filter "TestCategory=Unit"
```

9. **Gate the slice**:

```powershell
dotnet ef migrations add InitialCreate ...
dotnet build
dotnet test --filter "TestCategory=Unit"
```

Git checkpoint after gate passes.

---

## Phase 5b — App Core TDD (Per Entity Slice)

1. **Write service tests** in `Test/Test.Unit/Services/{Entity}ServiceTests.cs` from `test-templates-service.md`.
2. **Run RED**:

```powershell
dotnet test --filter "TestCategory=Unit"
```

   Expected: new tests fail because no-op stubs return empty/default values.
3. **Implement service + mapper + validator** and replace the no-op service registration.
4. **Run GREEN (Unit)**:

```powershell
dotnet test --filter "TestCategory=Unit"
```

5. **Write endpoint tests** in `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs` from `test-templates-endpoint.md`.
6. **Implement endpoints** and wire endpoint registration.
7. **Run GREEN (Endpoint)**:

```powershell
dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"
```

Git checkpoint after gate passes.

---

## Phase 5c/5d — Tests-After Protocol

Infrastructure and optional host phases do not follow TDD. Instead, implement first, then write tests at the end of the session to verify behavior.

5c tests: health checks, configuration/no-op behavior, caching behavior when enabled.

5d tests: scheduler registration/trigger tests, Function trigger smoke tests, Uno client tests when applicable.

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

1. **No-op stub satisfies the assertion**: Tighten assertions. Assert on specific values, not just success or non-null.
2. **Wrong test target**: Verify the test is calling the code you think it's calling.

---

## Replacing No-Op Stubs

When implementing a real class that replaces a no-op stub:

1. Create the real implementation class
2. Update `RegisterServices.cs` to swap the no-op registration for the real class
3. Leave the no-op class in place or delete it
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
