# AI Instructions - New .NET App/Service Scaffolding

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET applications and services.

## Purpose

This instruction set turns an AI coding assistant into a guided scaffolding engine for production-grade C#/.NET solutions. Instead of generating throwaway boilerplate, it drives a structured five-phase process - from domain discovery through implementation - producing consistent, buildable, testable code that follows clean architecture and the conventions of a mature engineering team.

The goal is not to replace engineering judgment but to compress the multi day/week "green-field to first vertical slice" timeline down to hours, with guardrails that prevent common shortcuts (missing tests, leaky abstractions, inconsistent naming).

Phase 1 also creates durable collaboration artifacts before code planning starts:

- `domain-specification.yaml` captures entities, relationships, events, workflows, and business rules in pure business language - the source of truth every later phase consumes.
- `UBIQUITOUS-LANGUAGE.md` records accepted domain terms, rejected synonyms, states, commands/actions, roles, events, policies, and naming guidance so later AI sessions use consistent expressions.
- `DESIGN-DECISIONS.md` records design choices and dependencies between decisions, so downstream resource and code choices do not silently contradict earlier domain answers.

## Phases

Each phase runs in its own AI session and produces artifacts the next phase consumes.

| Phase | Purpose | Output |
|---|---|---|
| **1 - Domain Discovery** | Structured interview to reach shared understanding, define ubiquitous language, resolve decision dependencies, and capture entities, relationships, events, workflows, and business rules in pure business language - no implementation details. | `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, `DESIGN-DECISIONS.md` |
| **2 - Resource Definition** | Map each resource requirement to concrete technology choices - data stores, messaging, AI capabilities, hosting models. | `resource-implementation.yaml` |
| **3 - Implementation Planning** | Resolve open questions, verify tooling (NuGet feeds, CLIs), discover project-specific CLIs and MCP servers, and produce a sequenced build plan. | `implementation-plan.md` |
| **4 - Contract Scaffolding** | Generate solution structure, interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Gate: `dotnet build` succeeds on the full solution. | Compilable skeleton |
| **5 - Implementation (TDD)** | Build vertical slices entity-by-entity across sub-phases 5a–5e (Foundation, App Core + Runtime, Optional Hosts, Quality + Delivery, Integration). Phase 5a uses test-driven development (write tests first → red → implement → green); 5b is mixed (TDD for app/API, tests-after for runtime); 5c–5e are tests-after. | Production code + passing tests |

Phase details: [START-AI.md](START-AI.md) § Phase Router.

## Approach

The instruction set is designed around three core ideas:

**1. Phased workflow with TDD.**
Five phases prevent hallucinated architecture by ensuring verified context before code is written. Phase 4 produces a compilable skeleton so that Phase 5a/5b can follow a strict red/green TDD cycle - tests are written against contracts before any implementation exists. See [ai/tdd-protocol.md](ai/tdd-protocol.md).

**2. Skills and templates as composable units.**
Implementation knowledge is split into ~27 skill files (how things work) and ~25 template files (what to generate). The Phase Router in `START-AI.md` and the Phase 5 file table in `ai/SKILL.md` tell the agent which files to load for the current phase or sub-phase.

**3. Composition patterns, not documentation alone.**
Pattern files in `patterns/` document how generated components wire together across projects - database context pooling, API startup sequences, request context resolution, cache configuration, and Aspire resource wiring. An index (`support/pattern-dispatcher.md`) maps each pattern file to its relevant phase. This grounds the generated output in proven, real-world patterns rather than abstract descriptions.

## High-Value Features

| Feature | What It Does |
|---|---|
| **Domain discovery conversation** | Phase 1 drives a structured interview to define shared language, decision dependencies, entities, relationships, events, workflows, and business rules in pure business language - no implementation details. The resulting [domain specification](ai/domain-specification-schema.md), `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md` become the source of truth that every later phase consumes, preventing scope drift and naming drift. |
| **Resource & technology mapping** | Phase 2 is a deliberate conversation about *how* to implement each domain concept - which data store fits each entity (SQL, Cosmos DB, Table Storage), where messaging is needed (Service Bus, Event Grid), whether AI capabilities apply (search, agents, document intelligence), and which hosting model suits each workload. The output is a [resource implementation](ai/resource-implementation-schema.md) YAML that locks in technology choices before any code is written. |
| **Phase-scoped file loading** | The Phase Router in `START-AI.md` and the Phase 5 file table in `ai/SKILL.md` tell the agent which files to load for the current phase. |
| **Three scaffolding modes** | `full` (production w/ gateway, scheduler, UI, IaC), `lite` (clean architecture without infra), `api-only` (single API host). Set once in resource YAML; the Phase Router and Phase 5 file table cover what to skip. |
| **Vertical-slice scaffolding** | Each entity is built end-to-end: domain model → EF config → repository → DTO/mapper → service → endpoint → tests. A [checklist](support/vertical-slice-checklist.md) and [execution gates](support/execution-gates.md) enforce completeness. |
| **Built-in quality gates** | `dotnet build` + targeted tests after every sub-phase. Architecture tests enforce layer dependencies. Structure validators catch DTO issues before runtime. |
| **Golden-path sample** | [support/golden-path-sample.md](support/golden-path-sample.md) provides a small WorkBoard scaffold scenario for regression-checking instruction changes. |
| **Safe session handoff** | A `HANDOFF.md` template lets the AI resume cleanly across sessions without losing progress or re-reading the full instruction set. |
| **Result pattern error flow** | Domain → Service → Endpoint error mapping is fully documented with a type mapping table, anti-patterns, and end-to-end trace example. No exceptions for business logic. |
| **Test data builders** | Fluent builder patterns for entities and DTOs ensure tests construct valid objects by default and override only the property under test. |
| **Composition pattern catalog** | Pattern files in `patterns/` document cross-project wiring - database pooling, API startup sequence, request context, cache configuration, Aspire resource wiring - with inline code snippets. An index (`support/pattern-dispatcher.md`) maps each to its relevant phase. |
| **Prompt catalog** | Copy-paste prompts for each phase live in [support/prompt-catalog.md](support/prompt-catalog.md), keeping `README.md` focused on human onboarding while [START-AI.md](START-AI.md) stays canonical for execution. |
| **Event boundary enforcement** | Cross-process events are modeled as integration contracts in `Application.Contracts.Events` and published via `IIntegrationEventPublisher`; Domain events remain aggregate-local. This avoids layered leakage and naming drift. |

## Reference Application

A companion reference app - **TaskFlow** - demonstrates every pattern and convention these instructions produce:

**Repository:** <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

TaskFlow is a fully scaffolded task-management application built by following this instruction set end-to-end. It covers dual DbContext pooling, YARP gateway with claims transformation, Aspire orchestration, FusionCache with Redis backplane, TickerQ scheduling, Azure Functions, multitenancy, scaffold-mode auth, and Blazor & Uno WASM UIs.

Use it for:

- **Pattern lookups** - when an instruction or template describes a pattern (e.g., middleware ordering, repository split, cache key format), the reference app contains the working implementation.
- **Wiring verification** - cross-project DI registration, startup sequences, and Aspire resource definitions are all present and buildable.
- **Test structure** - unit, integration, architecture, and endpoint test projects are scaffolded with builder patterns.

For a phase-by-phase pointer map into the reference app, use [support/taskflow-proof-map.md](support/taskflow-proof-map.md).

The AI assistant can access the repo via GitHub MCP or by cloning it locally. When stuck on how a pattern should look in practice, consult the reference app before inventing a new approach. The reference app is always available as a live codebase the AI can search, read, and cross-reference during any phase.

## Quick Start

If you want the shortest path from zero context to first scaffold:

1. Clone this repo.
2. Create a new app repo.
3. Run `python scripts/install-to-project.py --target /path/to/your-app-repo` from the clone.
4. Start through the harness table below: `AGENTS.md`, Copilot agent, Claude command, or a prompt that loads `.instructions/START-AI.md`.

Read the rest of this guide when you need setup details, MCP recommendations, or troubleshooting rules.

## Prerequisites

- `git`
- Python 3.11+ to run `install-to-project.py` and `configure-ef-packages-feed.py`
- Latest stable `.NET SDK`
- Docker engine running (Docker Desktop not required) - Aspire relies on it for hosting local container services
- VS Code + AI assistant
- Local SQL Server/Azure SQL access for dev scenarios
- GitHub Packages PAT for the private EF.Packages NuGet feed; local environments must set it before Phase 3/4 restore
- If using Uno UI:
  - `dotnet new install Uno.Templates`
  - `dotnet tool install -g uno.check` then `uno-check`
  - `dotnet tool install -g Microsoft.OpenApi.Kiota`

Version policy: prefer latest stable packages and SDKs.

## AI Agents & Harnesses - Quick Start

Clone this repo then run the install script to copy the instruction files into any target app repository root folder. The installer copies runtime instructions into `<app>/.instructions/` and places thin harness entrypoints at the app root. Scaffold rules stay app-scoped; do not put phase routing, TaskFlow rules, or generated-code conventions in global Codex, Claude, or Copilot instruction files.

### Supported harnesses

| Harness | Installed entrypoint | How to start |
|---|---|---|
| Codex CLI / CLI agents that read `AGENTS.md` | `<app>/AGENTS.md` | Ask the agent to scaffold or continue a phase; it loads `.instructions/START-AI.md` only after that explicit request. |
| GitHub Copilot in VS Code | `<app>/.github/agents/` | Select `dotnet-scaffold` or `vertical-slice` in the Copilot agent picker. |
| Claude Code / Claude VS Code extension | `<app>/.claude/commands/` when slash commands are supported | Run `/scaffold <domain>` or `/vertical-slice Product`. If commands are unavailable, prompt: `Load .instructions/START-AI.md and run the scaffold router.` |
| Generic AI assistant | `<app>/.instructions/START-AI.md` | Use [support/prompt-catalog.md](support/prompt-catalog.md), or directly ask the agent to load `.instructions/START-AI.md`. |

### Scaffold entrypoints

| Harness | Full scaffold | Vertical slice |
|---|---|---|
| GitHub Copilot | Select `dotnet-scaffold` in the agent picker | Select `vertical-slice` in the agent picker |
| Claude Code | `/scaffold <domain>` | `/vertical-slice <Entity>` |
| Codex CLI / CLI agents | Prompt: `Load .instructions/START-AI.md and run the scaffold router` | Prompt: `Load .instructions/support/vertical-slice-checklist.md` |
| Generic AI assistant | Prompt: `Load .instructions/START-AI.md and run the scaffold router` | Prompt: `Load .instructions/support/vertical-slice-checklist.md` |

### How they work

All harnesses follow the same flow:

1. **Scaffold** - Boot from `.instructions/START-AI.md`, check `HANDOFF.md` in the project root, load only the current phase files (per the Phase Router in START-AI.md and the Phase 5 file table in `ai/SKILL.md`), execute one phase, write `HANDOFF.md`, stop.
2. **Vertical slice** - Load `.instructions/support/vertical-slice-checklist.md`, generate the full entity stack (entity → EF config → repos → DTOs → mapper → validator → service → endpoint → DI wiring → migration), validate with `dotnet build` + `dotnet test`.

### Install into a new app

Use `install-to-project.py` from a local clone of this repo. It copies only the runtime payload - instruction files, scoped agents, CLI entrypoint, and slash commands - into your app, and skips repo-maintenance files (tests, CI workflows, global assistant instruction files, git hooks, virtualenvs).

`--target` is the **app repo root** (not the `.instructions/` folder). The script creates `<target>/.instructions/` if it does not exist, and writes `AGENTS.md`, `.claude/commands/`, and `.github/agents/` at the target root so CLI agents, Claude, and Copilot discover the scoped scaffold entrypoints.

```bash
# from a clone of this repo
python scripts/install-to-project.py --target /path/to/your-app-repo
# tip: run with --dry-run first to preview what gets copied
```

What it places:

| Source in this repo | Destination in your app |
|---|---|
| `README.md`, `CLAUDE.md`, `START-AI.md` | `<app>/.instructions/` |
| `ai/`, `patterns/`, `schemas/`, `skills/`, `support/`, `templates/`, `scripts/` | `<app>/.instructions/` |
| `AGENTS.md` | `<app>/AGENTS.md` (app repo root, so Codex-style CLI agents discover it) |
| `.claude/commands/` | `<app>/.claude/commands/` (app repo root, so Claude Code discovers them) |
| `.github/agents/` | `<app>/.github/agents/` (app repo root, so Copilot discovers the scoped agents) |

Flags:

| Flag | Purpose |
|---|---|
| `--dry-run` | Print planned copies without writing anything. |
| `--update` | Re-run against an existing install; preserves any target file with a newer mtime than the source. Leaves `HANDOFF.md` untouched. |
| `--instructions-only` | Copy only `<app>/.instructions/`; skip `AGENTS.md`, `.claude/commands/`, and `.github/agents/` placement (useful if you manage those separately). |

After install:

- [ ] Configure the private EF.Packages feed with `python .instructions/scripts/configure-ef-packages-feed.py --root . --feed-url https://nuget.pkg.github.com/{owner}/index.json --username {github-user}`.
- [ ] Confirm `dotnet restore` exits 0.
- [ ] Phase gates rely on `dotnet build` and `dotnet test`; the scaffold checklist at `support/final-scaffold-checklist.md` covers end-to-end acceptance.

