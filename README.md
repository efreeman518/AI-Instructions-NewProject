# AI Instructions — New .NET App/Service

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET business applications and services.

## AI Agents — Quick Start

This instruction set ships with pre-built agents for **VS Code Copilot** and **Claude Code**. After copying the instruction set into your app repo's `.instructions/` folder, the agents are ready to use.

### VS Code Copilot

Agents live in `.instructions/.github/agents/`. Copy (or symlink) the `.github/agents/` folder to your app repo root so Copilot discovers them.

| Agent | How to invoke | Purpose |
|-------|---------------|---------|
| `dotnet-scaffold` | Select **dotnet-scaffold** in the Copilot agent picker | Full phased scaffolding (Phases 1–5g), one phase per session |
| `vertical-slice` | Select **vertical-slice** in the Copilot agent picker | Add a new entity to an existing solution (fast-path) |

### Claude Code

Commands live in `.instructions/.claude/commands/`. Copy (or symlink) the `.claude/` folder to your app repo root.

| Command | How to invoke | Purpose |
|---------|---------------|---------|
| `/scaffold` | `/scaffold <business domain description>` | Full phased scaffolding, one phase per session |
| `/vertical-slice` | `/vertical-slice Product` | Add a new entity slice to an existing solution |

### How they work

Both agents follow the same flow:

1. **Scaffold** — Boots from `.instructions/START-AI.md`, checks `HANDOFF.md` in the project root for resume state, loads only the current phase's files via `.instructions/phase-load-packs.json`, executes one phase, writes `HANDOFF.md` on completion.
2. **Vertical slice** — Loads `.instructions/support/vertical-slice-checklist.md` fast-path, generates the full entity stack (12 steps: entity → EF config → repos → DTOs → mapper → validator → service → endpoint → DI wiring → migration), validates with `dotnet build` + `dotnet test`.

### Install into a new app

Use `install-to-project.py` from a local clone of this repo. It copies only the runtime payload — instruction files, agents, and slash commands — into your app, and skips repo-maintenance files (tests, CI workflows, git hooks, virtualenvs, README).

`--target` is the **app repo root** (not the `.instructions/` folder). The script creates `<target>/.instructions/` if it does not exist, and writes `.claude/commands/` and `.github/agents/` at the target root so Claude Code and Copilot discover them.

```bash
# from a clone of this repo
python scripts/install-to-project.py --target /path/to/your-app-repo
# tip: run with --dry-run first to preview what gets copied
```

What it places:

| Source in this repo | Destination in your app |
|---|---|
| `CLAUDE.md`, `START-AI.md`, `phase-load-packs.json`, `_manifest.json` | `<app>/.instructions/` |
| `ai/`, `patterns/`, `schemas/`, `skills/`, `support/`, `templates/`, `scripts/` | `<app>/.instructions/` |
| `.claude/commands/` | `<app>/.claude/commands/` (app repo root, so Claude Code discovers them) |
| `.github/agents/`, `.github/copilot-instructions.md` | `<app>/.github/` (app repo root, so Copilot discovers them) |

Flags:

| Flag | Purpose |
|---|---|
| `--dry-run` | Print planned copies without writing anything. |
| `--update` | Re-run against an existing install; preserves any target file with a newer mtime than the source. Leaves `HANDOFF.md` untouched. |
| `--instructions-only` | Copy only `<app>/.instructions/`; skip `.claude/commands/` and `.github/agents/` placement (useful if you manage those separately). |

After install:

- [ ] Run `python .instructions/scripts/preflight-instructions.py` in the app repo to validate the copied set.

### Manual copy (alternative)

If you prefer to copy by hand, remember: `.github/agents/` and `.claude/commands/` must live at the **app repo root**, not inside `.instructions/`, so the tools discover them. Everything else goes under `.instructions/`. The install script above does this automatically.

---

## Purpose

