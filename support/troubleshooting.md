# Troubleshooting — AI Agent Triage Rules

This file is intentionally lightweight. Use it to decide **what the AI should do next** when a build/test/run problem appears.

> **For compile/run commands and engineer actions, use [execution-gates.md](execution-gates.md) as the execution checklist.**

---

## Core Rule

AI agents generate code. Engineers own environment and runtime setup.

> All code generation and fixes apply to the **new project** only. If an error points to a pattern in `support/sampleapp-patterns.md`, document the issue in `support/UPDATE-INSTRUCTIONS.md`.

When an error appears:
1. Classify it (code-generation vs infrastructure/tooling)
2. Attempt **one** code-fix pass only when it is code-generation **in the new project**
3. If still failing (or infrastructure-related), log in `HANDOFF.md` and continue with non-blocked work

---

## Narrow Before Broad

After each material change, validate at the **smallest meaningful scope first**. Only escalate to broader builds, full-system runs, or browser sessions when the narrower check passes or cannot answer the question.

Validation ladder (prefer higher rows):

| Scope | When to use |
|---|---|
| Single file / `dotnet build` one project | After editing one project's code |
| `dotnet build` solution | After cross-project changes |
| `dotnet test --filter "TestCategory=Unit"` | After logic changes |
| `dotnet test` (full suite) | After wiring / DI / integration changes |
| Host startup / Aspire run | After config, startup, or infrastructure changes |
| Browser / E2E | Only after host-level checks pass |

Do not run a full-stack validation when a targeted build or endpoint check would isolate the issue faster.

---

## AI Fixes (One Pass Max)

The AI may fix:
- Missing `using`, `ProjectReference`, or package entries
- Missing DI registration, `DbSet<>`, endpoint map, or token substitution
- Namespace/path mismatches caused by scaffolding

Then run the phase validation command and continue only if green.

---

## Engineer-Owned Issues (Do Not Diagnose Deeply)

Flag immediately for the engineer when issues involve:
- NuGet auth/feeds, Docker/container startup, SQL connectivity
- Aspire env vars/ports, Functions runtime tools/config, Playwright install
- Certificates/credentials, cloud subscription access, deployment permissions

Reference the exact relevant section in [execution-gates.md](execution-gates.md).

---

## Distributed Host Debugging: Orchestration vs Application

When a multi-host app (Aspire, Gateway, API, Scheduler) fails at runtime, **separate orchestration failures from application failures** before investigating code.

