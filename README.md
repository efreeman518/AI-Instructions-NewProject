# AI Instructions — New .NET App/Service

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET business apps and services.

## Who This Is For

- Engineers using AI coding assistants to scaffold a new .NET solution
- Teams that want repeatable architecture guardrails and phased delivery
- Projects that may include API, Gateway, Scheduler, Functions, Uno UI, Aspire, IaC, and CI/CD

## Start Here

1. Human setup and prerequisites: [START-HUMAN.md](START-HUMAN.md)
2. AI session bootstrap (minimal load): [START-AI.md](START-AI.md)
3. Full implementation skill map: [SKILL.md](SKILL.md)

## Happy Path (6 Steps)

1. Complete prerequisites and repository setup from [START-HUMAN.md](START-HUMAN.md)
2. Run Phase 1 and produce domain YAML via [domain-specification-schema.md](domain-specification-schema.md)
3. Run Phase 2 and produce resource YAML via [resource-implementation-schema.md](resource-implementation-schema.md)
4. Build Phase 3 plan via [implementation-plan.md](implementation-plan.md)
5. Execute Phase 4 implementation using [SKILL.md](SKILL.md) and [ai-build-optimization.md](ai-build-optimization.md)
6. Validate each phase and track session continuity with [engineer-checklist.md](engineer-checklist.md) and [HANDOFF.md](HANDOFF.md)

## Lite vs Full Chooser

| If you need... | Choose |
|---|---|
| Fast internal tool, minimal infrastructure, lowest complexity | `scaffoldMode: lite` |
| Production-ready platform shape with optional hosts and delivery assets | `scaffoldMode: full` |

Default values and profiles are canonical in [resource-implementation-schema.md](resource-implementation-schema.md) under **Canonical Defaults**.

## Context Discipline

- Start new AI sessions with [START-AI.md](START-AI.md) only
- Keep [quick-reference.md](quick-reference.md) and [sampleapp-patterns.md](sampleapp-patterns.md) strictly on-demand
- Treat `sample-app/` as read-only reference
