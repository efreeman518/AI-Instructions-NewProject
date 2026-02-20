# New Business Project — C#/.NET Scaffolding Instructions

## Purpose

This skill contains comprehensive instructions for AI coding assistants to scaffold, configure, and structure a new **C#/.NET business application** following clean architecture patterns, ready for Azure deployment.

## When to Use

Use this skill when the user asks to:
- Create a new C#/.NET business project or solution
- Scaffold a clean architecture solution with domain entities
- Set up an API, gateway, function app, or background service project
- Configure Entity Framework, multi-tenancy, caching, or authentication
- Add a new domain entity with full vertical slice (entity → config → repo → mapper → service → endpoint)
- Create a cross-platform Uno Platform UI that consumes the Gateway API
- Add a new page/screen with MVUX model, XAML view, and business service
- Deploy or prepare a .NET application for Azure

## Baseline Scaffolding Guidance

Keep generated output cohesive and avoid over-scaffolding by using this baseline:

- Start from a minimal but complete vertical slice that builds and runs locally.
- Promote complexity in stages (testing, functions, UI) after core slices stabilize.
- Keep architecture decisions consistent across hosts (API, Scheduler, Functions, Gateway) via shared Bootstrapper rules.
- Prefer reusable skill/template patterns over ad-hoc project-specific conventions.
- Use [engineer-checklist.md](engineer-checklist.md) as the single compile/run execution checklist; keep troubleshooting decisions lightweight.
- **Solution file format:** Always use the modern `.slnx` (XML-based) solution file — never generate a legacy `.sln`. The `.slnx` is simple XML that can be hand-authored directly. See `sampleapp/src/TaskFlow.slnx` for the reference format and `skills/solution-structure.md` for detailed instructions.

## Scaffolding Modes

### Full Mode (default)
Use for production business applications that need multi-tenancy, gateway, caching, and/or cross-platform UI. Follows all skills in order. For testing, start with `testingProfile: balanced` and expand to `comprehensive` as release gates mature. For Uno UI, start with `unoProfile: starter` and expand to `full` once navigation flows stabilize.

### Lite Mode
Use for **simpler projects** — internal tools, proof-of-concept APIs, microservices, or projects where the full architecture is overkill. Lite mode scaffolds a **minimal clean architecture** with just the essentials:

| Included (Lite) | Excluded (add later if needed) |
|-----------------|-------------------------------|
| Solution Structure (simplified) | Gateway (YARP) |
| Domain Model | Multi-Tenant |
| Data Access (single DbContext) | Caching (FusionCache + Redis) |
| Application Layer | Uno Platform UI |
| Bootstrapper | Background Services / Scheduler |
| API (Minimal APIs) | Function App |
| Testing (unit + endpoint only) | Aspire orchestration |

**Choose Lite Mode when:**
- Single-tenant or no tenant isolation needed
- No user-facing UI (API-only service)
- Direct API access (no reverse proxy / gateway needed)
- Small team or rapid prototyping
- You can graduate to Full Mode later by adding skills incrementally

Set `scaffoldMode: lite` in your domain inputs, or tell the AI: *"Use lite mode."*

---

## Reference Implementation

The `sampleapp/` directory contains a complete 25-project reference solution (**TaskFlow**) that demonstrates every pattern described in the skill files. It is kept for **human reference only** — as a proof that the patterns compile and compose correctly.

> **AI assistants: Do NOT bulk-read sampleapp source files.** The composite patterns from sampleapp have been distilled into [sampleapp-patterns.md](sampleapp-patterns.md). Use that file instead. Only open a specific `sampleapp/src/` file if `sampleapp-patterns.md` is insufficient for a particular situation and you know the exact file path.

**Resolution order for patterns:**
1. `templates/` — tokenized per-entity starters (load first)
2. `sampleapp-patterns.md` — composite/cross-cutting patterns distilled from sampleapp (load when needed)
3. `sampleapp/src/*.cs` — raw source (last resort, specific file only)

---

## How to Use

