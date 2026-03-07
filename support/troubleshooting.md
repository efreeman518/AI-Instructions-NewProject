# Troubleshooting — AI Agent Triage Rules

This file is intentionally lightweight. Use it to decide **what the AI should do next** when a build/test/run problem appears.

> **For compile/run commands and engineer actions, use [engineer-checklist.md](engineer-checklist.md) as the execution checklist.**

---

## Core Rule

AI agents generate code. Engineers own environment and runtime setup.

> **⛔ NEVER fix, modify, or build sampleapp code unless explicitly instructed to do so.** The `sample-app/` directory is read-only reference. All code generation and fixes apply to the **new project** only. If an error points to a sampleapp file, document the issue and fix only in `UPDATE-INSTRUCTIONS.md` — it is not your code to fix directly.

When an error appears:
1. Classify it (code-generation vs infrastructure/tooling)
2. Attempt **one** code-fix pass only when it is code-generation **in the new project**
3. If still failing (or infrastructure-related), log in `HANDOFF.md` and continue with non-blocked work

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

Reference the exact relevant section in [engineer-checklist.md](engineer-checklist.md).

---

## Domain Ambiguity Defaults

When inputs are unclear, prefer pragmatic defaults and continue:
- Relationship unclear → default to one-to-many and note assumption
- Missing properties → add `Name`; add `TenantId` when tenant-scoped
- Lite mode + Gateway requested → keep Lite baseline; suggest Gateway as a later increment

---

## Common Test Failures

Single source of truth for recurring scaffolding test failures and known fixes.
If another file disagrees, this section wins.

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
| Create returns 201 but validation test expects 400 | DTO fields not applied to entity after create | Call `entity.Update(...)` after factory create |
| Schema changes not reflected in TestContainer | Previous schema persists | `EnsureDeletedAsync()` before `EnsureCreatedAsync()` |
| ProblemDetails stack traces leak in CI | Debug diagnostics enabled in all builds | Wrap diagnostic customization in `#if DEBUG` |
| StructureValidator not found | Missing static validator namespace import | Add `using {Namespace}.Application.Services.Validation;` |
| WASM build `DirectoryNotFoundException` (`unoresizetizer`) | Resizetizer 1.12.1 manifest-path issue | Apply `_FixWasmPwaManifestPath` target |

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

## Session State

When blocked, log in `HANDOFF.md` (see [template](HANDOFF.md)):
- Symptom + classification (`code-generation` or `infrastructure`)
- Current phase
- Next engineer action (link to [engineer-checklist.md](engineer-checklist.md))

If instruction gaps are discovered, append to `support/UPDATE-INSTRUCTIONS.md`.

