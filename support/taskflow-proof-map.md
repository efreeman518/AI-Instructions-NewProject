# TaskFlow Proof Map

Use this file when you need to prove that an instruction, pattern, or scaffolded output already exists in the TaskFlow reference app.

**Local sibling clone preferred:** if `../AI-Instructions-ReferenceApp/` exists relative to the target project's parent, read TaskFlow files via the Read tool — paths in the proof table below are relative to the TaskFlow repo root, so prefix with `../AI-Instructions-ReferenceApp/`. Fall back to GitHub MCP only when the local clone is absent: <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

Load this file on demand. Keep it out of the default phase context.

> **TaskFlow is a multi-tenant application.** It demonstrates tenant boundary validation, tenant query filters, tenant-scoped services, and global-admin bypass. When scaffolding a single-tenant app, the multi-tenant patterns shown in TaskFlow do not apply — see `// [MULTI-TENANT]` markers in the service template.

---

## How to Use It

1. Find the current phase or concern.
2. Jump to the matching TaskFlow area.
3. Verify structure, wiring, and naming there before inventing a new pattern.
4. Generate code for the target project. Reference application rules (do-not-copy-wholesale, etc.) live in [../START-AI.md](../START-AI.md) § Reference Application.

---

## Phase Proof Map

| Phase / Concern | TaskFlow area to inspect | What it proves |
|---|---|---|
| Phase 1 shared language | `UBIQUITOUS-LANGUAGE.md`, `DESIGN-DECISIONS.md`, `domain-specification.yaml`, `implementation-plan.md` | Shared terminology, rejected synonyms, decision dependencies, and vertical slice order are explicit before code generation. |
| Phase 4 contract scaffolding | `src/Domain/TaskFlow.Domain.Model`, `src/Application/TaskFlow.Application.Contracts`, `src/Application/TaskFlow.Application.Models`, `src/Test/Test.Support` | Entity shells, contracts, DTOs, builders, and test infrastructure exist before TDD starts. |
| Phase 5a domain model | `src/Domain/TaskFlow.Domain.Model` | `Create()` / `Update()` patterns, value objects, domain rules, and aggregate shape. |
| Phase 5a domain shared | `src/Domain/TaskFlow.Domain.Shared` | Shared enums, value-object base types, cross-aggregate primitives. |
| Phase 5a data persistence | `src/Infrastructure/TaskFlow.Infrastructure.Data`, `src/Infrastructure/TaskFlow.Infrastructure.Repositories` | Dual DbContext split, EF configuration, repository split, and save/query separation. |
| Phase 5b application layer | `src/Application/TaskFlow.Application.Services`, `src/Application/TaskFlow.Application.Mappers` | Service shape, result flow, mapper conventions, validator placement, `BuildResponse` helper, `ErrorConstants` usage, `nameof(Entity)`, `[LoggerMessage]` source-gen logging. Multi-tenant: tenant boundary validation, tenant filter manipulation logging, `PreventTenantChange` in Update. |
| Phase 5b message handlers | `src/Application/TaskFlow.Application.MessageHandlers` | Domain-event and integration-event consumer pattern, separate from aggregate emission. |
| Phase 5b storage / external infrastructure | `src/Infrastructure/TaskFlow.Infrastructure.Storage` | Blob, Service Bus, and Cosmos repositories with no-op stubs for unconfigured states. |
| Phase 5b API endpoints | `src/Host/TaskFlow.Api` | Minimal API grouping, endpoint conventions, exception handling, and registration flow. |
| Phase 5c runtime wiring | `src/Host/TaskFlow.Api`, `src/Host/TaskFlow.Bootstrapper`, `src/Host/Aspire/AppHost` | Middleware order, DI composition, app host resources, runtime config, and deployment shape. |
| Phase 5c Aspire service defaults | `src/Host/Aspire/ServiceDefaults` | OpenTelemetry wiring, `/healthz` and `/readyz` defaults, shared host registration. |
| Gateway | `src/Host/TaskFlow.Gateway` | YARP routing, token forwarding, claims transformation, and CORS wiring. |
| Caching | API + Bootstrapper + cache registrations | FusionCache + Redis backplane patterns and cache-key conventions. |
| Multitenancy | Request context handling in API + service layer | Tenant extraction, tenant boundary validation, global-admin bypass, tenant filter manipulation logging, `PreventTenantChange`, `ValidationHelper` delegation, `[LoggerMessage]` source-gen. |
| Phase 5c scheduler | `src/Host/TaskFlow.Scheduler` | TickerQ job registration, cron jobs, and scheduled handler structure. |
| Phase 5c functions | `src/Host/TaskFlow.Functions` | Function-project structure, trigger layout, and placeholder-host patterns. |
| Phase 5c Uno UI | `src/UI/TaskFlow.Uno` | UI project structure, feature grouping, and gateway-backed client flow. |
| Phase 5c Uno core | `src/UI/TaskFlow.Uno.Core` | Plain-`net10.0` library extracted from the Uno project so business logic and Kiota client are unit-testable without Uno SDK. |
| Phase 5c Blazor host | `src/UI/TaskFlow.Blazor` | Blazor alternative to Uno UI; same Gateway-backed client flow. |
| Phase 5d quality (.NET test projects) | `src/Test/Test.Unit`, `src/Test/Test.Integration`, `src/Test/Test.Endpoints`, `src/Test/Test.E2E`, `src/Test/Test.Architecture`, `src/Test/Test.Load`, `src/Test/Test.Benchmarks`, `src/Test/Test.Support` | `dotnet test`-runnable test project layout and quality-gate coverage. |
| Phase 5d browser UI tests (Node) | `src/Test/Test.PlaywrightUI` | Node.js Playwright suite. Runs against a hosted Aspire AppHost stack via `npm test`. **Not a `dotnet test` target** — invoking it that way will fail. |
| Phase 5e auth | `AuthConfiguration`, `ScaffoldAuthHandler`, gateway claims forwarding flow | Scaffold auth, Entra-ready wiring, and API claim enrichment path. |
| Phase 5e AI | `src/Infrastructure/TaskFlow.Infrastructure.AI`, commented AI resources in AppHost | AI service placement, deployment-only stance, and config-driven activation. |