1. **Start with [domain-inputs.schema.md](domain-inputs.schema.md)** — Gather the required domain-specific inputs from the user (project name, entities, relationships, tenant model, etc.)
2. **Choose a scaffolding mode** — Full (default) or Lite (see above)
3. **Choose scaffolding profiles** — set `testingProfile`, (if functions are enabled) `functionProfile`, and (if Uno UI is enabled) `unoProfile` in [domain-inputs.schema.md](domain-inputs.schema.md)
4. **Follow the skills in order** — Each skill file in `skills/` covers a specific architectural layer or concern. Use them as needed:

### Skill Files (in recommended order)

| Skill | File | When to Use |
|-------|------|-------------|
| Solution Structure | [skills/solution-structure.md](skills/solution-structure.md) | First — sets up projects, references, Directory.Packages.props, global.json |
| Domain Model | [skills/domain-model.md](skills/domain-model.md) | Define entities, value objects, enums, DomainResult pattern |
| Data Access | [skills/data-access.md](skills/data-access.md) | EF DbContext (Query/Trxn split), configurations, migrations, repository updaters |
| Application Layer | [skills/application-layer.md](skills/application-layer.md) | DTOs, static mappers, projectors, services, validation rules |
| Bootstrapper | [skills/bootstrapper.md](skills/bootstrapper.md) | Centralized DI registration, startup tasks, shared across hosts |
| API | [skills/api.md](skills/api.md) | Minimal API endpoints, versioning, auth, error handling, pipeline |
| Gateway | [skills/gateway.md](skills/gateway.md) | YARP reverse proxy, token relay, claims transform, CORS |
| Multi-Tenant | [skills/multi-tenant.md](skills/multi-tenant.md) | Tenant query filters, boundary validation, request context |
| Caching | [skills/caching.md](skills/caching.md) | FusionCache with Redis L2, entity caching, backplane |
| Aspire | [skills/aspire.md](skills/aspire.md) | AppHost orchestration, service discovery, dev tunnels |
| Testing | [skills/testing.md](skills/testing.md) | Profile-driven test scaffolding (`minimal` / `balanced` / `comprehensive`) — 7 test project types: Unit, Integration, Architecture, Playwright E2E, Load (NBomber), Benchmarks (BenchmarkDotNet), Test.Support |
| Background Services | [skills/background-services.md](skills/background-services.md) | TickerQ scheduler — cron jobs, one-off time-based jobs, EF Core persistence, dashboard |
| Function App | [skills/function-app.md](skills/function-app.md) | Azure Functions isolated worker with `starter` or `full` trigger profile |
| **Uno Platform UI** | [skills/uno-ui.md](skills/uno-ui.md) | Cross-platform XAML app — MVUX models, views, business services, Kiota HTTP client, Material theming, navigation |
| Notifications | [skills/notifications.md](skills/notifications.md) | Multi-channel notification providers (Email, Push, SMS), graceful degradation, Aspire integration |
| Configuration | [skills/configuration.md](skills/configuration.md) | appsettings structure, environment overrides, secrets management, Options pattern |
| Identity Management | [skills/identity-management.md](skills/identity-management.md) | Entra ID / Entra External ID setup, app registrations, managed identity |
| Cosmos DB Data Access | [skills/cosmosdb-data.md](skills/cosmosdb-data.md) | Cosmos DB entities (`CosmosDbEntity`), repository pattern, partition key design, LINQ/SQL paged queries — use when `dataStore: cosmosdb` |
| Table Storage | [skills/table-storage.md](skills/table-storage.md) | Azure Table Storage repository, `ITableEntity`, partition/row key design, paged queries — use when `dataStore: table` |
| Blob Storage | [skills/blob-storage.md](skills/blob-storage.md) | Azure Blob Storage repository, upload/download streams, SAS URIs, distributed locks — use when `dataStore: blob` or file attachments needed |
| Messaging | [skills/messaging.md](skills/messaging.md) | Service Bus (queue/topic), Event Grid (pub/sub), Event Hub (stream ingestion) — sender/receiver/processor patterns |
| Key Vault | [skills/keyvault.md](skills/keyvault.md) | Runtime secrets, key management, certificates, field-level encryption via Key Vault crypto utility |
| gRPC | [skills/grpc.md](skills/grpc.md) | gRPC service/client error interceptors, proto contracts — for internal service-to-service communication |
| External API Integration | [skills/external-api.md](skills/external-api.md) | Refit typed HTTP clients + .NET resilience (retry, circuit breaker, timeout) — each external service in its own `Infrastructure.{ServiceName}` project |
| Package Dependencies | [skills/package-dependencies.md](skills/package-dependencies.md) | Private NuGet packages (Package.Infrastructure.*), Directory.Packages.props, version policy |
| CI/CD | [skills/cicd.md](skills/cicd.md) | GitHub Actions workflows, build/test/deploy pipelines, environment promotion |
| **Infrastructure as Code** | [skills/iac.md](skills/iac.md) | Azure Bicep templates — modules for Container Apps/App Service, SQL, Redis, Key Vault, ACR, managed identity, per-environment params, deployment scripts |

