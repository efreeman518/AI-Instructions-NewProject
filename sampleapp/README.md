# TaskFlow — Reference Solution

> A purpose-built reference implementation demonstrating **all patterns** from the `NewProjectInstructions` skill-based AI coding assistant. Uses a **Todo app domain** with 10 entities across **25 projects** to cover every architectural concern — from domain modelling to deployment.
>
> **⚠ AI Context Loading Rule:** Do NOT bulk-read files from this directory. The composite patterns have been distilled into [`sampleapp-patterns.md`](../sampleapp-patterns.md). Read that file first. Only open specific `.cs` files here as a last resort when `sampleapp-patterns.md` is insufficient for your situation.

---

## Purpose

This is **NOT** a production application. It is a **reference solution** designed to:

1. **Demonstrate patterns** — every skill file and template in `skills/` and `templates/` is represented by working code
2. **Train AI assistants** — provides real code examples the AI can reference when generating new projects
3. **Validate templates** — ensures placeholder-based templates produce correct output when expanded

The solution does **not** need to compile — it serves as a pattern catalog with thorough comments explaining each pattern.

---

## Architecture

Clean Architecture with read/write DbContext split:

```
Domain.Model / Domain.Shared
    ↑
Application.Contracts / Application.Models / Application.Services / Application.MessageHandlers
    ↑
Infrastructure / Infrastructure.Repositories / Infrastructure.Notification
    ↑
TaskFlow.Bootstrapper (shared DI)
    ↑
TaskFlow.Api / TaskFlow.Scheduler / TaskFlow.BackgroundServices / FunctionApp / TaskFlow.Gateway / TaskFlow.UI
```

### Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **ORM** | EF Core 10 | Pooled DbContext factories, read/write split |
| **Caching** | FusionCache (L1 + L2 Redis) | Cache-aside + cache-on-write for static data |
| **Scheduling** | TickerQ | Cron-based, SQL-persisted, compat 170 |
| **API Gateway** | YARP | Token relay, claims forwarding, service discovery |
| **Orchestration** | .NET Aspire | Service discovery, health-based startup, persistent volumes |
| **Auth** | Entra External (CIAM) | User auth at gateway, client credentials downstream |
| **UI** | Uno Platform (MVUX) | Single codebase: Desktop, Mobile, WASM, WinUI |
| **Testing** | MSTest + CleanMoq + Respawn | Unit, integration, architecture, E2E, load, benchmarks |
| **Functions** | Azure Functions v4 (isolated) | Timer, HTTP, Blob, Queue, EventGrid triggers |
| **Containers** | Multi-stage Docker (chiseled) | Minimal attack surface, non-root user |

---

## Domain Model (10 Entities)

| Entity | Type | Key Patterns |
|--------|------|-------------|
| **TodoItem** | Rich tenant entity | Self-referencing hierarchy, `[Flags]` enum, owned `DateRange`, child collections, `DomainResult` |
| **Category** | Simple tenant entity | Cacheable static data, cache-on-write invalidation |
| **Tag** | Non-tenant global | Shared across tenants, no `TenantId` |
| **TodoItemTag** | Junction entity | Composite PK, no `EntityBase` |
| **Comment** | Append-only child | Never updated, only created/deleted |
| **Attachment** | Polymorphic entity | `EntityType` discriminator + `EntityId` |
| **Team** | Parent with children | `TeamMembers` child collection management |
| **TeamMember** | Child of Team | `MemberRole` enum, unique per team |
| **Reminder** | Time-based entity | Scheduler processing, `DueDate` + `IsSent` |
| **TodoItemHistory** | Event-driven read-only | Created by message handlers, audit trail |

### Enums (4)
- `TodoItemStatus` — `[Flags]` with bit-shift values
- `MemberRole` — team member roles
- `EntityType` — polymorphic discriminator
- `ReminderType` — reminder categories

### Value Objects (1)
- `DateRange` — owned type stored as `StartDate`/`EndDate` columns

### Domain Rules (5)
- `RuleBase<T>` — specification pattern base
- `TodoItemStatusTransitionRule` — state machine validation
- `CategoryDeletionRule` — cross-entity check (active items)
- `TeamDeactivationRule` — active member check
- `TodoItemHierarchyRule` — self-reference depth guard

