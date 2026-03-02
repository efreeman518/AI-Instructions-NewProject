# Test Gotchas (Canonical)

Single source of truth for recurring scaffolding test failures and known fixes.

If another file disagrees, this file wins.

---

## Quick Reference

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

---

## Detailed Fix Patterns

## 1) Tenant Query Filter in Tests

```csharp
services.AddScoped<IRequestContext<string, Guid?>>(provider =>
    new EF.Common.Contracts.RequestContext<string, Guid?>(
        Guid.NewGuid().ToString(), "Test.Endpoints", TestTenantId, []));
```

## 2) SearchRequest Defaults

Always send explicit paging:

```csharp
new SearchRequest<MyFilter>
{
    PageSize = 100,
    PageIndex = 1,
    Filter = new MyFilter()
}
```

## 3) Rate Limiter Override in Test Host

```csharp
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        _ => RateLimitPartition.GetNoLimiter("test"));
});
```

## 4) SaveChangesAsync Overload

```csharp
await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);
```

## 5) ProblemDetails Debug Leak Guard

```csharp
#if DEBUG
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
    {
        // debug-only exception detail
    });
#endif
```

---

## Canonical References

- Testing strategy: [skills/testing.md](skills/testing.md)
- Troubleshooting routing: [troubleshooting.md](troubleshooting.md)
- Data-access SaveChanges behavior: [skills/data-access.md](skills/data-access.md)
- Uno WASM workaround: [skills/uno-ui.md](skills/uno-ui.md)