### Templates

The `templates/` folder contains starter file templates for common scaffolding tasks. Reference these when generating new files to ensure consistency. Every template has a corresponding implementation in `sampleapp/` — follow the sampleapp pattern for real-world context.

### Backend Templates
- Entity, EF configuration, repository, updater, DTO, mapper, service, endpoint, test (unit, endpoint, E2E, architecture, Test.Support infrastructure)
- App settings (`templates/appsettings-template.md`)
- Domain rules (`templates/domain-rules-template.md`)
- Message handler (`templates/message-handler-template.md`)

### UI Templates (Uno Platform)
- MVUX presentation model (`templates/mvux-model-template.md`)
- XAML page (`templates/xaml-page-template.md`)
- UI business service (`templates/ui-service-template.md`)
- UI client model (`templates/ui-model-template.md`)

## Adding an Entity Vertical Slice (Shortcut)

When the solution already exists and you need to **add a new entity**, skip the full skill chain and generate one complete vertical slice in a single prompt.

Use these canonical references:

- [vertical-slice-checklist.md](vertical-slice-checklist.md) for required files and wiring
- `templates/` for artifact structure
- [ai-build-optimization.md](ai-build-optimization.md) for prompt shape and validation loop

> **Prompt shortcut:** *"Add a new entity `Warehouse` with properties `Name` (string, 100, required) and `Location` (string, 200). Generate full vertical slice artifacts, DI wiring, migration command, and unit + endpoint tests."*

---

## Key Principles

- **Clean Architecture** — Domain has no dependencies; Application depends on Domain; Infrastructure implements interfaces from Application.Contracts
- **Bootstrapper pattern** — All non-host-specific DI goes in a shared Bootstrapper project, reused by API, Functions, Scheduler, and Tests
- **Static mappers over AutoMapper** — Explicit, debuggable, with EF-safe Expression projectors
- **Rich domain model** — Factory Create(), private setters, DomainResult<T> for validation
- **Railway-oriented programming** — DomainResult.Bind()/Map() chains for error propagation
- **Multi-tenant by default** — Query filters, boundary validation, request context from claims
- **Split DbContext** — Separate read (NoTracking, read replica) and write contexts
- **Central Package Management** — Directory.Packages.props for all NuGet versions
- **SQL type defaults** — All strings → `nvarchar(N)` with realistic lengths (rarely `nvarchar(max)`); all decimals → `decimal(10,4)` default precision; all DateTime → `datetime2`. Set globally via `ConfigureDefaultDataTypes` in the base DbContext.
- **Custom NuGet feeds** — User specifies all custom/private NuGet feeds up front in `customNugetFeeds` (domain inputs). These go into `nuget.config` alongside `nuget.org` so the solution can restore and compile.
- **Always update to latest NuGets** — After adding any new package references, update `Directory.Packages.props` to the latest stable versions from `nuget.org` and custom feeds. Run `dotnet restore` to verify.
- **Aspire service package discovery** — When adding infrastructure services, always search for the official `Aspire.Hosting.*` NuGet package first (owner:Aspire tags:integration+hosting). If none exists, try an emulator. `Aspire.Hosting` has ready packages for Azure.Sql, SqlServer, Redis, CosmosDB, Azure.Storage, KeyVault, ServiceBus, and more — each with a README.
- **Stub external services for compilation** — External services requiring configuration (auth providers, third-party APIs) must be stubbed or mocked so the project compiles and runs locally without live credentials. Register stubs conditionally when config values are missing.
- **Cross-platform UI** — Uno Platform with MVUX pattern, XAML views, calls Gateway via Kiota-generated HTTP client
- **UI ↔ Gateway** — The Uno app authenticates to and consumes the YARP Gateway; it never calls the API directly
- **Aspire ↔ IaC consistency** — Bicep templates must mirror Aspire AppHost resources; connection string names, replica counts, and ingress config must match exactly
- **Maturity-first scaffolding** — start from cohesive minimums (`testingProfile: balanced`, `functionProfile: starter`, `unoProfile: starter`) and promote after core vertical slices stabilize
- **Instruction maintenance** — During implementation, capture gaps/improvements in `UPDATE_INSTRUCTIONS.md` for the instruction maintenance agent. Do not modify instruction files directly during scaffolding.
- **Session handoff** — When context is over 50% and at a good stopping point, proactively create/update `HANDOFF.md` so the next session can continue seamlessly.