---

## Project Structure

```
sampleapp/
├── support/
│   └── assign-managed-identity-to-app-role.ps1  # DevOps: Entra app role assignment
│
└── src/
    ├── TaskFlow.slnx                    # 25 projects in 7 solution folders
    ├── Directory.Packages.props                # Central package management (~55 packages)
    ├── global.json                             # .NET SDK 10.0.100
    ├── nuget.config                            # Private feed + nuget.org
    │
    ├── Domain/
    │   ├── TaskFlow.Domain.Model/       # Entities, Enums, ValueObjects, Rules
    │   └── TaskFlow.Domain.Shared/      # Constants (Roles, CacheNames, EventNames)
    │
    ├── Application/
    │   ├── TaskFlow.Application.Contracts/  # Interfaces, Mappers, Events
    │   │   └── Constants/                          # AppConstants, ErrorConstants
    │   ├── TaskFlow.Application.Models/     # DTOs, SearchFilters
    │   ├── TaskFlow.Application.Services/   # Service implementations
    │   │   └── Rules/                              # ValidationHelper, StructureValidators, ServiceErrorMessages
    │   └── TaskFlow.Application.MessageHandlers/  # Event handlers
    │
    ├── Infrastructure/
    │   ├── TaskFlow.Infrastructure.Data/         # DbContexts, EF Configurations
    │   │   ├── Migrations/                         # InitialCreate migration (3 files)
    │   │   └── Scripts/                            # Seed/, Maintenance/, CreateUser.sql
    │   ├── TaskFlow.Infrastructure.Repositories/  # Query + Trxn repos, Updaters
    │   └── TaskFlow.Infrastructure.Notification/  # Email/SMS providers
    │
    ├── TaskFlow/
    │   ├── TaskFlow.Bootstrapper/              # Shared DI registration hub
    │   ├── TaskFlow.Api/                       # Minimal API endpoints
    │   │   ├── Auth/                           # GatewayClaimsTransformer, TenantMatchHandler
    │   │   ├── HealthChecks/                   # Database, Redis, AuthOidc, SqlToken
    │   │   └── Dockerfile                      # Multi-stage chiseled image
    │   ├── TaskFlow.Gateway/                   # YARP reverse proxy
    │   │   ├── Auth/                           # GatewayClaimsPayload, TenantMatchHandler
    │   │   ├── StartupTasks/                   # WarmupDependencies (YARP cluster pre-warm)
    │   │   └── Dockerfile                      # Minimal gateway image
    │   ├── TaskFlow.Scheduler/                 # TickerQ cron scheduler
    │   │   └── Dockerfile                      # Full runtime image
    │   ├── TaskFlow.BackgroundServices/        # Channel-based background queue
    │   └── TaskFlow.UI/                        # Uno Platform (MVUX) cross-platform UI
    │       ├── Business/Models/                # Client-side models
    │       ├── Business/Services/              # API client services
    │       ├── Client/                         # TaskFlowApiClient (HttpClient wrapper)
    │       ├── Converters/                     # XAML value converters
    │       ├── Infrastructure/                 # MockHttpMessageHandler (design-time)
    │       ├── Presentation/                   # MVUX partial-record models (IFeed, IState)
    │       ├── Styles/                         # ColorPaletteOverride, FeedView
    │       └── Views/                          # XAML pages (Shell + 6 pages)
    │
    ├── Functions/
    │   └── TaskFlow.FunctionApp/        # Azure Functions v4 (isolated)
    │
    ├── Aspire/
    │   ├── AppHost/                            # Aspire orchestration
    │   └── ServiceDefaults/                    # OpenTelemetry, health, resilience
    │
    └── Test/
        ├── Test.Support/                       # Shared test infrastructure
        ├── Test.Unit/                          # Unit tests (MSTest + CleanMoq)
        ├── Test.Integration/                   # Endpoint integration tests
        ├── Test.Architecture/                  # Dependency & convention rules (NetArchTest)
        ├── Test.PlaywrightUI/                  # E2E browser tests (Playwright + page objects)
        ├── Test.Load/                          # Load/stress testing (NBomber scenarios)
        └── Test.Benchmarks/                    # Micro-benchmarks (BenchmarkDotNet)
```

