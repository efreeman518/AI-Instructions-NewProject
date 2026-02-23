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

> **⛔ CRITICAL — SAMPLEAPP IS READ-ONLY REFERENCE CODE**
>
> **Do NOT create, modify, delete, or build any file under `sampleapp/`.** The sampleapp exists solely as a reference implementation. You are building a **new application** in the user's project directory based on the instructions, skills, templates, and sampleapp patterns provided — together with the domain inputs for the new app.
>
> Violations include:
> - Editing any `.cs`, `.csproj`, `.json`, or other file under `sampleapp/`
> - Running `dotnet build`, `dotnet restore`, or `dotnet test` targeting `sampleapp/`
> - Adding new files or projects under `sampleapp/`
> - Attempting to "fix" compile errors in sampleapp code
>
> If you catch yourself about to modify a sampleapp file, **STOP** — you are on the wrong path. Re-read the domain inputs and create the corresponding file in the **new project's** directory instead.

> **AI assistants: Do NOT bulk-read sampleapp source files.** The composite patterns from sampleapp have been distilled into [sampleapp-patterns.md](sampleapp-patterns.md). Use that file instead. Only open a specific `sampleapp/src/` file if `sampleapp-patterns.md` is insufficient for a particular situation and you know the exact file path.

**Resolution order for patterns:**
1. `templates/` — tokenized per-entity starters (load first)
2. `sampleapp-patterns.md` — composite/cross-cutting patterns distilled from sampleapp (load when needed)
3. `sampleapp/src/*.cs` — raw source (last resort, specific file only, **read-only**)

---

## How to Use

1. **Domain Discovery Conversation (start here)** — Before collecting structured inputs or generating any code, have a collaborative conversation with the engineer to explore and refine the domain. The goal is to surface hidden complexity, clarify business rules, and arrive at a well-thought-out entity model before scaffolding begins. See the **Domain Discovery Protocol** below.
2. **Capture inputs in [domain-inputs.schema.md](domain-inputs.schema.md)** — After the domain conversation, translate the agreed-upon design into structured domain inputs (project name, entities, relationships, tenant model, etc.)
3. **Choose a scaffolding mode** — Full (default) or Lite (see above)
4. **Choose scaffolding profiles** — set `testingProfile`, (if functions are enabled) `functionProfile`, and (if Uno UI is enabled) `unoProfile` in [domain-inputs.schema.md](domain-inputs.schema.md)
5. **Follow the skills in order** — Each skill file in `skills/` covers a specific architectural layer or concern. Use them as needed:

---

## Domain Discovery Protocol

**Before writing any code or collecting YAML inputs, the AI must lead an exploratory conversation with the engineer to deeply understand the business domain.** This is the most valuable phase of the project — a well-modeled domain prevents expensive rework later.

### Why This Matters

Engineers often start with a rough idea of their entities but haven't fully considered edge cases, relationships, lifecycle states, or future growth. The AI should act as a **domain modeling partner** — asking probing questions, suggesting patterns, challenging assumptions, and proposing alternatives the engineer may not have considered.

### Conversation Flow

The AI should guide the discussion through these areas (adapt to the project):

#### 1. Business Context & Goals
- What problem does this application solve? Who are the users?
- What are the core workflows or business processes?
- Is this replacing an existing system, or greenfield?
- What scale and growth trajectory is expected?

#### 2. Entity Discovery & Refinement
- What are the main "things" (nouns) in the domain? Start with what the engineer knows, then probe deeper:
  - *"You mentioned `Order` — does an order go through lifecycle states? What are they?"*
  - *"Is a `Customer` always a single person, or can it be an organization?"*
  - *"You have `Product` — do products have variants (sizes, colors)? Are they configurable?"*
- For each entity, explore:
  - **Identity** — What uniquely identifies it? Is there a natural key beyond the database ID?
  - **Lifecycle** — Does it have states/statuses? What transitions are valid?
  - **Ownership** — Is it tenant-scoped? User-scoped? Global?
  - **Cardinality** — How many of these will exist? Dozens, thousands, millions?