## Reference Files

| File | Purpose |
|------|---------|
| [placeholder-tokens.md](placeholder-tokens.md) | Canonical glossary of all placeholder tokens (`{Entity}`, `{Project}`, etc.) with casing rules |
| [vertical-slice-checklist.md](vertical-slice-checklist.md) | Complete file checklist when adding a new entity vertical slice |
| [troubleshooting.md](troubleshooting.md) | AI triage policy — classify quickly, one code-fix pass max, then flag |
| [engineer-checklist.md](engineer-checklist.md) | Engineer execution checklist — compile/run verification, host startup, and environment tasks |
| [ai-build-optimization.md](ai-build-optimization.md) | Prompting, iteration, validation patterns, **Fail Fast Protocol**, **Phase Loading Manifest**, **Session Handoff Protocol**, and **Instruction Maintenance (UPDATE_INSTRUCTIONS.md)** |
| [domain-inputs.schema.md](domain-inputs.schema.md) | Schema for user-provided domain inputs (including `customNugetFeeds`) |
| [sampleapp-patterns.md](sampleapp-patterns.md) | **Distilled composite patterns** from sampleapp — use this instead of reading sampleapp source |
| `sampleapp/` | Complete 25-project reference implementation (TaskFlow) — **human reference only, do not bulk-load** |

> **Context window management:** Before starting, read the Phase Loading Manifest and Session Boundary Protocol in [ai-build-optimization.md](ai-build-optimization.md). Only load the files needed for the current phase. Create a `handoff.md` checkpoint after each phase to enable clean session restarts.

## Target Stack

> **Version policy:** Always use the **latest stable release** of each component. Do not pin to specific major/minor versions — the patterns in these instructions are forward-compatible. When scaffolding, use `dotnet --version` to detect the installed SDK and target accordingly. Use `"rollForward": "latestFeature"` in `global.json`.

- **.NET** (latest stable) / **C#** (latest)
- ASP.NET Core Minimal APIs
- Entity Framework Core (latest, matching .NET SDK) — SQL Server / Azure SQL
- .NET Aspire (latest) for orchestration
- Azure (App Service, Container Apps, Functions, Azure SQL, Redis, Key Vault, Entra ID)
- MSTest + CleanMoq for unit testing, Playwright for E2E, NetArchTest for architecture, NBomber for load, BenchmarkDotNet for benchmarks
- FusionCache + Redis for caching
- YARP (latest) for gateway/reverse proxy
- Uno Platform (latest, WinUI / XAML) for cross-platform UI (Web, Android, iOS, macOS, Windows, Linux)
- Uno.Extensions (MVUX, Navigation, Reactive, HTTP/Kiota, Authentication, Configuration)
- Uno.Toolkit + Uno.Material for Material Design theming
- Microsoft Kiota (latest) for typed HTTP client generation from OpenAPI
- CommunityToolkit.Mvvm for messaging
- Azure Bicep (latest) for Infrastructure as Code
- Azure CLI / Azure Developer CLI (azd) for deployment