This instruction set turns an AI coding assistant into a guided scaffolding engine for production-grade C#/.NET solutions. Instead of generating throwaway boilerplate, it drives a structured five-phase process — from domain discovery through implementation — producing consistent, buildable, testable code that follows clean architecture and the conventions of a mature engineering team.

The goal is not to replace engineering judgment but to compress the multi day/week "green-field to first vertical slice" timeline down to hours, with guardrails that prevent common shortcuts (missing tests, leaky abstractions, inconsistent naming).

## Phases

Each phase runs in its own AI session and produces artifacts the next phase consumes.

| Phase | Purpose | Output |
|---|---|---|
| **1 — Domain Discovery** | Structured conversation to define entities, relationships, events, workflows, and business rules in pure business language — no implementation details. | `domain-specification.yaml` |
| **2 — Resource Definition** | Map each resource requirement to concrete technology choices — data stores, messaging, AI capabilities, hosting models. | `resource-implementation.yaml` |
| **3 — Implementation Planning** | Resolve open questions, verify tooling (NuGet feeds, CLIs), discover project-specific CLIs and MCP servers, and produce a sequenced build plan. | `implementation-plan.md` |
| **4 — Contract Scaffolding** | Generate solution structure, interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Gate: `dotnet build` succeeds on the full solution. | Compilable skeleton |
| **5 — Implementation (TDD)** | Build vertical slices entity-by-entity across sub-phases 5a–5g. Phases 5a/5b use test-driven development (write tests first → red → implement → green). Phases 5c–5g use tests-after. | Production code + passing tests |

Phase details: [START-AI.md](START-AI.md) § Phase Router.

## Approach

The instruction set is designed around three core ideas:

**1. Phased workflow with TDD.**
Five phases prevent hallucinated architecture by ensuring verified context before code is written. Phase 4 produces a compilable skeleton so that Phase 5a/5b can follow a strict red/green TDD cycle — tests are written against contracts before any implementation exists. See [ai/tdd-protocol.md](ai/tdd-protocol.md).

**2. Skills and templates as composable units.**
Implementation knowledge is split into ~27 skill files (how things work) and ~25 template files (what to generate). An AI token manifest (`_manifest.json`) tracks estimated token costs, phase membership, and mode exclusions so the assistant loads only what's needed per phase, staying within context budgets. Templates carry `generates` and `requires` metadata, and the load-set resolver expands transitive dependencies automatically.

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

1. Create a new app repo and copy this instruction set into `.instructions/`.
2. Open the app repo in VS Code.
3. Run `python .instructions/scripts/preflight-instructions.py` once to validate the copied instruction set. This refreshes manifest metadata, regenerates load packs, prints the current context-budget report, lints the docs, and runs the Python script test suite.
4. Start Phase 1 with the Phase 1 prompt in [support/prompt-catalog.md](support/prompt-catalog.md).
5. When you reach implementation, begin the AI session with [START-AI.md](START-AI.md).

Read the rest of this guide when you need setup details, MCP recommendations, or troubleshooting rules.

## Prerequisites

- `git`
- Latest stable `.NET SDK`
- Docker engine running (Docker Desktop not required) — Aspire relies on it for hosting local container services
- VS Code + AI assistant
- Local SQL Server/Azure SQL access for dev scenarios
- Private feed URLs if using internal packages (`customNugetFeeds`)
- If using Uno UI:
  - `dotnet new install Uno.Templates`
  - `dotnet tool install -g uno.check` then `uno-check`
  - `dotnet tool install -g Microsoft.OpenApi.Kiota`

Version policy: prefer latest stable packages and SDKs.

## Repository Setup

1. Create a new empty app repo.
2. Copy this instruction set into `.instructions/` in that repo.
3. Open the app repo in VS Code.

Expected shape (note `.github/agents/` and `.claude/commands/` live at the app repo root, not inside `.instructions/`):