#### 3. Relationships & Boundaries
- How do entities relate? Challenge simple assumptions:
  - *"Is this truly one-to-many, or could it become many-to-many later?"*
  - *"Should `Address` be its own entity or embedded within `Customer`?"*
  - *"When you delete a `Category`, what happens to its `Products`?"*
- Where are the aggregate boundaries? Which entities are always loaded/saved together?
- Are there cross-aggregate references that might indicate a bounded context boundary?

#### 4. Business Rules & Validation
- What are the invariants? (e.g., "An order must have at least one line item", "Price cannot be negative")
- Are there rules that span multiple entities? (e.g., "A customer can't have more than 5 active orders")
- What calculations or derived data exist? (e.g., order totals, inventory counts)

#### 5. Data Store Considerations
- Does any entity have a variable/evolving schema? → Consider Cosmos DB
- Are there large binary payloads (files, images)? → Consider Blob Storage
- Is there high-volume append-only data (logs, telemetry)? → Consider Table Storage or event streaming
- Does any entity need semantic search or similarity matching? → Consider vector indexing (Azure SQL, Cosmos DB, or Azure AI Search)
- Reference the **Data Store Selection Guide** in [domain-inputs.schema.md](domain-inputs.schema.md)

#### 6. Multi-Tenancy & Access
- Is the app multi-tenant? How is tenant isolation enforced?
- What roles exist? What can each role do?
- Are there entities that are shared across tenants (reference data)?

#### 7. Integration & Events
- What external systems does this integrate with?
- What domain events matter? (e.g., "OrderPlaced", "InventoryLow")
- Are there workflows triggered by state changes?

#### 8. AI Services & Intelligent Capabilities
- **Semantic search / vector indexing** — Does any entity need full-text semantic search beyond simple WHERE filters? Azure SQL, Cosmos DB, and Azure AI Search all support vector indexing for semantic search. If users will search with natural language queries (e.g., "find products similar to…", "search knowledge base"), consider adding vector embeddings to the relevant entities or provisioning an Azure AI Search index.
  - *"Will users search this data with natural language or need similarity matching?"*
  - *"Could a vector index improve the search experience for this entity?"*
  - *"Is the search volume high enough to justify a dedicated Azure AI Search resource, or can in-database vector search (Azure SQL / Cosmos DB) suffice?"*
- **AI agent / multi-agent workflows** — Are there scenarios where an AI agent (or orchestrated team of agents) could add value? Consider **Microsoft Foundry** for managed model endpoints and the **Microsoft Agent Framework** (successor to Semantic Kernel + AutoGen) for in-code agent orchestration.
  - *"Are there decision-making or classification tasks that could be delegated to an AI model?"*
  - *"Would a multi-agent workflow improve complex processes (e.g., intake triage, document analysis, recommendation engine)?"*
  - *"Do you need a conversational agent or copilot experience embedded in the app?"*
- Reference the **AI Services** section in [domain-inputs.schema.md](domain-inputs.schema.md) for structured inputs.

### AI Behavior During Discovery

- **Ask open-ended questions first**, then narrow down with specific options.
- **Propose entity models visually** — summarize the emerging model in a compact table or list after exploring each area so the engineer can react to it.
- **Challenge and suggest** — Don't just record what the engineer says. Offer alternatives:
  - *"Instead of a boolean `IsActive`, consider a `Status` flags enum so you can represent multiple states."*
  - *"This looks like it might benefit from a many-to-many with a join entity that carries its own data."*
  - *"Have you considered making `Address` a value object instead of a separate entity?"*
- **Summarize iteratively** — After each topic area, recap the current model and confirm before moving on.
- **Know when to stop** — Domain discovery doesn't need to be exhaustive. Once the core entities (3-8) are well-defined with clear relationships and business rules, propose moving to structured inputs. Edge entities can be added later as vertical slices.

