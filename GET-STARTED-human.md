# Get Started (Human Guide)

Use this guide to run the instruction set with an AI coding assistant in VS Code.

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
3. If useful, add and note in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md)

## Repository Setup

1. Create a new empty app repo.
2. Copy this instruction set into `.instructions/` in that repo.
3. Open the app repo in VS Code.

Expected shape:

```text
<YourApp>/
  .instructions/
    SKILL.md
    domain-inputs.schema.md
    skills/
    templates/
  README.md
```

## Choose Mode + Profiles

Set these early in domain inputs:

- `scaffoldMode`: `full` or `lite`
- `testingProfile`: `minimal|balanced|comprehensive`
- `functionProfile`: `starter|full` (if Functions enabled)
- `unoProfile`: `starter|full` (if Uno UI enabled)

Recommended first pass:

```yaml
scaffoldMode: full
testingProfile: balanced
includeFunctionApp: false
includeUnoUI: false
```

Add optional hosts after core backend slices stabilize.

## Workflow

1. **Domain discovery conversation** with AI (before YAML)
2. Produce YAML inputs using [domain-inputs.schema.md](domain-inputs.schema.md)
3. Scaffold by phase using [SKILL.md](SKILL.md) + [ai-build-optimization.md](ai-build-optimization.md)
4. Validate after each phase (`dotnet build`, then targeted tests)
5. Create `HANDOFF.md` if missing, then update it at phase boundaries

## Domain Discovery Prompt Starter

```text
Help me model this domain before we scaffold code.
I want to define entities, relationships, lifecycle states, business rules,
data stores, tenancy/auth model, and integration events.
Ask clarifying questions, summarize each iteration, then generate YAML inputs.
```

## Scaffolding Prompt Starter

```text
Use .instructions/ and scaffold only this phase.
Inputs:
- ProjectName: <name>
- scaffoldMode: <full|lite>
- testingProfile: <...>
- hosts enabled: <api/gateway/functions/scheduler/ui>
- customNugetFeeds: <...>
- entities: <...>
Constraints:
- Never modify sampleapp/
- Follow placeholder tokens and templates
- Validate with dotnet build after generation
- One code-fix pass max, then flag infra blockers in HANDOFF.md (create if missing)
```

## Validation Cadence

- Phase scaffold: `dotnet build`
- Feature slices: targeted unit, endpoint, and integration tests
- Pre-merge: full test run
- IaC checks: run infra validation commands from [engineer-checklist.md](engineer-checklist.md)

## Troubleshooting Model

- AI handles code-generation issues in one pass.
- Engineer handles infra/environment issues (feeds, auth, Docker, ports, certs, cloud auth).
- AI creates `HANDOFF.md` if needed, then logs blockers with exact next action.

## Core References

- [SKILL.md](SKILL.md)
- [domain-inputs.schema.md](domain-inputs.schema.md)
- [ai-build-optimization.md](ai-build-optimization.md)
- [sampleapp-patterns.md](sampleapp-patterns.md)
- [quick-reference.md](quick-reference.md) *(on-demand during implementation details and naming lookups)*
- [engineer-checklist.md](engineer-checklist.md)
- [troubleshooting.md](troubleshooting.md) *(on-demand when failures occur)*

For default phase flow, load only the minimal set in [SKILL.md](SKILL.md) and pull quick-reference/troubleshooting only when needed.

## Important Guardrails

- `sampleapp/` is reference-only.
- Never build or compile `sampleapp/`; build/test only the new project being scaffolded.
- Do not over-scaffold optional workloads on first pass.
- Keep context small; load only files needed for the current phase.
- Capture instruction improvements in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md).