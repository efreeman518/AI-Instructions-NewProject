# Get Started

This guide walks you through using these scaffolding instructions to create a new C#/.NET business application with your AI coding assistant (e.g., GitHub Copilot in VS Code).

---

## Prerequisites

- **Git** installed (`git --version`)
- **.NET SDK** (latest stable) installed (`dotnet --version`)
- **Visual Studio Code** with GitHub Copilot (or another AI coding assistant)
- **SQL Server** or **Azure SQL** available for local development
- **Custom NuGet feeds** — if your project uses private/internal packages, have the feed URLs ready. You'll specify them in `customNugetFeeds` so the solution can restore and compile.
- **Uno Platform templates** installed: `dotnet new install Uno.Templates` *(if using Uno UI)*
- **Uno.Check** tool (recommended): `dotnet tool install -g uno.check && uno-check` *(if using Uno UI)*
- **Kiota** CLI for HTTP client generation: `dotnet tool install -g Microsoft.OpenApi.Kiota` *(if using Uno UI)*
- For mobile: Android SDK / Xcode (iOS/macOS) as needed
- Familiarity with C#, Entity Framework Core, XAML, and clean architecture concepts

> **Version policy:** Always install and target the **latest stable** .NET SDK, EF Core, Aspire, and all other components. These instructions are forward-compatible — do not pin to a specific version.

### Recommended MCP Servers

