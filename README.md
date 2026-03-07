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
Implementation knowledge is split into ~30 skill files (how things work) and ~24 template files (what to generate). An AI token manifest (`_manifest.json`) tracks estimated token costs, phase membership, and mode exclusions so the assistant loads only what's needed per phase, staying within context budgets. Templates carry `generates` and `requires` metadata, and the load-set resolver expands transitive dependencies automatically.

**3. A reference implementation, not documentation alone.**
A complete sample application (`sample-app/`) demonstrates every pattern in working code. The AI is instructed to read patterns from it — never to edit it. This grounds the generated output in real, compilable examples rather than abstract descriptions.

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
| **Reference sample app** | A complete TaskFlow application demonstrates all patterns in compilable code — entities, repositories, services, endpoints, Aspire orchestration, and tests. |
| **Prompt patterns** | Copy-paste prompts for each phase let engineers kick off AI sessions without memorizing the workflow. |

## Start Here

1. **Human setup:** [START-HUMAN.md](START-HUMAN.md) — prerequisites, repo setup, MCP servers
2. **AI session bootstrap:** [START-AI.md](START-AI.md) — load this first, then phase-specific files
3. **Implementation skill map:** [ai/SKILL.md](ai/SKILL.md) — Phase 4 execution, loading manifest, execution rules
4. **Prompt starters:** [support/prompt-patterns.md](support/prompt-patterns.md) — copy-paste prompts for phase kickoff and resume

## Layout

- Root: entry points and machine-readable metadata (`README.md`, `START-AI.md`, `START-HUMAN.md`, `_manifest.json`, `phase-load-packs.json`)
- `ai/`: phase schemas, execution guidance, and AI-loaded base docs
- `support/`: operator checklists, troubleshooting, handoff template, prompt starters, and repo change notes
- `skills/`, `templates/`, `scripts/`, `sample-app/`: unchanged domain-specific buckets

## Happy Path

1. Prerequisites and repo setup → [START-HUMAN.md](START-HUMAN.md)
2. Phase 1: Domain YAML → [ai/domain-specification-schema.md](ai/domain-specification-schema.md)
3. Phase 2: Resource YAML → [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md)
4. Phase 3: Implementation plan → [ai/implementation-plan.md](ai/implementation-plan.md)
5. Phase 4: Build → [ai/SKILL.md](ai/SKILL.md)
6. Validate gates → [support/execution-gates.md](support/execution-gates.md)
7. Troubleshoot → [support/troubleshooting.md](support/troubleshooting.md)

## Mode Selection

| Need | Mode |
|---|---|
| Fast internal tool, minimal infra | `scaffoldMode: lite` |
| Production-ready with optional hosts | `scaffoldMode: full` |
| Single API, no gateway/UI/scheduler | `scaffoldMode: api-only` |

Defaults: [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) **Canonical Defaults**.