### Transition to Scaffolding

When the conversation has produced a clear domain model, the AI should:

1. **Present a final domain summary** — entities, key properties, relationships, data stores, business rules
2. **Ask for confirmation** — *"Does this capture your domain accurately? Anything to add or change?"*
3. **Generate the YAML domain inputs** — Translate the agreed model into the [domain-inputs.schema.md](domain-inputs.schema.md) format
4. **Proceed to scaffolding** — Follow the skill order below

> **Prompt to trigger domain discovery:**  *"I want to build a new application for [brief description]. Let's think through the domain together before we start coding."*

---

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
- **Instruction maintenance** — During implementation, capture gaps/improvements in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md) for the instruction maintenance agent. Do not modify instruction files directly during scaffolding.
- **Sampleapp is read-only** — Never create, modify, delete, or build any file under `sampleapp/`. It is reference code only. Always generate new code in the **user's project directory**, not in sampleapp.
- **Session handoff** — When context is over 50% and at a good stopping point, proactively create/update `HANDOFF.md` so the next session can continue seamlessly.

## Reference Files

| File | Purpose |
|------|---------|
| [placeholder-tokens.md](placeholder-tokens.md) | Canonical glossary of all placeholder tokens (`{Entity}`, `{Project}`, etc.) with casing rules |
| [vertical-slice-checklist.md](vertical-slice-checklist.md) | Complete file checklist when adding a new entity vertical slice |
| [troubleshooting.md](troubleshooting.md) | AI triage policy — classify quickly, one code-fix pass max, then flag |
| [engineer-checklist.md](engineer-checklist.md) | Engineer execution checklist — compile/run verification, host startup, and environment tasks |
| [ai-build-optimization.md](ai-build-optimization.md) | Prompting, iteration, validation patterns, **Fail Fast Protocol**, **Phase Loading Manifest**, **Session Handoff Protocol**, and **Instruction Maintenance ([UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md))** |
| [domain-inputs.schema.md](domain-inputs.schema.md) | Schema for user-provided domain inputs (including `customNugetFeeds`) |
| [sampleapp-patterns.md](sampleapp-patterns.md) | **Distilled composite patterns** from sampleapp — use this instead of reading sampleapp source |
| `sampleapp/` | Complete 25-project reference implementation (TaskFlow) — **READ-ONLY reference, never modify or build; do not bulk-load** |

> **Context window management:** Before starting, read the Phase Loading Manifest and Session Boundary Protocol in [ai-build-optimization.md](ai-build-optimization.md). Only load the files needed for the current phase. Create a `handoff.md` checkpoint after each phase to enable clean session restarts.

## Target Stack

> **Version policy:** Always use the **latest stable release** of each component. Do not pin to specific major/minor versions — the patterns in these instructions are forward-compatible. When scaffolding, use `dotnet --version` to detect the installed SDK and target accordingly. Use `"rollForward": "latestFeature"` in `global.json`.

- **.NET** (latest stable) / **C#** (latest)
- ASP.NET Core Minimal APIs
- Entity Framework Core (latest, matching .NET SDK) — SQL Server / Azure SQL
- .NET Aspire (latest) for orchestration
- Azure (App Service, Container Apps, Functions, Azure SQL, Redis, Key Vault, Entra ID)
- Microsoft Foundry for managed AI model endpoints, Foundry Agent Service, and prompt management
- Microsoft Agent Framework (latest) for in-code agent orchestration, multi-agent handoff, and tool-calling patterns
- Azure AI Search for vector indexing and hybrid semantic search
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

## Recommended MCP Servers

MCP (Model Context Protocol) servers give the AI assistant access to current documentation and operational tools. Configure these in the engineer's AI client before starting scaffolding. The servers below are organized by value tier and mapped to project phases.

### Essential (configure before scaffolding)