```text
<YourApp>/
  .github/
    agents/                        # copied from .instructions/.github/agents/ — Copilot discovers here
      dotnet-scaffold.agent.md
      vertical-slice.agent.md
  .claude/
    commands/                      # copied from .instructions/.claude/commands/ — Claude Code discovers here
      scaffold.md
      vertical-slice.md
  .instructions/
    README.md
    START-AI.md
    _manifest.json
    phase-load-packs.json
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
    tests/
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
| Microsoft Docs MCP | Official .NET/Azure docs, samples, full-page retrieval | — |
| Context7 MCP | Third-party library/API docs | — |

### Enable by phase

| Server | Enable when | CLI alternative |
|---|---|---|
| GitHub MCP | Repo workflows, issues/PRs, CI visibility | `gh` CLI (preferred) |
| Azure MCP | IaC/deployment/resource validation | `az` CLI (preferred) |
| Playwright MCP | UI E2E validation/debugging | — |
| Fetch MCP | Pull external specs/docs into markdown | `curl` / `Invoke-RestMethod` |
| Sequential Thinking MCP | Complex design/debug reasoning | — |

Optional additions: Git, Docker (`docker` CLI preferred), Memory, web-search MCPs, Azure DevOps MCP (`az devops` CLI preferred).

### Tooling discovery

Phase 3 analyzes `resource-implementation.yaml` technology choices and actively researches available CLIs and MCP servers for the project's specific libraries and services. Results are recorded in the implementation plan's **Tooling & Environment Readiness** section and verified at the start of each subsequent phase.

**CLI → MCP → online resources:** Prefer CLI tools first (lowest token cost), then MCP servers for interactive exploration, then documentation URLs and GitHub repos the AI can fetch during implementation.

## High-Value Features

| Feature | What It Does |
|---|---|
| **Domain discovery conversation** | Phase 1 drives a structured discussion to define entities, relationships, events, workflows, and business rules in pure business language — no implementation details. The resulting [domain specification](ai/domain-specification-schema.md) YAML becomes the single source of truth that every later phase consumes, preventing scope drift and hallucinated architecture. |
| **Resource & technology mapping** | Phase 2 is a deliberate conversation about *how* to implement each domain concept — which data store fits each entity (SQL, Cosmos DB, Table Storage), where messaging is needed (Service Bus, Event Grid), whether AI capabilities apply (search, agents, document intelligence), and which hosting model suits each workload. The output is a [resource implementation](ai/resource-implementation-schema.md) YAML that locks in technology choices before any code is written. |
| **Token-aware phase loading** | A manifest and Python scripts calculate per-file token costs and generate phase-specific load sets, keeping AI context usage under a configurable ceiling (~30K tokens/phase). |
| **Three scaffolding modes** | `full` (production w/ gateway, scheduler, UI, IaC), `lite` (clean architecture without infra), `api-only` (single API host). Set once in resource YAML, all downstream loading adapts. |
| **Vertical-slice scaffolding** | Each entity is built end-to-end: domain model → EF config → repository → DTO/mapper → service → endpoint → tests. A [checklist](support/vertical-slice-checklist.md) and [execution gates](support/execution-gates.md) enforce completeness. |
| **Built-in quality gates** | `dotnet build` + targeted tests after every sub-phase. Architecture tests enforce layer dependencies. Structure validators catch DTO issues before runtime. |
| **Automated validation scripts** | Python scripts lint instruction files (broken links, placeholder coverage, terminology drift, manifest sync), validate domain/resource YAML schemas, and run preflight checks before scaffolding begins. |
| **Visible budget proof loop** | `scripts/report-context-budgets.py` reports hot-path phase totals and compact 5a/5b slice totals directly from `_manifest.json` + `phase-load-packs.json`, and preflight prints that report on every run. |
| **Safe session handoff** | Context budget tracking per sub-phase plus a `HANDOFF.md` template let the AI resume cleanly across sessions without losing progress or re-reading the full instruction set. |
| **Result pattern error flow** | Domain → Service → Endpoint error mapping is fully documented with a type mapping table, anti-patterns, and end-to-end trace example. No exceptions for business logic. |
| **Test data builders** | Fluent builder patterns for entities and DTOs ensure tests construct valid objects by default and override only the property under test. |
| **Composition pattern catalog** | Pattern files in `patterns/` document cross-project wiring — database pooling, API startup sequence, request context, cache configuration, Aspire resource wiring — with inline code snippets. An index (`support/pattern-dispatcher.md`) maps each to its relevant phase. |
| **Prompt catalog** | Copy-paste prompts for each phase live in [support/prompt-catalog.md](support/prompt-catalog.md), keeping `README.md` focused on human onboarding while [START-AI.md](START-AI.md) stays canonical for execution. |
| **Event boundary enforcement** | Cross-process events are modeled as integration contracts in `Application.Contracts.Events` and published via `IIntegrationEventPublisher`; Domain events remain aggregate-local. This avoids layered leakage and naming drift. |

## Mode Selection

| Need | Mode |
|---|---|
| Fast internal tool, minimal infra | `scaffoldMode: lite` |
| Production-ready with optional hosts | `scaffoldMode: full` |
| Single API, no gateway/UI/scheduler | `scaffoldMode: api-only` |

Defaults: [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) **Canonical Defaults**.

## Happy Path

1. Prerequisites and repo setup (see [Quick Start](#quick-start) and [Prerequisites](#prerequisites))
2. Phase 1: Domain YAML → [ai/domain-specification-schema.md](ai/domain-specification-schema.md)
3. Phase 2: Resource YAML → [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md)
4. Phase 3: Implementation plan → [ai/implementation-plan.md](ai/implementation-plan.md)
5. Phase 4: Contract scaffolding → [ai/contract-scaffolding.md](ai/contract-scaffolding.md)
6. Phase 5: Implementation (TDD) → [ai/SKILL.md](ai/SKILL.md) + [ai/tdd-protocol.md](ai/tdd-protocol.md)
7. Validate gates → [support/execution-gates.md](support/execution-gates.md)
8. Troubleshoot → [support/troubleshooting.md](support/troubleshooting.md)

## Prompt Catalog

For copy-paste phase prompts, see [support/prompt-catalog.md](support/prompt-catalog.md). The catalog is a convenience layer for engineers; [START-AI.md](START-AI.md) remains the canonical operational bootstrap for AI execution.

## Operational References

These references are for **maintaining and developing the instruction set itself** — not for using it to scaffold a new application. For app scaffolding, see [Quick Start](#quick-start) and [Happy Path](#happy-path).

- [START-AI.md](START-AI.md) — canonical AI bootstrap, version checks, phase routing, and load rules
- [support/prompt-catalog.md](support/prompt-catalog.md) — copy-paste prompts for starting or resuming a session
- [support/execution-gates.md](support/execution-gates.md) — canonical validation gates and operator setup checklist
- [support/troubleshooting.md](support/troubleshooting.md) — failure triage and recurring issue guidance
- [support/taskflow-proof-map.md](support/taskflow-proof-map.md) — fast reference-app proof map from instruction concern to TaskFlow area
- [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md) — capture improvements discovered during scaffolding

Run `python scripts/preflight-instructions.py` before Phase 4 execution and before opening validation PRs. It refreshes `_manifest.json`, regenerates `phase-load-packs.json`, prints the current context-budget report, lints markdown invariants, and runs the Python unittest suite in `tests/`.

For an on-demand budget snapshot without the full preflight, run `python scripts/report-context-budgets.py --mode full`.

## Document Ownership

- `README.md` — human onboarding and repository overview
- `START-AI.md` — canonical AI session bootstrap and phase router
- `ai/SKILL.md` — scaffolding policy and conventions (loaded as Phase 5 base)
 
## Layout

- Root: entry points and machine-readable metadata (`README.md`, `START-AI.md`, `_manifest.json`, `phase-load-packs.json`)
- `ai/`: phase schemas, execution guidance, and AI-loaded base docs
- `support/`: operator checklists, troubleshooting, handoff template, prompt catalog, and repo change notes
- `skills/`, `templates/`, `scripts/`, `schemas/`: domain-specific content and validation schemas
