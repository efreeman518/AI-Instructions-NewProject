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

## Common Test Failures (Quick Reference)

| Symptom | Root Cause | Fix |
|---|---|---|
| Search returns empty/0 results | `SearchRequest.PageSize` defaults to 0 | Send `{ PageSize = 100, PageIndex = 1 }` |
| Search returns 500 / negative OFFSET | `PageIndex = 0` with nonzero `PageSize` | Set `PageIndex = 1` (1-based) |
| All writes return 500 `NotImplementedException` | Using `SaveChangesAsync(ct)` instead of overload | Use `SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct)` |
| Tenant-scoped queries return empty | `IRequestContext.TenantId` is null in test host | Override `IRequestContext` in `ConfigureTestServices` with fixed `TestTenantId` |
| Delete tests pass but entity still exists | Service loads entity but never calls `Delete()` | Add `repoTrxn.Delete(entity)` before save |
| FK violation on assign/reference tests | Test uses `Guid.NewGuid()` for FK values | Create real related entities first |
| 429 TooManyRequests in long test sequences | API rate limiter (100 req/min default) | Override with `GetNoLimiter` in test factory |
| `CS0104` RequestContext ambiguity | `Test.Support.RequestContext` vs `EF.Common.Contracts.RequestContext` | Use fully qualified `new EF.Common.Contracts.RequestContext<...>(...)` |
| Create returns 201 but validation test expects 400 | `CreateAsync` doesn't apply DTO field to entity | Add `entity.Update(field: dto.Field)` after `Create()` |
| Schema changes not reflected in TestContainer | Old schema cached from previous run | Add `EnsureDeletedAsync()` before `EnsureCreatedAsync()` |
| WASM build `DirectoryNotFoundException` on `unoresizetizer\` | Resizetizer 1.12.1 sets `WasmPWAManifestFile` to a directory path when `UnoSplashScreen` is absent | Add `_FixWasmPwaManifestPath` MSBuild target (see `skills/uno-ui.md` Known Build Issues) |
| ProblemDetails leaks stack traces in CI/release | `AddProblemDetails` with `ex.ToStringDemystified()` runs in all configurations | Wrap diagnostic ProblemDetails block in `#if DEBUG` / `#endif` |
| Rate limiter returns 429 in tests | Rate limiting middleware active in test host | Disable rate limiter in `CustomApiFactory`: `services.Configure<RateLimiterOptions>(o => o.GlobalLimiter = PartitionedRateLimiter.CreateChained<HttpContext>())` |
| StructureValidator not found | Missing `using` for the static validator class | `StructureValidator` is static — no DI registration needed. Verify `using {Namespace}.Application.Services.Validation;` |

---

## Session State

When blocked, log in `HANDOFF.md` (see [template](HANDOFF.md)):
- Symptom + classification (`code-generation` or `infrastructure`)
- Current phase
- Next engineer action (link to [engineer-checklist.md](engineer-checklist.md))

If instruction gaps are discovered, append to `UPDATE-INSTRUCTIONS.md`.