---

## Framework & Package Versions

| Package | Version |
|---------|---------|
| .NET SDK | 10.0.100 |
| EF Core | 10.0.2 |
| Aspire | 13.1.0 |
| YARP | 2.3.0 |
| TickerQ | 10.0.2 |
| FusionCache | 2.5.0 |
| Scalar | 2.12.35 |
| MSTest | 4.1.0 |
| Polly | 8.6.5 |
| Identity.Web | 4.3.0 |
| Functions Worker | 2.51.0 |
| Uno.Sdk | (implicit via Uno.Sdk MSBuild SDK) |
| Playwright | 1.49.0 |
| BenchmarkDotNet | 0.14.0 |
| NBomber | 6.3.0 |
| NetArchTest.Rules | 1.4.0 |

All versions centrally managed in `Directory.Packages.props`.

---

## Patterns Demonstrated

### Per Skill File Coverage

| Skill | Files | Key Patterns |
|-------|-------|-------------|
| `solution-structure.md` | slnx, csproj, global.json | CPM, solution folders, naming conventions |
| `domain-model.md` | 10 entities, 5 rules | DomainResult, factory methods, value objects |
| `data-access.md` | 2 DbContexts, 18 repos, 4 updaters, migrations | Read/write split, pooled factories, projectors, InitialCreate migration |
| `application-layer.md` | 6 services, 10 mappers, 3 handlers | Service pattern, event-driven, validation rules |
| `caching.md` | FusionCache L1+L2 | Cache-aside, cache-on-write, named caches |
| `bootstrapper.md` | RegisterServices, startup tasks | Centralized DI, layered registration, per-service settings |
| `api.md` | 4 endpoint classes, Program.cs | Minimal API, Result.Match, versioning |
| `background-services.md` | TickerQ jobs, channel queue | BaseTickerQJob, BoundedChannel |
| `function-app.md` | 7 trigger types | Isolated worker, binding expressions |
| `gateway.md` | YARP proxy, token relay, warmup | Claims forwarding, service-to-service auth, WarmupDependencies |
| `aspire.md` | AppHost, ServiceDefaults | WaitFor, persistent volumes, OpenTelemetry |
| `notifications.md` | Email/SMS providers | Graceful degradation, provider abstraction |
| `configuration.md` | 6 appsettings files, Dockerfiles | Layered hierarchy, feature flags, multi-stage Docker builds |
| `testing.md` | 7 test projects, 20+ test files | Unit, integration, architecture, E2E (Playwright), load (NBomber), benchmarks |
| `identity-management.md` | GatewayClaimsTransformer, health checks | Entra ID, claims pipeline, OIDC/SQL-token health probes |
| `multi-tenant.md` | IRequestContext, query filters, validators | Global query filters, TenantBoundaryValidator, cross-tenant guard |
| `uno-ui.md` | 44 files (MVUX models, XAML, services) | Partial records, IFeed/IState, Kiota HTTP client, XAML pages |
| `package-dependencies.md` | Directory.Packages.props | ~55 packages, CPM, private feed |
| `cicd.md` | Dockerfiles, DB scripts | Multi-stage chiseled builds, seed/maintenance SQL |
| `iac.md` | support/ scripts | Managed identity app-role assignment (PowerShell) |

---

## How to Use This Reference

1. **When generating a new project**: Read the relevant `skills/*.md` file for the pattern, then look at the corresponding `sampleapp/src/` file for a concrete implementation example.

2. **When expanding templates**: The `templates/*.md` files contain `{App}`, `{Entity}`, etc. placeholders. Compare the template to the sample output to verify correct expansion.

3. **When reviewing patterns**: Every file has a top-level comment block explaining which patterns it demonstrates. Look for `// Pattern:` inline comments for specific callouts.

---

## Quick Reference

- **Skill files**: `skills/` (~20 markdown files)
- **Templates**: `templates/` (~16 markdown files)
- **Domain definition schema**: `domain-definition-schema.md`
- **Placeholder tokens**: `placeholder-tokens.md`
- **Vertical slice checklist**: `vertical-slice-checklist.md`
- **Troubleshooting guide**: `troubleshooting.md`
