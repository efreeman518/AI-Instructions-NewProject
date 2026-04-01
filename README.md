# AI Instructions — New .NET App/Service

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET business applications and services.

## Purpose

This instruction set turns an AI coding assistant into a guided scaffolding engine for production-grade C#/.NET solutions. Instead of generating throwaway boilerplate, it drives a structured four-phase process — from domain discovery through implementation — producing consistent, buildable, testable code that follows clean architecture and the conventions of a mature engineering team.

The goal is not to replace engineering judgment but to compress the 2–5 day "green-field to first vertical slice" timeline down to hours, with guardrails that prevent common shortcuts (missing tests, leaky abstractions, inconsistent naming).

## Approach

The instruction set is designed around three core ideas:

**1. Phased workflow, not a single prompt.**
Work proceeds through four phases — Domain Discovery, Resource Definition, Implementation Planning, and Implementation. Each phase produces a concrete artifact (YAML spec, implementation plan, or code) that the next phase consumes. This prevents hallucinated architecture and ensures the AI has verified context before writing code.

**2. Skills and templates as composable units.**
Implementation knowledge is split into ~27 skill files (how things work) and ~20 template files (what to generate). An AI token manifest (`_manifest.json`) tracks estimated token costs, phase membership, and mode exclusions so the assistant loads only what's needed per phase, staying within context budgets. Templates carry `generates` and `requires` metadata, and the load-set resolver expands transitive dependencies automatically.

**3. Composition patterns, not documentation alone.**
A comprehensive pattern catalog (`support/sampleapp-patterns.md`) documents how all generated components wire together — startup sequences, database pooling, request context resolution, cache configuration, and middleware ordering. This grounds the generated output in proven, real-world patterns rather than abstract descriptions.

## Quick Start

If you want the shortest path from zero context to first scaffold:

1. Create a new app repo and copy this instruction set into `.instructions/`.
2. Open the app repo in VS Code.
3. Run `./.instructions/scripts/preflight-instructions.ps1` once to validate the copied instruction set.
4. Start Phase 1 with the Phase 1 prompt in the [Prompt Patterns](#prompt-patterns) section.
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
    scripts/
    templates/
    schemas/
  README.md
```

Notes:
- `.instructions/support/HANDOFF.md` is the template.
- The working `HANDOFF.md` used to resume AI sessions is created in the target project root during Phase 4.

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
| **Token-aware phase loading** | A manifest and PowerShell scripts calculate per-file token costs and generate phase-specific load sets, keeping AI context usage under a configurable ceiling (~30K tokens/phase). |
| **Three scaffolding modes** | `full` (production w/ gateway, scheduler, UI, IaC), `lite` (clean architecture without infra), `api-only` (single API host). Set once in resource YAML, all downstream loading adapts. |
| **Vertical-slice scaffolding** | Each entity is built end-to-end: domain model → EF config → repository → DTO/mapper → service → endpoint → tests. A [checklist](support/vertical-slice-checklist.md) and [execution gates](support/execution-gates.md) enforce completeness. |
| **Built-in quality gates** | `dotnet build` + targeted tests after every sub-phase. Architecture tests enforce layer dependencies. Structure validators catch DTO issues before runtime. |
| **Automated validation scripts** | PowerShell scripts lint instruction files (broken links, placeholder coverage, terminology drift, manifest sync), validate domain/resource YAML schemas, and run preflight checks before scaffolding begins. |
| **Safe session handoff** | Context budget tracking per sub-phase plus a `HANDOFF.md` template let the AI resume cleanly across sessions without losing progress or re-reading the full instruction set. |
| **Result pattern error flow** | Domain → Service → Endpoint error mapping is fully documented with a type mapping table, anti-patterns, and end-to-end trace example. No exceptions for business logic. |
| **Test data builders** | Fluent builder patterns for entities and DTOs ensure tests construct valid objects by default and override only the property under test. |
| **Composition pattern catalog** | A comprehensive pattern catalog (`support/sampleapp-patterns.md`) documents cross-file wiring — startup sequences, database pooling, request context, cache configuration, middleware ordering — with inline code snippets. |
| **Prompt starters** | Copy-paste prompts for each phase let engineers kick off AI sessions without memorizing the workflow. |

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
5. Phase 4: Build → [ai/SKILL.md](ai/SKILL.md)
6. Validate gates → [support/execution-gates.md](support/execution-gates.md)
7. Troubleshoot → [support/troubleshooting.md](support/troubleshooting.md)

## Prompt Patterns

Copy-paste these prompts to start a phase. Replace `{...}` with actual values.

**Session model:** Each phase (and each Phase 4 sub-phase) runs in its own AI session. At the end of each session the AI writes/updates `HANDOFF.md` in the target project root, then the session closes. The next session starts from `START-AI.md` + `HANDOFF.md` only.

### Phase 1 — Domain Discovery (Session 1)

```text
Load .instructions/START-AI.md. This is a new project — no HANDOFF.md yet.
Generate a domain-specification YAML for a new application called {ProjectName}.
The business is: {one-sentence business description}.
Key entities: {entity list}.
Follow .instructions/ai/domain-specification-schema.md.
When the YAML is complete and reviewed, write HANDOFF.md to the project root and close the session.
```

### Phase 2 — Resource Definition (Session 2)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Generate the resource implementation YAML per .instructions/ai/resource-implementation-schema.md.
Mode: {full|lite|api-only}. Testing profile: {minimal|balanced|comprehensive}.
Declare externalDependencyModes for every external dependency before finalizing.
When the YAML is complete and the Phase 2→3 transition gate passes, update HANDOFF.md and close the session.
```

### Phase 3 — Implementation Plan (Session 3)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Generate an implementation plan per .instructions/ai/implementation-plan.md template.
Run Phase 3 pre-flights: NuGet feeds configured (dotnet restore exits 0), dotnet ef available.
Flag open questions before writing implementation-plan.md to the project root.
When the plan is reviewed and open questions resolved, update HANDOFF.md and close the session.
```

### Phase 4 — Sub-Phase Start Prompts

Each sub-phase is its own session. Start every Phase 4 session with:

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
```

Then append the sub-phase-specific block below.

#### 4a — Foundation

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4a -Mode {full|lite|api-only} to resolve the load set.
Scaffold Phase 4a: solution structure, domain model, data access, core entity/config/repository/appsettings.
Gate: 'dotnet build' passes. When gate passes, update HANDOFF.md (currentSubPhase: 4b) and close session.
```

#### 4b — App Core

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4b -Mode {full|lite|api-only}.
Scaffold Phase 4b: DTOs, mappers, services, endpoints, bootstrapper DI wiring.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Endpoint'. When gate passes, update HANDOFF.md and close session.
```

#### 4c — Runtime / Edge

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4c -Mode {full|lite|api-only}.
Scaffold only the enabled runtime concerns: {gateway/aspire/caching/observability/security}.
Gate: 'dotnet build' + app starts via Aspire. When gate passes, update HANDOFF.md and close session.
```

#### 4d — Optional Hosts

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4d -Mode {full|lite|api-only}.
Scaffold only the enabled optional hosts: {scheduler/functionapp/notifications}.
Update hostGates in HANDOFF.md per host as each reaches scaffolded → validated.
Close session when all enabled hosts are validated (or blockers are recorded for deployment-only deps).
```

> Note: Uno UI is always a dedicated session within 4d. Use the same session start prompt but scope only to Uno UI.

#### 4e — Quality + Delivery

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4e -Mode {full|lite|api-only}.
Scaffold tests (profile: {minimal|balanced|comprehensive}), IaC, and CI/CD as in scope.
Gate: 'dotnet test' passes; 'az bicep build --file infra/main.bicep' passes (if IaC enabled). Update HANDOFF.md and close session.
```

#### 4f — Authentication

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4f -Mode {full|lite|api-only}.
Finalize auth: replace stubs with config-driven scaffold principal ({Entra|OAuth2|social|hybrid}).
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Endpoint'. Update HANDOFF.md and close session.
```

#### 4g — AI Integration

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4g -Mode {full|lite|api-only} {-IncludeAiSearch} {-IncludeAgents}.
Scaffold AI integration: {search indexing/agent service} as enabled.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Unit'. Update HANDOFF.md and close session.
```

### Resume from HANDOFF

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Resume from currentSubPhase. Load the files listed in Next Load Set. Check Blockers before proceeding.
```

### Add New Entity (Existing Project)

```text
Load .instructions/START-AI.md. No HANDOFF.md needed — this is an add-entity operation.
Add entity {Entity} to the existing solution using .instructions/support/vertical-slice-checklist.md fast-path.
Follow patterns established by {ExistingEntity}.
```

## Validation Cadence

See [support/execution-gates.md](support/execution-gates.md) for the canonical phase gates, commands, and operator setup checklist.
Run `./scripts/preflight-instructions.ps1` before Phase 4 execution and before opening validation PRs.

## Troubleshooting Model

- AI handles code-generation issues in one pass.
- Engineer handles infra/environment issues (feeds, auth, Docker, ports, certs, cloud auth).
- During Phase 4 execution, AI creates `HANDOFF.md` when context is high or at session boundaries.

## Important Guardrails

- Generate code only in the target project, not in this instruction repo.
- Do not over-scaffold optional workloads on first pass.
- Keep context small; load only files needed for the current phase.
- Capture instruction improvements in [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md).

## Core References

- [START-AI.md](START-AI.md) *(AI session bootstrap; load first)*
- [ai/SKILL.md](ai/SKILL.md)
- [ai/domain-specification-schema.md](ai/domain-specification-schema.md) *(Phase 1 output)*
- [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) *(Phase 2 output)*
- [ai/implementation-plan.md](ai/implementation-plan.md) *(Phase 3 template)*
- [support/execution-gates.md](support/execution-gates.md) *(canonical phase checkpoints, validation commands, and operator setup checklist)*
- [support/troubleshooting.md](support/troubleshooting.md) *(triage rules + canonical recurring test failures and fixes)*
- `phase-load-packs.json` *(generated phase load sets)*
- [support/sampleapp-patterns.md](support/sampleapp-patterns.md) *(strictly on-demand for cross-project pattern selection)*
- [support/quick-reference.md](support/quick-reference.md) *(strictly on-demand for naming/DI/config lookups)*

For default phase flow, start with [START-AI.md](START-AI.md) only, then load phase files incrementally.

## Layout

- Root: entry points and machine-readable metadata (`README.md`, `START-AI.md`, `_manifest.json`, `phase-load-packs.json`)
- `ai/`: phase schemas, execution guidance, and AI-loaded base docs
- `support/`: operator checklists, troubleshooting, handoff template, prompt starters, and repo change notes
- `skills/`, `templates/`, `scripts/`, `schemas/`: domain-specific content and validation schemas

## Release Notes

### [1.0] — 2026-04-01 — Initial baseline release

#### Included at baseline

- 4-phase workflow: Domain Discovery → Resource Definition → Implementation Planning → Implementation (4a–4g)
- Manifest-driven token-aware phase loading (`_manifest.json`, `phase-load-packs.json`)
- `modeExclusions` in `_manifest.json` as the source of truth for `full`, `lite`, and `api-only` modes
- Dependency-aware `get-phase-load-set.ps1` with transitive `requires`/`dependencies` resolution, topological ordering, and budget reporting
- `scripts/generate-phase-load-packs.ps1` as the only driver of phase/mode pack generation
- `skills/identity-management.md` placed at canonical `phase-4f`
- `ai/SKILL.md` scoped to Phase 4 execution guidance; prompt starters in README.md
- TaskFlow pattern catalog with composition wiring snippets
- `support/HANDOFF.md` for session state preservation across context boundaries
- Lint checks for manifest load-orchestration invariants
- CI workflow (`instruction-preflight.yml`) validating manifest, load packs, lint, and YAML schema on every push/PR
