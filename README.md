# AI Instructions — New .NET App/Service

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET business apps and services.

## Start Here

1. **Human setup:** [START-HUMAN.md](START-HUMAN.md) — prerequisites, repo setup, MCP servers
2. **AI session bootstrap:** [START-AI.md](START-AI.md) — load this first, then phase-specific files
3. **Implementation skill map:** [SKILL.md](SKILL.md) — Phase 4 execution, loading manifest, execution rules

## Happy Path

1. Prerequisites and repo setup → [START-HUMAN.md](START-HUMAN.md)
2. Phase 1: Domain YAML → [domain-specification-schema.md](domain-specification-schema.md)
3. Phase 2: Resource YAML → [resource-implementation-schema.md](resource-implementation-schema.md)
4. Phase 3: Implementation plan → [implementation-plan.md](implementation-plan.md)
5. Phase 4: Build → [SKILL.md](SKILL.md)
6. Validate gates → [execution-gates.md](execution-gates.md)
7. Troubleshoot test failures → [test-gotchas.md](test-gotchas.md)

## Mode Selection

| Need | Mode |
|---|---|
| Fast internal tool, minimal infra | `scaffoldMode: lite` |
| Production-ready with optional hosts | `scaffoldMode: full` |

Defaults: [resource-implementation-schema.md](resource-implementation-schema.md) **Canonical Defaults**.