| Server | Purpose | Covers |
|--------|---------|--------|
| **Microsoft Docs** (`mcp-microsoftdocs`) | Official Microsoft/Azure documentation search, code samples, full-page fetch | .NET, ASP.NET Core, EF Core, Aspire, Azure Functions, Bicep, Entra ID, Container Apps, all Azure services |
| **Context7** (`@upstash/context7-mcp`) | Third-party and community library documentation | Uno Platform, YARP, FusionCache, Kiota, NBomber, BenchmarkDotNet, TickerQ, CommunityToolkit, Refit, NetArchTest |

### Recommended (high-value for this stack)

| Server | Purpose | When to add |
|--------|---------|-------------|
| **GitHub** (`@modelcontextprotocol/server-github`) | Repo management, PRs, issue tracking, Actions workflow status | CI/CD phases, team collaboration |
| **Azure** (`@azure/mcp`) | Azure resource management, deployment validation, subscription queries — also covers **Microsoft Foundry** (models, agents, threads, evaluations) and **Azure AI Search** (index management, search queries) | IaC authoring, deployment phases, AI service scaffolding |
| **Uno Platform Remote MCP** (`https://mcp.platform.uno/v1`) | Uno Platform documentation search/fetch (`uno_platform_docs_search`, `uno_platform_docs_fetch`), agent priming prompts (`/new`, `/init`), usage rules (`uno_platform_agent_rules_init`, `uno_platform_usage_rules_init`) | Uno UI scaffolding phase — provides up-to-date docs, MVUX patterns, and best-practice priming directly from the Uno Platform team. Available via the **Uno Platform VS Code extension** (`unoplatform.vscode`) or manually via `mcp.json`. Requires an Uno Platform account sign-in. |
| **Uno Platform App MCP** (local, via Uno Platform extension) | Live app interaction — screenshots (`uno_app_get_screenshot`), visual tree inspection (`uno_app_visualtree_snapshot`), pointer clicks, key presses, text input, DataContext inspection | UI development and testing — gives the AI "eyes and hands" to validate generated UI against a running app. Requires a running Uno app and the `dnx` command (.NET 10+) or `uno-devserver` tool (.NET 9). Community license includes core tools; Pro license adds automation peer actions and DataContext inspection. |
| **Playwright** (`@executeautomation/playwright-mcp-server`) | Browser automation for E2E test authoring and debugging | When scaffolding `Test.PlaywrightUI` |
| **Fetch** (`@modelcontextprotocol/server-fetch`) | Fetch any URL as markdown — web pages, OpenAPI specs, NuGet READMEs | Kiota client generation (fetching OpenAPI specs), reading package docs, checking release notes |
| **Sequential Thinking** (`@modelcontextprotocol/server-sequential-thinking`) | Structured multi-step reasoning with revision and branching | Domain discovery conversations, complex architecture decisions, debugging multi-layer issues |

### Optional (add based on workflow needs)

| Server | Purpose | When to consider |
|--------|---------|------------------|
| **Git** (`@modelcontextprotocol/server-git`) | Direct git operations — commits, diffs, log, branches | When AI needs to create commits or inspect history directly |
| **Docker** (`@modelcontextprotocol/server-docker`) | Container management — list, start, stop, inspect, logs | Aspire local dev with SQL/Redis containers, debugging container issues |
| **Memory** (`@modelcontextprotocol/server-memory`) | Persistent knowledge graph across sessions | Long-running projects where domain context must survive session boundaries (supplements `HANDOFF.md`) |
| **Brave Search** (`@anthropic/mcp-brave-search`) or **Tavily** (`@tavily/mcp-server`) | Web search for current information | Troubleshooting obscure errors, finding latest package versions, checking breaking changes |
| **Azure DevOps** (community) | ADO work items, repos, pipelines | If using Azure DevOps instead of GitHub |
| **Filesystem** (`@modelcontextprotocol/server-filesystem`) | Broader file operations (move, search, trees) | Pure MCP clients without built-in file tools (VS Code already covers this) |

