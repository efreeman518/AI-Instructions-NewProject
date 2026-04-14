# AI Instructions — New .NET App/Service

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET business applications and services.

## Purpose

This instruction set turns an AI coding assistant into a guided scaffolding engine for production-grade C#/.NET solutions. Instead of generating throwaway boilerplate, it drives a structured five-phase process — from domain discovery through implementation — producing consistent, buildable, testable code that follows clean architecture and the conventions of a mature engineering team.

The goal is not to replace engineering judgment but to compress the 2–5 day "green-field to first vertical slice" timeline down to hours, with guardrails that prevent common shortcuts (missing tests, leaky abstractions, inconsistent naming).

## Phases

Each phase runs in its own AI session and produces artifacts the next phase consumes.

| Phase | Purpose | Output |
|---|---|---|
| **1 — Domain Discovery** | Structured conversation to define entities, relationships, events, workflows, and business rules in pure business language — no implementation details. | `domain-specification.yaml` |
| **2 — Resource Definition** | Map each domain concept to concrete technology choices — data stores, messaging, AI capabilities, hosting models. | `resource-implementation.yaml` |
| **3 — Implementation Planning** | Resolve open questions, verify tooling (NuGet feeds, EF CLI), and produce a sequenced build plan. | `implementation-plan.md` |
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
A comprehensive pattern catalog (`support/sampleapp-patterns.md`) documents how all generated components wire together — startup sequences, database pooling, request context resolution, cache configuration, and middleware ordering. This grounds the generated output in proven, real-world patterns rather than abstract descriptions.

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
3. Run `python .instructions/scripts/preflight-instructions.py` once to validate the copied instruction set. This refreshes manifest metadata, regenerates load packs, prints the current context-budget report, lints the docs, and runs the Python script test suite. PowerShell equivalent: `./.instructions/scripts/preflight-instructions.ps1`.
4. Start Phase 1 with the Phase 1 prompt in [support/prompt-catalog.md](support/prompt-catalog.md).
5. When you reach implementation, begin the AI session with [START-AI.md](START-AI.md).

Read the rest of this guide when you need setup details, MCP recommendations, or troubleshooting rules.

## Prerequisites

- `git`
- Latest stable `.NET SDK`
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

Expected shape:

```text
<YourApp>/
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
- The working `HANDOFF.md` used to resume AI sessions is created in the target project root during Phase 5.

## Recommended MCP Servers

### Core (always on)

| Server | Why |
|---|---|
| Microsoft Docs MCP | Official .NET/Azure docs, samples, full-page retrieval |
| Context7 MCP | Third-party library/API docs |

### Enable by phase

| Server | Enable when |
|---|---|
| GitHub MCP | Repo workflows, issues/PRs, CI visibility |
| Azure MCP | IaC/deployment/resource validation |
| Playwright MCP | UI E2E validation/debugging |
| Fetch MCP | Pull external specs/docs into markdown |
| Sequential Thinking MCP | Complex design/debug reasoning |

Optional additions: Git, Docker, Memory, web-search MCPs, Azure DevOps MCP.

### Dynamic discovery protocol

Before each phase, quickly check for new MCP servers for libraries/services in scope:
1. Search npm (`mcp + <library/service>`)
2. Check MCP registry
3. If useful, add and note in [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md)

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
| **Composition pattern catalog** | A comprehensive pattern catalog (`support/sampleapp-patterns.md`) documents cross-file wiring — startup sequences, database pooling, request context, cache configuration, middleware ordering — with inline code snippets. |
| **Prompt catalog** | Copy-paste prompts for each phase live in [support/prompt-catalog.md](support/prompt-catalog.md), keeping `README.md` focused on human onboarding while [START-AI.md](START-AI.md) stays canonical for execution. |

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

- [START-AI.md](START-AI.md) — canonical AI bootstrap, version checks, phase routing, and load rules
- [support/prompt-catalog.md](support/prompt-catalog.md) — copy-paste prompts for starting or resuming a session
- [support/execution-gates.md](support/execution-gates.md) — canonical validation gates and operator setup checklist
- [support/troubleshooting.md](support/troubleshooting.md) — failure triage and recurring issue guidance
- [support/taskflow-proof-map.md](support/taskflow-proof-map.md) — fast reference-app proof map from instruction concern to TaskFlow area
- [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md) — capture improvements discovered during scaffolding

Run `python scripts/preflight-instructions.py` before Phase 4 execution and before opening validation PRs. It refreshes `_manifest.json`, regenerates `phase-load-packs.json`, prints the current context-budget report, lints markdown invariants, and runs the Python unittest suite in `tests/`. PowerShell equivalent: `./scripts/preflight-instructions.ps1`.

For an on-demand budget snapshot without the full preflight, run `python scripts/report-context-budgets.py --mode full`.

## Document Ownership

- `README.md` — human onboarding and repository overview
- `START-AI.md` — canonical AI session bootstrap and phase router
- `ai/SKILL.md` — Phase 5 execution policy only

## Layout

- Root: entry points and machine-readable metadata (`README.md`, `START-AI.md`, `_manifest.json`, `phase-load-packs.json`)
- `ai/`: phase schemas, execution guidance, and AI-loaded base docs
- `support/`: operator checklists, troubleshooting, handoff template, prompt catalog, and repo change notes
- `skills/`, `templates/`, `scripts/`, `schemas/`: domain-specific content and validation schemas