**Step 1 — Confirm the substrate is running:**
- Docker containers started? (`docker ps`)
- Aspire dashboard reachable? (check the URL from `dotnet run` output — do not reuse a prior session's URL)
- All registered resources show healthy in the dashboard?
- Gateway/proxy routes respond (even with 401/404)?

**Step 2 — Only then investigate application-level concerns:**
- API returns expected status codes?
- Auth tokens/claims correct?
- Seed/migration data present?
- Config values (connection strings, feature flags) populated?

If Step 1 fails, the problem is infrastructure — flag for the engineer per [execution-gates.md](execution-gates.md). Do not debug application code when the host substrate is not ready.

**Step 3 — After fixing an infrastructure startup issue, verify at the data plane:**
Process liveness and clean logs are necessary but not sufficient. When a fix targets a component that creates or seeds backing data (migrations, seed tasks, scheduler tables, queue metadata), perform one direct data-plane check before declaring the fix complete:
- **Database:** query for expected tables, seed rows, or schema objects
- **Queue/bus:** confirm the queue or topic exists and is reachable
- **Storage:** verify the container or blob artifact was created

This prevents false-positive fixes where the crash is resolved but the intended persistence or seeding outcome was never achieved.

---

## Third-Party Operational Store Schema Triage

When a third-party library (scheduler, queue, dashboard, job runner) uses EF-backed or SQL-backed operational tables and startup or seeding fails:

1. **Identify the schema owner.** Does the library auto-create its tables at startup? Does it ship migrations or SQL scripts you must run? Does it expect a design-time factory in your project? Or does it assume tables already exist?
2. **Do not conflate startup success with schema presence.** Many libraries start without error even when their backing tables are missing — failures appear later during seeding, first job execution, or dashboard queries, and are easily misread as connection or configuration issues.
3. **Verify the schema directly.** Query `INFORMATION_SCHEMA.TABLES` or the database tool of your choice to confirm the expected tables exist before investigating application-level failures.
4. **Record the schema ownership model** in `HANDOFF.md` and `resource-implementation.yaml` so future sessions do not re-diagnose the same issue.

---

## Upstream Issue Diagnosis Discipline

When consulting upstream GitHub issues, PRs, or release notes to diagnose a third-party library failure:

1. **Extract the specific failure class** the upstream material addresses before applying its fix. Design-time migration problems, host-lifecycle startup problems, schema-default problems, and runtime missing-table problems are **separate fault domains** — treat them as distinct until proven otherwise.
2. **Match, don't pattern-match.** An upstream issue is relevant only if its failure mode matches your local failure mode, not just the library or feature area. A migration-generation fix does not resolve a runtime missing-table error, even if both involve the same library.
3. **Use upstream material to refine the diagnosis, not to short-circuit it.** If an upstream issue narrows the problem to a specific version, config flag, or code path, use that to focus your investigation — do not copy the fix verbatim without verifying the preconditions match.

---

## Domain Ambiguity Defaults

When inputs are unclear, prefer pragmatic defaults and continue:
- Relationship unclear → default to one-to-many and note assumption
- Missing properties → add `Name`; add `TenantId` when tenant-scoped
- Lite mode + Gateway requested → keep Lite baseline; suggest Gateway as a later increment

---

## Common Test Failures

Single source of truth for recurring scaffolding test failures and known fixes.
For phase gates and validation commands, see [execution-gates.md](execution-gates.md).

### Quick Reference

| Symptom | Root Cause | Fix |
|---|---|---|
| Search returns empty/0 results | `SearchRequest.PageSize` defaults to 0 | Send `{ PageSize = 100, PageIndex = 1 }` |
| Search returns 500 / negative OFFSET | `PageIndex = 0` with nonzero `PageSize` | Set `PageIndex = 1` (1-based) |
| Tenant-scoped queries return empty | `IRequestContext.TenantId` is null in test host | Override request context with fixed `TestTenantId` |
| All writes return `NotImplementedException` | Wrong `SaveChangesAsync` overload used | Use `SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct)` |
| Delete test passes but entity still exists | `Delete(entity)` not called | Call `repoTrxn.Delete(entity)` before save |
| FK violation in assign/reference tests | FK uses random GUID not existing row | Create related records first |
| Rate-limited 429 in integration tests | API rate limiter enabled in test host | Override limiter to `GetNoLimiter` in test factory |
| `CS0104` RequestContext ambiguity | `Test.Support` and `EF.Common.Contracts` both define type | Use fully qualified `EF.Common.Contracts.RequestContext<...>` |
| `IInternalMessageBus` resolution error in test factory | `AuditInterceptor` registered by Bootstrapper, not removed by test DB swap | Remove `AuditInterceptor<string, Guid?>` and all pool/factory descriptors. See [test-templates-endpoint.md](../templates/test-templates-endpoint.md) → *Pooled DbContext Swap*. |
| `Cannot consume scoped from singleton IDbContextPool` in tests | `AddDbContext` registers scoped options; original pooled factory registration still present | Remove `IDbContextPool<T>` descriptors (match by `ServiceType.FullName.Contains("DbContextPool")`) before re-registering |
| `DbContextOptions must be DbContextOptions<T>` in tests | Multiple contexts sharing non-generic `DbContextOptions` constructor | Build typed options per context via `new DbContextOptionsBuilder<T>().UseInMemoryDatabase(name).Options` |
| `CS9035` required member `AuditId` not set | `DbContextBase` declares `required` members; `new` operator enforces at compile time | Use `ConstructorInfo.Invoke()` via reflection to bypass — see `TestDbContextFactory` in [test-templates-endpoint.md](../templates/test-templates-endpoint.md) |
| Create returns 201 but validation test expects 400 | DTO fields not applied to entity after create | Call `entity.Update(...)` after factory create |
| Schema changes not reflected in TestContainer | Previous schema persists | `EnsureDeletedAsync()` before `EnsureCreatedAsync()` |
| ProblemDetails stack traces leak in CI | Debug diagnostics enabled in all builds | Wrap diagnostic customization in `#if DEBUG` |
| StructureValidator not found | Missing static validator namespace import | Add `using {Namespace}.Application.Services.Validation;` |
| WASM build `DirectoryNotFoundException` (`unoresizetizer`) | Resizetizer 1.12.1 manifest-path issue | See fix snippet in `skills/uno-ui.md` → *UnoSplashScreen WASM Build Failure* |
| `NotSupportedException` deserializing `Result<T>` in tests | `Result<T>` lacks parameterless constructor | Use `JsonDocument` parsing instead of `ReadFromJsonAsync<Result<T>>()`. Search endpoints serialize just the `PagedResponse<T>` value, not the wrapper. |

### Detailed Fix Patterns

#### 1) Tenant Query Filter in Tests

```csharp
services.AddScoped<IRequestContext<string, Guid?>>(provider =>
    new EF.Common.Contracts.RequestContext<string, Guid?>(
        Guid.NewGuid().ToString(), "Test.Endpoints", TestTenantId, []));
```

#### 2) SearchRequest Defaults

Always send explicit paging:

```csharp
new SearchRequest<MyFilter>
{
    PageSize = 100,
    PageIndex = 1,
    Filter = new MyFilter()
}
```

#### 3) Rate Limiter Override in Test Host

```csharp
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        _ => RateLimitPartition.GetNoLimiter("test"));
});
```

#### 4) SaveChangesAsync Overload

```csharp
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
```

#### 5) ProblemDetails Debug Leak Guard

```csharp
builder.Services.AddProblemDetails(options =>
{
#if DEBUG
    options.CustomizeProblemDetails = ctx =>
    {
        // debug-only exception detail
    };
#endif
});
```

---

## Razor vs C# Namespace Scoping

`global using` in a `.cs` GlobalUsings.cs file applies only to C# source files — it does NOT affect Razor component C# code blocks.
For every namespace used inside a `.razor` file, add an explicit `@using Namespace.Here` in the nearest `_Imports.razor`.
Rule of thumb: if a type is used both in `.cs` service layer code AND in `.razor` pages, it needs entries in BOTH `GlobalUsings.cs` AND `_Imports.razor`.

---

## Inspecting private NuGet package API surfaces

When a private package's public API is unknown or differs from docs/conventions:

1. Locate the DLL: find it under `%USERPROFILE%\.nuget\packages\{package}\{version}\lib\`.
2. Create a throwaway console project targeting `net10.0`:
   ```
   dotnet new console -o C:\Temp\InspectLib --framework net10.0
   ```
3. Add a `<Reference>` to the DLL in the `.csproj`.
   Preload any dependency assemblies (e.g., `Microsoft.Extensions.Logging.Abstractions`) with `Assembly.LoadFrom()` before calling `GetParameters()`.
4. Use `Assembly.LoadFrom(path)`, wrap `GetTypes()` in a `ReflectionTypeLoadException` catch, then iterate types/members.
5. `dotnet run` and inspect the output.

> **Note:** `Assembly.ReflectionOnlyLoadFrom()` is NOT supported on .NET 10 — use full load + exception handling instead.
> Binary text extraction (grep for strings in the DLL bytes) is useful as a quick fallback to spot type names before writing reflection code.

---

## Session State

When blocked, log in `HANDOFF.md` (see [template](HANDOFF.md)):
- Symptom + classification (`code-generation` or `infrastructure`)
- Current phase
- Next engineer action (link to [execution-gates.md](execution-gates.md))

If instruction gaps are discovered, append to `support/UPDATE-INSTRUCTIONS.md`.

