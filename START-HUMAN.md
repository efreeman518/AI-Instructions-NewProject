# START-HUMAN

Use this guide to run the instruction set with an AI coding assistant in VS Code.

## Quick Start

If you want the shortest path from zero context to first scaffold:

1. Create a new app repo and copy this instruction set into `.instructions/`.
2. Open the app repo in VS Code.
3. Run `./.instructions/scripts/preflight-instructions.ps1` once to validate the copied instruction set.
4. Start Phase 1 with the prompt in [support/prompt-patterns.md](support/prompt-patterns.md) or the Phase 1 prompt below.
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

## Repository Setup

1. Create a new empty app repo.
2. Copy this instruction set into `.instructions/` in that repo.
3. Open the app repo in VS Code.

Expected shape:

```text
<YourApp>/
  .instructions/
    START-HUMAN.md
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
      prompt-patterns.md
      execution-gates.md
      engineer-checklist.md
      troubleshooting.md
    skills/
    scripts/
    templates/
    sample-app/
  README.md
```

Notes:
- `.instructions/support/HANDOFF.md` is the template.
- The working `HANDOFF.md` used to resume AI sessions is created in the target project root during Phase 4.

## Choose Mode + Profiles

Set mode/profile fields early in domain inputs. Allowed values and defaults are canonical in [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) under **Canonical Defaults**.

Add optional hosts after core backend slices stabilize.

## Workflow

1. **Phase 1 — Domain Discovery:** model entities, relationships, events, workflows in business language → [ai/domain-specification-schema.md](ai/domain-specification-schema.md)
2. **Phase 2 — Resource Definition:** map domain to Aspire/Azure resources, datatypes, messaging, hosting → [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md)
3. **Phase 3 — Implementation Plan:** ordered steps, resolve open questions → `implementation-plan.md` in project root
4. **Phase 4 — Implementation:** start AI session with [START-AI.md](START-AI.md), then execute sub-phases (4a-4g) → [ai/SKILL.md](ai/SKILL.md)
5. During Phase 4, create/update `HANDOFF.md` at session boundaries when context is high
6. Run instruction preflight: `./scripts/preflight-instructions.ps1` (refreshes `_manifest.json`, regenerates `phase-load-packs.json`, runs lint)
7. Resolve phase load sets as needed with `./scripts/get-phase-load-set.ps1`

## Phase 1 — Domain Discovery Prompt

```text
Help me model this business domain.
Define entities, relationships, lifecycle states, business rules, events, and workflows.
Use business language — no databases, no datatypes, no implementation.
Ask clarifying questions, summarize each iteration, then produce domain-specification YAML.
Write output to `.instructions/domain-specification.yaml` (or explicitly state a different agreed path).
```

## Phase 2 — Resource Definition Prompt

```text
Review the domain definition and help me choose:
- Data stores per entity (SQL, CosmosDB, Table, Blob)
- Datatypes, lengths, and precision for EF configuration
- Messaging infrastructure (Service Bus, Event Grid, Event Hubs)
- Hosting model (Container Apps, App Service)
- UI hosting if applicable
Map domain constructs to Aspire resources and Azure services.
Write output to `.instructions/resource-implementation.yaml` (or explicitly state a different agreed path).
```

## Scaffolding Prompt Starter

See [support/prompt-patterns.md](support/prompt-patterns.md).

## Validation Cadence

See [support/execution-gates.md](support/execution-gates.md) for the canonical phase gates and commands. IaC checks: [support/engineer-checklist.md](support/engineer-checklist.md).
Run `./scripts/preflight-instructions.ps1` before Phase 4 execution and before opening validation PRs.

## Troubleshooting Model

- AI handles code-generation issues in one pass.
- Engineer handles infra/environment issues (feeds, auth, Docker, ports, certs, cloud auth).
- During Phase 4 execution, AI creates `HANDOFF.md` when context is high or at session boundaries.

## Core References

- [START-AI.md](START-AI.md) *(AI session bootstrap; load first)*
- [ai/SKILL.md](ai/SKILL.md)
- [support/prompt-patterns.md](support/prompt-patterns.md)
- [ai/domain-specification-schema.md](ai/domain-specification-schema.md) *(Phase 1 output)*
- [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) *(Phase 2 output)*
- [ai/implementation-plan.md](ai/implementation-plan.md) *(Phase 3 template)*
- [support/execution-gates.md](support/execution-gates.md) *(canonical phase checkpoints and validation commands)*
- [support/troubleshooting.md](support/troubleshooting.md) *(triage rules + canonical recurring test failures and fixes)*
- `phase-load-packs.json` *(generated phase load sets)*
- [support/sampleapp-patterns.md](support/sampleapp-patterns.md) *(strictly on-demand for cross-project pattern selection)*
- [support/quick-reference.md](support/quick-reference.md) *(strictly on-demand for naming/DI/config lookups)*
- [support/engineer-checklist.md](support/engineer-checklist.md)

For default phase flow, start with [START-AI.md](START-AI.md) only, then load phase files incrementally.

## Important Guardrails

- `sample-app/` is reference-only.
- Never build or compile `sample-app/`; build/test only the new project being scaffolded.
- Do not over-scaffold optional workloads on first pass.
- Keep context small; load only files needed for the current phase.
- Capture instruction improvements in [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md).