MCP (Model Context Protocol) servers give your AI assistant access to up-to-date documentation, operational tools, and live system interaction beyond its training data. Configure these in your AI client (e.g., VS Code `settings.json` under `mcp.servers`, Claude Desktop `claude_desktop_config.json`, or your MCP client's config).

#### Essential — Configure Before Scaffolding

These two servers cover documentation for the entire tech stack. Without them, the AI falls back to potentially stale training data.

| MCP Server | What it provides | Covers |
|-----------|-------------------|--------|
| **Microsoft Docs** (`mcp-microsoftdocs`) | Official Microsoft/Azure documentation search, code samples, and full-page fetch | .NET, ASP.NET Core, EF Core, Aspire, Azure Functions, Bicep, Entra ID, Container Apps, all Azure services |
| **Context7** (`@upstash/context7-mcp`) | Library-specific documentation from package sources | Uno Platform, YARP, FusionCache, Kiota, NBomber, BenchmarkDotNet, TickerQ, CommunityToolkit, Refit, NetArchTest |

#### Recommended — High-Value for This Stack

Adds operational capabilities that directly support scaffolding phases.

| MCP Server | What it provides | When to use |
|-----------|-------------------|-------------|
| **GitHub** (`@modelcontextprotocol/server-github`) | Repo management, branch/PR creation, issue tracking, Actions workflow status | Source control, CI/CD phases, team collaboration |
| **Azure** (`@azure/mcp`) | Azure resource management, deployment validation, subscription/resource queries | IaC authoring, deployment, resource verification |
| **Playwright** (`@executeautomation/playwright-mcp-server`) | Browser automation — navigate, click, fill, screenshot, extract content | Writing and debugging `Test.PlaywrightUI` E2E tests |
| **Fetch** (`@modelcontextprotocol/server-fetch`) | Fetch any URL and convert to markdown — web pages, OpenAPI specs, package READMEs | Retrieving OpenAPI specs for Kiota client generation, reading NuGet package docs, checking release notes |
| **Sequential Thinking** (`@modelcontextprotocol/server-sequential-thinking`) | Structured multi-step reasoning with revision and branching | Domain discovery conversations, complex architecture decisions, debugging multi-layer issues |

#### Optional — Add Based on Your Workflow

| MCP Server | What it provides | When to consider |
|-----------|-------------------|------------------|
| **Git** (`@modelcontextprotocol/server-git`) | Direct git operations — commits, diffs, log, branch management | When the AI needs to create commits, inspect history, or manage branches directly |
| **Docker** (`@modelcontextprotocol/server-docker`) | Container management — list, start, stop, inspect, logs | Aspire local dev with SQL/Redis containers, debugging container issues |
| **Memory** (`@modelcontextprotocol/server-memory`) | Persistent knowledge graph across sessions — stores entities, relationships, observations | Long-running projects where domain context must survive session boundaries (supplements `HANDOFF.md`) |
| **Brave Search** (`@anthropic/mcp-brave-search`) or **Tavily** (`@tavily/mcp-server`) | Web search for current information | Troubleshooting obscure errors, finding latest package versions, checking breaking changes |
| **Azure DevOps** (community) | ADO work items, repos, pipelines | If using Azure DevOps instead of GitHub |
| **Filesystem** (`@modelcontextprotocol/server-filesystem`) | Broader file operations (move, search, directory trees) | Useful in pure MCP clients without built-in file tools; VS Code already covers this |

> **No separate Aspire or Uno MCP needed** — Microsoft Docs covers Aspire on Microsoft Learn, and Context7 indexes Uno Platform docs. Between those two, both frameworks are fully covered.

> **Tip:** Start with the Essential + Recommended servers. Add Optional servers as specific needs arise (e.g., add Docker MCP when you start hitting container issues with Aspire, add Memory MCP for long multi-session projects).

---

## How These Instructions Work

This repository is a **skill** — a set of structured instructions that guide an AI coding assistant through scaffolding a complete .NET solution. It is not a project template you clone; instead, you provide your domain inputs and the AI generates the code for you, following the patterns defined here.

### What's Included

| Folder / File | Purpose |
|---|---|
| [SKILL.md](SKILL.md) | Entry point — describes the skill, when to use it, and the recommended order of operations |
| [domain-inputs.schema.md](domain-inputs.schema.md) | Schema for the inputs you provide (project name, entities, relationships, infra choices, UI options) |
| [skills/](skills/) | Detailed instructions for each architectural layer (solution structure, domain model, data access, API, gateway, **Uno UI**, etc.) |
| [templates/](templates/) | Starter file templates the AI references when generating entities, services, endpoints, **MVUX models, XAML pages**, and more |
| [vertical-slice-checklist.md](vertical-slice-checklist.md) | Complete generated-file checklist for adding a new entity end-to-end |
| [quick-reference.md](quick-reference.md) | Naming, DI, route, and configuration cheat sheet |
| [ai-build-optimization.md](ai-build-optimization.md) | Prompt and iteration playbook for faster, cleaner AI-assisted scaffolding |
| [engineer-checklist.md](engineer-checklist.md) | Engineer execution checklist — single source for compile/run verification and environment actions |
| [troubleshooting.md](troubleshooting.md) | AI triage rules — one-pass code fixes, then flag for engineer |

### The Workflow

```
You describe your domain  →  AI reads the skill files  →  AI scaffolds the full solution
                                                         →  AI scaffolds the Uno UI (if enabled)
```

---

## Step 1 — Create Your Project Repository

### 1a. Create a new empty repository

Create a new repo on GitHub. Initialize it with a README and `.gitignore` for .NET:

- **GitHub**: Click **New repository** → name it (e.g., `TaskFlow`) → add `.gitignore` template: **VisualStudio** → **Create repository**

### 1b. Clone it locally

```bash
git clone https://github.com/your-org/TaskFlow.git
cd TaskFlow
```

### 1c. Copy the instruction files into your repo

Copy the contents of this instructions repository into a `.instructions/` folder at the root of your new repo. This keeps the scaffolding instructions versioned with your project and available to the AI assistant:

```bash
# From your new repo root — copy the instructions in
mkdir .instructions
# Copy all instruction files into .instructions/
# (SKILL.md, GET-STARTED-human.md, domain-inputs.schema.md, skills/, templates/)
```

Your repo should look like this:

```
TaskFlow/
├── .instructions/          ← Scaffolding instructions (this repo's contents)
│   ├── SKILL.md
│   ├── GET-STARTED-human.md
│   ├── domain-inputs.schema.md
│   ├── skills/
│   │   ├── solution-structure.md
│   │   ├── domain-model.md
│   │   └── ...
│   └── templates/
│       ├── entity-template.md
│       ├── service-template.md
│       └── ...
├── .gitignore
└── README.md
```

> **Alternative**: If you prefer not to embed the instructions, you can open both repos in a VS Code multi-root workspace. But embedding in `.instructions/` is simpler — the AI can always find the files, and you can update them over time.

### 1d. Open in VS Code

```bash
code .
```

---

## Step 2 — Choose a Scaffolding Mode

Before preparing your domain inputs, decide which **scaffolding mode** fits your project:

| | **Full Mode** (default) | **Lite Mode** |
|---|---|---|
| **Use when** | Production business apps, multi-tenant SaaS, apps with UI | Internal tools, microservices, APIs, PoCs |
| **Architecture** | Full clean architecture (12 skills) | Minimal clean architecture (6 skills) |
| **DbContext** | Split read/write | Single DbContext |
| **Gateway** | YARP reverse proxy | Direct API access |
| **Multi-Tenant** | Query filters + boundary validation | Single tenant |
| **Caching** | FusionCache + Redis L2 | None (add later) |
| **UI** | Uno Platform (optional) | None |
| **Testing** | Full pyramid (7 project types) | Unit + endpoint tests |
| **Aspire** | AppHost orchestration | Optional |

Set `scaffoldMode: lite` in your domain inputs or tell the AI. You can always graduate from Lite → Full by adding skills incrementally.

Also set profile inputs early to avoid over-scaffolding:
- `testingProfile: minimal|balanced|comprehensive`
- `functionProfile: starter|full` (if `includeFunctionApp: true`)
- `unoProfile: starter|full` (if `includeUnoUI: true`)

### Baseline defaults for first scaffold

For a cohesive first scaffold that still leaves room to grow:

```yaml
testingProfile: balanced       # Unit + Integration/Endpoint + Test.Support
includeFunctionApp: true       # optional; set false if not needed yet
functionProfile: starter       # promote to full after local host/config is stable
includeUnoUI: true             # optional; set false for API-only backends
unoProfile: starter            # promote to full after core routes stabilize
```

This keeps the initial solution runnable in Aspire without front-loading every optional host and test layer.

---

## Step 3 — Explore Your Domain with the AI

Before writing YAML or preparing structured inputs, **have a conversation with your AI assistant to think through your domain**. This is the highest-value step — a well-modeled domain prevents expensive rework later.

### Why Start with a Conversation?

Most engineers start with a rough idea of their entities but haven't fully worked through relationships, lifecycle states, edge cases, or data store choices. The AI can act as a domain modeling partner — asking probing questions, suggesting patterns, and challenging assumptions.

### How to Start

Open your AI assistant and use a prompt like:

> **Example prompt:**
> "I want to build a new application for managing team tasks and todo items. Let's think through the domain together before we start coding. Here's what I know so far:
> - We need to track TodoItems, Categories, and Teams
> - TodoItems can have Tags, Comments, and Reminders
> - Team members can be assigned to todo items
>
> Help me think through the entities, relationships, business rules, and anything I might be missing."

The AI will guide you through questions about:
- **Business context** — Who uses this? What workflows matter?
- **Entity discovery** — What are the core "things"? What properties do they have? Do they have lifecycle states?
- **Relationships** — One-to-many vs. many-to-many? Cascade deletes? Aggregate boundaries?
- **Business rules** — Invariants, validation, derived data
- **Data store choices** — SQL vs. Cosmos DB vs. Table/Blob Storage for each entity
- **Multi-tenancy & access** — Tenant isolation, roles, shared reference data
- **Integration & events** — External systems, domain events, triggered workflows

### What to Expect

The AI will iteratively summarize the emerging model, propose alternatives, and challenge oversimplifications. Once you've landed on a clear model (typically 3-8 core entities with well-defined relationships), the AI will present a final summary and generate the YAML domain inputs for you.

> **Tip:** You don't need to have all the answers up front. The conversation is designed to help you discover what you don't know yet. The AI will suggest common patterns for your domain.

---

## Step 4 — Prepare Your Domain Inputs

After the domain discovery conversation, capture your inputs in structured form. The AI will often generate these for you based on the conversation. Open [domain-inputs.schema.md](domain-inputs.schema.md) for the full schema.

### Minimal Example

```yaml
ProjectName: TaskFlow
multiTenant: true
customNugetFeeds:
  - name: "CompanyFeed"
    url: "https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json"

entities:
  - name: TodoItem
    isTenantEntity: true
    properties:
      - name: Title
        type: string
        maxLength: 200
        required: true
      - name: Description
        type: string
        maxLength: 2000
        required: false
      - name: Priority
        type: int
        required: true
```

### Fuller Example

```yaml
ProjectName: TaskFlow
multiTenant: true
authProvider: EntraID
database: AzureSQL
caching: FusionCache+Redis
useAspire: true
deployTarget: ContainerApps
includeApi: true
includeGateway: true
testingProfile: balanced
includeFunctionApp: true
functionProfile: starter

# IaC
includeIaC: true
azureRegion: eastus2
includeGitHubActions: true

# UI (Uno Platform)
includeUnoUI: true
unoProfile: starter
uiThemeColor: "#6750A4"
uiPages:
  - Home
  - TodoItemList
  - TodoItemDetail
  - Settings

entities:
  - name: TodoItem
    isTenantEntity: true
    properties:
      - name: Title
        type: string
        maxLength: 200
        required: true
      - name: Description
        type: string
        maxLength: 2000
        required: false
      - name: Priority
        type: int
        required: true
      - name: Status
        type: flags_enum
        values: [None, IsStarted, IsCompleted, IsBlocked, IsCancelled]
    children:
      - name: Tags
        entity: Tag
        relationship: many-to-many
        joinEntity: TodoItemTag
      - name: Comments
        entity: Comment
        relationship: one-to-many
        cascadeDelete: true
    navigation:
      - name: Category
        entity: Category
        required: false
        deleteRestrict: true

  - name: Category
    isTenantEntity: true
    properties:
      - name: Name
        type: string
        maxLength: 100
        required: true

  - name: Tag
    isTenantEntity: false
    properties:
      - name: Name
        type: string
        maxLength: 50
        required: true

  - name: Comment
    isTenantEntity: true
    properties:
      - name: Text
        type: string
        maxLength: 1000
        required: true
      - name: AuthorId
        type: string
        maxLength: 200
        required: true
```

Write your inputs in a YAML block or simply describe them in natural language — the AI will map them to the schema.

---

## Step 5 — Start Scaffolding with Your AI Assistant

Once your domain inputs are finalized (from the discovery conversation or prepared manually), open your AI coding assistant and begin scaffolding:

> **Example prompt:**
> "I want to scaffold a new .NET project using the instructions in `.instructions/`. Here are my domain inputs:
>
> ProjectName: TaskFlow
> multiTenant: true
> entities: TodoItem (Title, Description, Priority), Category (Name)
>
> Please start by creating the solution structure, then the domain model, data access, application layer, and API."

The AI will read `.instructions/SKILL.md`, follow the skills in order, and begin generating files.

---

## Step 6 — Follow the Recommended Order

The full ordered list is maintained in [SKILL.md](SKILL.md) (single source of truth). For day-to-day scaffolding, use this phased sequence:

| Phase | Skills |
|------|--------|
| Foundation | [solution-structure](skills/solution-structure.md) → [domain-model](skills/domain-model.md) → [data-access](skills/data-access.md) |
| App Core | [application-layer](skills/application-layer.md) → [bootstrapper](skills/bootstrapper.md) → [api](skills/api.md) |
| Edge & Runtime | [gateway](skills/gateway.md) *(if enabled)* → [aspire](skills/aspire.md) *(if enabled)* |
| Optional Workloads | [background-services](skills/background-services.md), [function-app](skills/function-app.md), [uno-ui](skills/uno-ui.md), [notifications](skills/notifications.md) |
| Delivery | [testing](skills/testing.md) → [configuration](skills/configuration.md) → [identity-management](skills/identity-management.md) → [iac](skills/iac.md) → [cicd](skills/cicd.md) |

### Pragmatic Startup Path (Recommended)

For the highest success rate with AI-generated output:

1. Generate **Foundation + App Core** first (app/API/services baseline)
2. Keep optional hosts/workloads disabled until the baseline loop is green
3. Add only one additional runtime workload at a time (Gateway, then Aspire, then optional hosts)
4. Use [engineer-checklist.md](engineer-checklist.md) as the single compile/run execution checklist

Build and verify after each phase before moving forward.

---

## Preflight Before Phase Execution

Before running any scaffolding phase, verify prerequisites using the [engineer-checklist.md](engineer-checklist.md). At minimum:

- [ ] `dotnet build` — zero errors
- [ ] `dotnet test --filter "TestCategory=Unit"` — all green
- [ ] Docker running (if Aspire uses containers)

Use [engineer-checklist.md](engineer-checklist.md) as the single source of truth for execution steps.

---

## Step 7 — Use Templates for New Entities

When you need to add a new entity after the initial scaffolding, use the **vertical slice shortcut** — generate all artifacts for the entity in one pass instead of re-running skills sequentially.

### Vertical Slice Shortcut (Add Entity)

For an existing solution, ask the AI to generate the whole slice in one pass (domain → data → application → API → tests → DI wiring → migration).

Use these references instead of re-specifying every file in the prompt:

- [vertical-slice-checklist.md](vertical-slice-checklist.md) — required files + wiring checklist
- [templates/](templates/) — canonical starter templates
- [ai-build-optimization.md](ai-build-optimization.md) — prompt format that reduces retries

> **Example prompt:** *"Add a new entity `Reminder` with properties `Message` (string, 500, optional) and `RemindAt` (DateTimeOffset, required). Tenant-scoped, one-to-many with `TodoItem`. Generate the full vertical slice with unit + endpoint tests, DI registration, and migration command."*

---

## Step 8 — Build and Verify

After each scaffolding phase, run a quick verification loop:

```bash
dotnet build
dotnet test --filter "TestCategory=Unit"
```

If both pass, proceed to the next phase. If either fails, the AI agent will attempt one pass of code-level fixes. Any remaining issues are flagged for you.

**For complete execution procedures** — migrations, Aspire, Scheduler, Functions, Uno UI, IaC, and test commands — use [engineer-checklist.md](engineer-checklist.md). Keep troubleshooting decisions lightweight and checklist-driven.

---

## Tips

- **Work incrementally.** Don't try to generate the entire solution in one prompt. Go layer by layer and verify each step builds.
- **Review generated code.** The AI follows the patterns closely, but always review for correctness — especially EF configurations and relationships.
- **Use natural language.** You don't need perfect YAML. Describe your entities conversationally and the AI will interpret them against the schema.
- **Ask for specific slices.** If you only need a new endpoint or a new service, point the AI at the relevant template and skill file.
- **Keep this repo as a reference.** You can point your AI assistant at these instructions any time you need to extend the project later.
- **AI handles code generation; you handle infrastructure.** The AI will flag build/runtime issues it can't resolve. Use [engineer-checklist.md](engineer-checklist.md) to work through them.
- **IaC mirrors Aspire.** Connection string names, resource names, and replica counts must match between Aspire AppHost and Bicep templates.

---

## Keep Nearby While Scaffolding

- [quick-reference.md](quick-reference.md) for naming, DI snippets, routes, and config keys
- [SKILL.md](SKILL.md) for canonical skill order and architecture principles
- [ai-build-optimization.md](ai-build-optimization.md) for prompt patterns that reduce rework
- [engineer-checklist.md](engineer-checklist.md) for build verification, environment setup, and infrastructure TODO lists