### Manual copy (alternative)

If you prefer to copy by hand, remember: `AGENTS.md`, `.github/agents/`, and `.claude/commands/` must live at the **app repo root**, not inside `.instructions/`, so the tools discover them. Everything else goes under `.instructions/`. Do not copy scaffold routing into global assistant instruction files. The install script above does this automatically.

---

## Purpose

This instruction set turns an AI coding assistant into a guided scaffolding engine for production-grade C#/.NET solutions. Instead of generating throwaway boilerplate, it drives a structured five-phase process — from domain discovery through implementation — producing consistent, buildable, testable code that follows clean architecture and the conventions of a mature engineering team.

The goal is not to replace engineering judgment but to compress the multi day/week "green-field to first vertical slice" timeline down to hours, with guardrails that prevent common shortcuts (missing tests, leaky abstractions, inconsistent naming).

Phase 1 also creates durable collaboration artifacts before code planning starts:

- `UBIQUITOUS-LANGUAGE.md` records accepted domain terms, rejected synonyms, states, commands/actions, roles, events, policies, and naming guidance so later AI sessions use consistent expressions.
- `DESIGN-DECISIONS.md` records design choices and dependencies between decisions, so downstream resource and code choices do not silently contradict earlier domain answers.

### Phase-1 Artifact Lifecycle

`domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md` are **living source of truth**, not snapshots. Every phase consumes them, so they must stay current as the project evolves — otherwise later AI sessions reason from a stale model and naming/decision drift creeps in.

**When to update each:**

- **New entity, term, role, event, or domain action** → append the term to `UBIQUITOUS-LANGUAGE.md` and update the relevant section of `domain-specification.yaml` (entity, customAction, event, etc.) before generating code. The `/vertical-slice` checklist enforces this as a pre-flight step.
- **New design choice or revision of an earlier one** → append to `DESIGN-DECISIONS.md`. Do not silently rewrite earlier entries; mark the prior decision as superseded and link forward, so the dependency graph remains traceable.
- **Schema or relationship change** → update `domain-specification.yaml` first, then propagate to EF configuration, repositories, DTOs, mappers, and tests in that order.

**Drift signal.** If `UBIQUITOUS-LANGUAGE.md` and code identifiers diverge, the doc is wrong, not the code (per [support/final-scaffold-checklist.md](support/final-scaffold-checklist.md) language-failure rule). Update the doc to match accepted reality before changing code names. The same applies to `DESIGN-DECISIONS.md` — if the implemented architecture has moved past a recorded decision, supersede the entry rather than leaving the doc to contradict the code.

**Mid-scaffold corrections.** When a domain misunderstanding surfaces mid-Phase-5 (entity purpose wrong, term mismatched, decision violated), [support/OPERATIONS.md](support/OPERATIONS.md) is the canonical recovery path: clarify with the user, update the Phase-1 artifacts, then re-scaffold the affected slice.

