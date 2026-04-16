# TaskFlow Proof Map

Use this file when you need to prove that an instruction, pattern, or scaffolded output already exists in the TaskFlow reference app.

Reference app repository: <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

Load this file on demand. Keep it out of the default phase context.

> **TaskFlow is a multi-tenant application.** It demonstrates tenant boundary validation, tenant query filters, tenant-scoped services, and global-admin bypass. When scaffolding a single-tenant app, the multi-tenant patterns shown in TaskFlow do not apply — see `// [MULTI-TENANT]` markers in the service template.

---

## How to Use It

1. Find the current phase or concern.
2. Jump to the matching TaskFlow area.
3. Verify structure, wiring, and naming there before inventing a new pattern.
4. Generate code for the target project; do not copy TaskFlow files wholesale.

---

## Phase Proof Map

| Phase / Concern | TaskFlow area to inspect | What it proves |
|---|---|---|
| Phase 4 contract scaffolding | `src/Domain/TaskFlow.Domain.Model`, `src/Application/TaskFlow.Application.Contracts`, `src/Application/TaskFlow.Application.Models`, `src/Test/Test.Support` | Entity shells, contracts, DTOs, builders, and test infrastructure exist before TDD starts. |
| Phase 5a domain model | `src/Domain/TaskFlow.Domain.Model` | `Create()` / `Update()` patterns, value objects, domain rules, and aggregate shape. |
| Phase 5a data persistence | `src/Infrastructure/TaskFlow.Infrastructure.Data`, `src/Infrastructure/TaskFlow.Infrastructure.Repositories` | Dual DbContext split, EF configuration, repository split, and save/query separation. |
| Phase 5b application layer | `src/Application/TaskFlow.Application.Services`, `src/Application/TaskFlow.Application.Mappers` | Service shape, result flow, mapper conventions, validator placement, `BuildResponse` helper, `ErrorConstants` usage, `nameof(Entity)`, `[LoggerMessage]` source-gen logging. Multi-tenant: tenant boundary validation, tenant filter manipulation logging, `PreventTenantChange` in Update. |
| Phase 5b API endpoints | `src/Host/TaskFlow.Api` | Minimal API grouping, endpoint conventions, exception handling, and registration flow. |
| Phase 5c runtime wiring | `src/Host/TaskFlow.Api`, `src/Host/TaskFlow.Bootstrapper`, `src/Aspire/AppHost` | Middleware order, DI composition, app host resources, runtime config, and deployment shape. |
| Gateway | `src/Host/TaskFlow.Gateway` | YARP routing, token forwarding, claims transformation, and CORS wiring. |
| Caching | API + Bootstrapper + cache registrations | FusionCache + Redis backplane patterns and cache-key conventions. |
| Multitenancy | Request context handling in API + service layer | Tenant extraction, tenant boundary validation, global-admin bypass, tenant filter manipulation logging, `PreventTenantChange`, `ValidationHelper` delegation, `[LoggerMessage]` source-gen. |
| Phase 5d scheduler | `src/Host/TaskFlow.Scheduler` | TickerQ job registration, cron jobs, and scheduled handler structure. |
| Phase 5d functions | `src/Host/TaskFlow.Functions` | Function-project structure, trigger layout, and placeholder-host patterns. |
| Phase 5d Uno UI | `src/UI/TaskFlow.Uno` | UI project structure, feature grouping, and gateway-backed client flow. |
| Phase 5e quality | `src/Test/Test.Unit`, `src/Test/Test.Architecture`, `src/Test/Test.Load`, `src/Test/Test.Benchmarks` | Test project layout and quality-gate coverage. |
| Phase 5f auth | `AuthConfiguration`, `ScaffoldAuthHandler`, gateway claims forwarding flow | Scaffold auth, Entra-ready wiring, and API claim enrichment path. |
| Phase 5g AI | `src/Infrastructure/TaskFlow.Infrastructure.AI`, commented AI resources in AppHost | AI service placement, deployment-only stance, and config-driven activation. |

---

## High-Value Proof Checks

- **Multi-tenant proof:** TaskFlow demonstrates full multi-tenancy — `ITenantEntity<Guid>`, `ITenantBoundaryValidator`, `ValidationHelper`, `TenantBoundaryLoggingExtensions`, tenant query filters, tenant stamping, and global-admin bypass. Not all scaffolds require multi-tenancy.
- **Service pattern proof:** TaskFlow services use `BuildResponse` helper, `ErrorConstants.ERROR_ITEM_NOTFOUND`, `nameof(Entity)`, `[LoggerMessage]` source-gen logging, and `DefaultRequest<T>`/`DefaultResponse<T>` as `record` types.
- **Dual DbContext proof:** TaskFlow uses `TaskFlowDbContextTrxn` for writes and `TaskFlowDbContextQuery` for read-only/no-tracking access.
- **Repository proof:** TaskFlow splits repository contracts and implementations into transaction and query variants.
- **Middleware proof:** The API pipeline is ordered as security headers → correlation ID → exception handling → rate limiting → auth → gateway claim enrichment → authorization → endpoints.
- **Gateway proof:** The gateway forwards bearer tokens and original claims through an encoded header.
- **Scheduler proof:** TickerQ jobs are registered as explicit scheduled handlers, not hidden inside random hosted services.
- **Scaffold-auth proof:** Local/dev completion does not require live cloud auth; scaffold auth supplies trusted claims until Phase 5f finalizes identity.

---

## When To Load This File

- A skill or template describes a pattern but the concrete shape is still ambiguous.
- You need to verify that the instruction set already has a working example.
- You want a fast pointer into TaskFlow without searching the whole repo.