---

## Direct Proof Links

Use these links first. If a branch or path has moved, search inside the same repository for the path suffix or type name; do not invent a new pattern.

| Concern | Direct link |
|---|---|
| Ubiquitous language | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/blob/main/UBIQUITOUS-LANGUAGE.md> |
| Design decisions | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/blob/main/DESIGN-DECISIONS.md> |
| Domain model | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Domain/TaskFlow.Domain.Model> |
| Domain shared | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Domain/TaskFlow.Domain.Shared> |
| Application contracts | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Application/TaskFlow.Application.Contracts> |
| Application models | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Application/TaskFlow.Application.Models> |
| Application services | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Application/TaskFlow.Application.Services> |
| Application mappers | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Application/TaskFlow.Application.Mappers> |
| Application message handlers | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Application/TaskFlow.Application.MessageHandlers> |
| Data infrastructure | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Infrastructure/TaskFlow.Infrastructure.Data> |
| Repositories | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Infrastructure/TaskFlow.Infrastructure.Repositories> |
| Storage / external infrastructure | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Infrastructure/TaskFlow.Infrastructure.Storage> |
| AI infrastructure | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Infrastructure/TaskFlow.Infrastructure.AI> |
| API host | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/TaskFlow.Api> |
| Bootstrapper | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/TaskFlow.Bootstrapper> |
| Aspire AppHost | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/Aspire/AppHost> |
| Aspire service defaults | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/Aspire/ServiceDefaults> |
| Gateway | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/TaskFlow.Gateway> |
| Scheduler | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/TaskFlow.Scheduler> |
| Functions | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Host/TaskFlow.Functions> |
| Uno UI | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/UI/TaskFlow.Uno> |
| Uno core (testable) | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/UI/TaskFlow.Uno.Core> |
| Blazor host | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/UI/TaskFlow.Blazor> |
| Test support | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.Support> |
| Unit tests | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.Unit> |
| Architecture tests | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.Architecture> |
| Endpoint tests | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.Endpoints> |
| E2E tests | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.E2E> |
| Playwright UI tests (Node) | <https://github.com/efreeman518/AI-Instructions-ReferenceApp/tree/main/src/Test/Test.PlaywrightUI> |

---

## High-Value Proof Checks

- **Multi-tenant proof:** TaskFlow demonstrates full multi-tenancy — `ITenantEntity<Guid>`, `ITenantBoundaryValidator`, `ValidationHelper`, `TenantBoundaryLoggingExtensions`, tenant query filters, tenant stamping, and global-admin bypass. Not all scaffolds require multi-tenancy.
- **Service pattern proof:** TaskFlow services use `BuildResponse` helper, `ErrorConstants.ERROR_ITEM_NOTFOUND`, `nameof(Entity)`, `[LoggerMessage]` source-gen logging, and `DefaultRequest<T>`/`DefaultResponse<T>` as `record` types.
- **Dual DbContext proof:** TaskFlow uses `TaskFlowDbContextTrxn` for writes and `TaskFlowDbContextQuery` for read-only/no-tracking access.
- **Repository proof:** TaskFlow splits repository contracts and implementations into transaction and query variants.
- **Middleware proof:** The API pipeline is ordered as security headers → correlation ID → exception handling → rate limiting → auth → gateway claim enrichment → authorization → endpoints.
- **Gateway proof:** The gateway forwards bearer tokens and original claims through an encoded header.
- **Scheduler proof:** TickerQ jobs are registered as explicit scheduled handlers, not hidden inside random hosted services.
- **Scaffold-auth proof:** Local/dev completion does not require live cloud auth; scaffold auth supplies trusted claims until Phase 5e finalizes identity.

---

## When To Load This File

- A skill or template describes a pattern but the concrete shape is still ambiguous.
- You need to verify that the instruction set already has a working example.
- You want a fast pointer into TaskFlow without searching the whole repo.