**Do not delete.** These artifacts are the onboarding surface for every future AI session, code reviewer, and new team member. Keep them in the repo root for the life of the project — even when they grow long, the cost of reading them is far lower than the cost of an AI session reasoning from absent context.

## Phases

Each phase runs in its own AI session and produces artifacts the next phase consumes.

| Phase | Purpose | Output |
|---|---|---|
| **1 — Domain Discovery** | Structured interview to reach shared understanding, define ubiquitous language, resolve decision dependencies, and capture entities, relationships, events, workflows, and business rules in pure business language — no implementation details. | `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, `DESIGN-DECISIONS.md` |
| **2 — Resource Definition** | Map each resource requirement to concrete technology choices — data stores, messaging, AI capabilities, hosting models. | `resource-implementation.yaml` |
| **3 — Implementation Planning** | Resolve open questions, verify tooling (NuGet feeds, CLIs), discover project-specific CLIs and MCP servers, and produce a sequenced build plan. | `implementation-plan.md` |
| **4 — Contract Scaffolding** | Generate solution structure, interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Gate: `dotnet build` succeeds on the full solution. | Compilable skeleton |
| **5 — Implementation (TDD)** | Build vertical slices entity-by-entity across sub-phases 5a–5e (Foundation, App Core + Runtime, Optional Hosts, Quality + Delivery, Integration). Phase 5a uses test-driven development (write tests first → red → implement → green); 5b is mixed (TDD for app/API, tests-after for runtime); 5c–5e are tests-after. | Production code + passing tests |

Phase details: [START-AI.md](START-AI.md) § Phase Router.

## Approach

The instruction set is designed around three core ideas:

**1. Phased workflow with TDD.**
Five phases prevent hallucinated architecture by ensuring verified context before code is written. Phase 4 produces a compilable skeleton so that Phase 5a/5b can follow a strict red/green TDD cycle — tests are written against contracts before any implementation exists. See [ai/tdd-protocol.md](ai/tdd-protocol.md).

**2. Skills and templates as composable units.**
Implementation knowledge is split into ~27 skill files (how things work) and ~25 template files (what to generate). The Phase Router in `START-AI.md` and the Phase 5 file table in `ai/SKILL.md` tell the agent which files to load for the current phase or sub-phase.

**3. Composition patterns, not documentation alone.**
Pattern files in `patterns/` document how generated components wire together across projects — database context pooling, API startup sequences, request context resolution, cache configuration, and Aspire resource wiring. An index (`support/pattern-dispatcher.md`) maps each pattern file to its relevant phase. This grounds the generated output in proven, real-world patterns rather than abstract descriptions.

## Reference Application

A companion reference app — **TaskFlow** — demonstrates every pattern and convention these instructions produce:

**Repository:** <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

TaskFlow is a fully scaffolded task-management application built by following this instruction set end-to-end. It covers dual DbContext pooling, YARP gateway with claims transformation, Aspire orchestration, FusionCache with Redis backplane, TickerQ scheduling, Azure Functions, multitenancy, scaffold-mode auth, and Uno WASM UI.

Use it for:

- **Pattern lookups** — when an instruction or template describes a pattern (e.g., middleware ordering, repository split, cache key format), the reference app contains the working implementation.
- **Wiring verification** — cross-project DI registration, startup sequences, and Aspire resource definitions are all present and buildable.
- **Test structure** — unit, integration, architecture, and endpoint test projects are scaffolded with builder patterns.

For a phase-by-phase pointer map into the reference app, use [support/taskflow-proof-map.md](support/taskflow-proof-map.md).

The AI assistant can access the repo via GitHub MCP or by cloning it locally. When stuck on how a pattern should look in practice, consult the reference app before inventing a new approach. The reference app is always available as a live codebase the AI can search, read, and cross-reference during any phase.

## Quick Start

If you want the shortest path from zero context to first scaffold:

1. Create a new app repo.
2. Run `python scripts/install-to-project.py --target /path/to/your-app-repo` from this repo.
3. Start through the harness table above: `AGENTS.md`, Copilot agent, Claude command, or a prompt that loads `.instructions/START-AI.md`.

Read the rest of this guide when you need setup details, MCP recommendations, or troubleshooting rules.

## Prerequisites

- `git`
- Python 3.11+ to run `install-to-project.py` and `configure-ef-packages-feed.py`
- Latest stable `.NET SDK`
- Docker engine running (Docker Desktop not required) — Aspire relies on it for hosting local container services
- VS Code + AI assistant
- Local SQL Server/Azure SQL access for dev scenarios
- GitHub Packages PAT for the private EF.Packages NuGet feed; local environments must set it before Phase 3/4 restore
- If using Uno UI:
  - `dotnet new install Uno.Templates`
  - `dotnet tool install -g uno.check` then `uno-check`
  - `dotnet tool install -g Microsoft.OpenApi.Kiota`

Version policy: prefer latest stable packages and SDKs.

### EF.Packages Feed Setup

The scaffold depends on private `EF.*` packages. Local restore requires a GitHub PAT with package read access.

Use an environment variable rather than committing secrets:

```powershell
$env:NUGET_AUTH_TOKEN = "ghp_xxxxxxxxxxxxxxxxxxxx"
```

Then ensure `nuget.config` maps `EF.*` to the private feed and uses `%NUGET_AUTH_TOKEN%` for the feed password. Configure once, then verify with `dotnet restore`:

```powershell
python .instructions/scripts/configure-ef-packages-feed.py --root . --feed-url https://nuget.pkg.github.com/{owner}/index.json --username {github-user}
dotnet restore
```

Never commit a PAT. CI should inject the same token through secret variables.

## Repository Setup

1. Create a new empty app repo.
2. Install this instruction set with `python scripts/install-to-project.py --target /path/to/your-app-repo`.
3. Open the app repo in VS Code or your CLI harness.

Expected shape (note `AGENTS.md`, `.github/agents/`, and `.claude/commands/` live at the app repo root, not inside `.instructions/`):

```text
<YourApp>/
  AGENTS.md                       # CLI agents discover here; loads .instructions/START-AI.md only on explicit scaffold request
  .github/
    agents/                        # Copilot discovers scoped agents here
      dotnet-scaffold.agent.md
      vertical-slice.agent.md
  .claude/
    commands/                      # Claude discovers scoped commands here
      scaffold.md
      vertical-slice.md
  .instructions/
    README.md
    CLAUDE.md
    START-AI.md
    ai/
      SKILL.md
      domain-specification-schema.md
      resource-implementation-schema.md
      implementation-plan.md
      placeholder-tokens.md
    support/
      HANDOFF.md
      execution-gates.md
      troubleshooting.md
    skills/
    scripts/
    templates/
    schemas/
  README.md