### Phase-to-MCP Mapping

| Project Phase | Servers active |
|--------------|----------------|
| Domain Discovery | Microsoft Docs, Context7, Sequential Thinking, **Azure MCP** (for Foundry model/agent exploration) |
| Foundation & App Core | Microsoft Docs, Context7, Fetch (for package docs), **Azure MCP** (if AI services enabled) |
| Edge & Runtime (Gateway, Aspire) | Microsoft Docs, Context7, Docker (if containers) |
| Optional Workloads (Functions, Uno UI) | Microsoft Docs, Context7, Fetch (OpenAPI specs for Kiota), **Uno Platform Remote MCP** + **Uno Platform App MCP** (for Uno UI) |
| Testing & E2E | Playwright, Context7 (NBomber, BenchmarkDotNet) |
| CI/CD & Deployment | GitHub, Azure, Brave Search/Tavily (troubleshooting) |
| IaC (Bicep) | Microsoft Docs, Azure |
| Cross-session continuity | Memory, Git |

> **No dedicated Aspire MCP server is needed.** Microsoft Docs covers all Aspire documentation on Microsoft Learn. For **Uno Platform**, use the two official MCPs provided by the Uno Platform VS Code extension: the **Remote MCP** for documentation search/fetch and best-practice priming, and the **App MCP** for live app interaction (screenshots, visual tree, clicks). Both require the Uno Platform extension (`unoplatform.vscode`) and an Uno Platform account sign-in. See [Uno Platform MCP docs](https://platform.uno/docs/articles/features/using-the-uno-mcps.html) for setup details.

> **AI assistants:** When scaffolding Uno UI, use the `/init` prompt from the Uno Platform MCP to prime the session with Uno best practices before generating code. Use `uno_platform_docs_search` and `uno_platform_docs_fetch` for current API patterns. During UI validation, use `uno_app_get_screenshot` and `uno_app_visualtree_snapshot` from the App MCP to verify generated UI against a running app.

> **AI assistants:** When you need current API signatures, patterns, or configuration details for any library in the target stack — **use MCP documentation lookups instead of relying on training data**. Use Microsoft Docs for .NET/Azure APIs and Context7 for third-party libraries. Use Sequential Thinking for complex domain modeling and architecture decisions. Use Fetch to retrieve OpenAPI specs and package documentation directly.

### Dynamic MCP Discovery Protocol

The MCP ecosystem evolves rapidly. **Before starting each scaffolding phase**, the AI agent should perform a real-time search for new or updated MCP servers that may be relevant to the upcoming work. This ensures the engineer benefits from the latest tooling without relying solely on the static list above.

#### When to Search

- **Before each phase transition** (Foundation → App Core → Edge → Optional Workloads → Testing → IaC/CI)
- **When encountering a new library or service** not listed in the phase mapping above
- **When a known MCP is failing or returning stale results** — check for updates or alternatives

#### How to Search

1. **Use web search or Fetch** to query for `"{library or service name} MCP server"` (e.g., `"Aspire MCP server"`, `"FusionCache MCP"`, `"TickerQ MCP"`)
2. Check [modelcontextprotocol.io](https://modelcontextprotocol.io) for the official MCP server registry
3. Check npm (`npmjs.com/search?q=mcp+{name}`) and GitHub (`github.com/search?q={name}+mcp+server`) for community servers
4. Validate that any discovered MCP is actively maintained (recent commits, non-archived repo)

#### What to Do with Findings

- **If a useful new MCP is found:** Suggest it to the engineer for installation, and append a finding to [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md) so the maintenance agent can add it to the static list above.
- **If an existing MCP has been deprecated or replaced:** Append a finding to UPDATE-INSTRUCTIONS.md with the replacement details.
- **Keep suggestions brief** — one-line recommendation with package name, purpose, and install command.
- **Never block on MCP discovery** — if search takes too long or yields nothing, proceed with the known servers and note the gap.