```

Notes:
- `.instructions/support/HANDOFF.md` is the template.
- A working `HANDOFF.md` is created in the target project root at the end of every phase and updated after each Phase 5 sub-phase.

## Recommended MCP Servers

> **Prefer CLI over MCP when both exist.** CLI tools have lower token cost and faster execution. Use MCP servers for interactive exploration or when no CLI equivalent is available.

### Core (always on)

| Server | Why | CLI alternative |
|---|---|---|
| Microsoft Docs MCP | Official .NET/Azure docs, samples, full-page retrieval | - |
| Context7 MCP | Third-party library/API docs | - |

### Enable by phase

| Server | Enable when | CLI alternative |
|---|---|---|
| GitHub MCP | Repo workflows, issues/PRs, CI visibility | `gh` CLI (preferred) |
| Azure MCP | IaC/deployment/resource validation | `az` CLI (preferred) |
| Playwright MCP | UI E2E validation/debugging | - |
| Fetch MCP | Pull external specs/docs into markdown | `curl` / `Invoke-RestMethod` |
| Sequential Thinking MCP | Complex design/debug reasoning | - |

Optional additions: Git, Docker (`docker` CLI preferred), Memory, web-search MCPs, Azure DevOps MCP (`az devops` CLI preferred).

### Tooling discovery

Phase 3 analyzes `resource-implementation.yaml` technology choices and actively researches available CLIs and MCP servers for the project's specific libraries and services. Results are recorded in the implementation plan's **Tooling & Environment Readiness** section and verified at the start of each subsequent phase.

**CLI → MCP → online resources:** Prefer CLI tools first (lowest token cost), then MCP servers for interactive exploration, then documentation URLs and GitHub repos the AI can fetch during implementation.

## Mode Selection

| Need | Mode |
|---|---|
| Fast internal tool, minimal infra | `scaffoldMode: lite` |
| Production-ready with optional hosts | `scaffoldMode: full` |
| Single API, no gateway/UI/scheduler | `scaffoldMode: api-only` |

Defaults: [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) **Canonical Defaults**.

## Happy Path

1. Prerequisites and repo setup (see [Quick Start](#quick-start) and [Prerequisites](#prerequisites))
2. Phase 1: Domain YAML + language + decisions → [ai/shared-understanding-interview.md](ai/shared-understanding-interview.md)
3. Phase 2: Resource YAML → [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md)
4. Phase 3: Implementation plan → [ai/implementation-plan.md](ai/implementation-plan.md)
5. Phase 4: Contract scaffolding → [ai/contract-scaffolding.md](ai/contract-scaffolding.md)
6. Phase 5: Implementation (TDD) → [ai/SKILL.md](ai/SKILL.md) + [ai/tdd-protocol.md](ai/tdd-protocol.md)
7. Validate gates → [support/execution-gates.md](support/execution-gates.md)
8. Final scaffold check → [support/final-scaffold-checklist.md](support/final-scaffold-checklist.md)
9. Troubleshoot → [support/troubleshooting.md](support/troubleshooting.md)

## Prompt Catalog

For copy-paste phase prompts, see [support/prompt-catalog.md](support/prompt-catalog.md). The catalog is a convenience layer for engineers; [START-AI.md](START-AI.md) remains the canonical operational bootstrap for AI execution.

## Operational References

These references are for **maintaining and developing the instruction set itself** - not for using it to scaffold a new application. For app scaffolding, see [Quick Start](#quick-start) and [Happy Path](#happy-path).

- [START-AI.md](START-AI.md) - canonical AI bootstrap, version checks, phase routing, and load rules
- [support/prompt-catalog.md](support/prompt-catalog.md) - copy-paste prompts for starting or resuming a session
- [support/execution-gates.md](support/execution-gates.md) - canonical validation gates and operator setup checklist
- [support/golden-path-sample.md](support/golden-path-sample.md) - canonical small sample for regression-checking scaffold instructions
- [support/final-scaffold-checklist.md](support/final-scaffold-checklist.md) - final generated-app scaffold acceptance checklist
- [support/troubleshooting.md](support/troubleshooting.md) - failure triage and recurring issue guidance
- [support/taskflow-proof-map.md](support/taskflow-proof-map.md) - fast reference-app proof map from instruction concern to TaskFlow area
- [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md) - capture improvements discovered during scaffolding

Useful script entrypoints:

- `scripts/install-to-project.py` - copy the runtime payload into a consumer app's `.instructions/` directory and place harness entrypoints at the app root.
- `scripts/configure-ef-packages-feed.py` - create/update target-app `nuget.config` for EF.Packages without writing PATs.

## Document Ownership

- `README.md` - human onboarding and repository overview
- `AGENTS.md` - root CLI-agent scaffold entrypoint for installed app repos
- `START-AI.md` - canonical AI session bootstrap and phase router
- `ai/SKILL.md` - scaffolding policy and conventions (loaded as Phase 5 base)
 
## Layout

- Root: entry points (`AGENTS.md`, `README.md`, `CLAUDE.md`, `START-AI.md`)
- `ai/`: phase schemas, execution guidance, and AI-loaded base docs
- `support/`: operator checklists, troubleshooting, handoff template, prompt catalog, and repo change notes
- `skills/`, `templates/`, `scripts/`, `schemas/`: domain-specific content and validation schemas

