# AI Build Optimization Playbook

Use this file to keep scaffolding deterministic, context-efficient, and verifiable.

## Core Rules

- Execute one phase at a time.
- Keep instruction context small (target `≤30K` per phase).
- Validate immediately after generation (`dotnet build` first).
- One code-fix pass per failure set; then flag blockers for engineer.
- Never modify/build `sampleapp/`.

## High-Confidence Workflow

1. Domain discovery conversation
2. Capture YAML inputs from [domain-inputs.schema.md](domain-inputs.schema.md)
3. Choose mode/profiles (`scaffoldMode`, `testingProfile`, optional `functionProfile`/`unoProfile`)
4. Run one phase using only required skills/templates
5. Validate + checkpoint in `HANDOFF.md`
6. Move to next phase

## Fail-Fast Protocol

After every build:

- **Code-generation issue** (usings/references/DI/wiring/packages):
  - attempt one focused fix pass
  - rebuild
- **Infrastructure issue** (feed auth, env vars, Docker, certs, SQL/cloud access):
  - do not loop fixes
  - document blocker in `HANDOFF.md`
  - point engineer to [engineer-checklist.md](engineer-checklist.md)

## Prompt Patterns

### Domain discovery

```text
Before scaffolding, help me model the domain:
- entities/lifecycle
- relationships/boundaries
- business rules
- data store fit
- tenancy/auth/events
Then summarize and generate YAML inputs.
```

### Initial scaffold

```text
Use .instructions/.
Execute only this phase:
- <list of skills>
Inputs:
- ProjectName, scaffoldMode, testingProfile
- enabled hosts
- customNugetFeeds
- entities
Constraints:
- Never modify sampleapp/
- Use templates + placeholder tokens
- Build after generation
- One code-fix pass max; infra blockers go to HANDOFF.md
```

### Fix-only

```text
Fix only current build/test failures.
No unrelated refactors.
Preserve public contracts unless required.
Re-run same validation command.
If still failing after one pass, log in HANDOFF.md.
```

### Vertical slice

```text
Add entity <Entity> end-to-end using templates + vertical-slice-checklist.
Generate all required artifacts and DI wiring.
Build and run targeted tests.
Report generated files + follow-ups.
```

## Validation Cadence

- Foundation/App Core: `dotnet build`
- Feature slice: build + targeted unit/endpoint/integration tests
- Pre-merge baseline: full test run
- IaC validation: run commands from [engineer-checklist.md](engineer-checklist.md)

## Context Control

- Load only active phase files.
- Prefer references/diffs over large pasted content.
- Use [sampleapp-patterns.md](sampleapp-patterns.md) before raw sampleapp files.
- Use MCP servers for up-to-date APIs/packages.

## Phase Loading Manifest

### Always available (small)
- [SKILL.md](SKILL.md)
- [placeholder-tokens.md](placeholder-tokens.md)
- [ai-build-optimization.md](ai-build-optimization.md)

### Phase 1 — Foundation
- `skills/solution-structure.md`
- `skills/domain-model.md`
- `skills/data-access.md`
- `skills/package-dependencies.md`
- `templates/entity-template.md`
- `templates/ef-configuration-template.md`
- `templates/repository-template.md`
- relevant sections of [domain-inputs.schema.md](domain-inputs.schema.md)

### Phase 2 — App Core
- `skills/application-layer.md`
- `skills/bootstrapper.md`
- `skills/api.md`
- `templates/dto-template.md`
- `templates/mapper-template.md`
- `templates/service-template.md`
- `templates/endpoint-template.md`

### Phase 3 — Runtime/Edge
Load only enabled concerns:
- `skills/gateway.md`
- `skills/aspire.md`
- `skills/configuration.md`
- `skills/multi-tenant.md`
- `skills/caching.md`

### Phase 4 — Optional Hosts
- `skills/background-services.md` (scheduler)
- `skills/function-app.md` (functions)
- `skills/uno-ui.md` (UI; dedicated session preferred)
- `skills/notifications.md` (if enabled)
- UI templates when `includeUnoUI: true`

### Phase 5 — Quality + Delivery
- `skills/testing.md`
- relevant `templates/test-template-*.md`
- `skills/identity-management.md`
- `skills/iac.md`
- `skills/cicd.md`

## Session Handoff (`HANDOFF.md`)

Update at phase boundaries or when context is high.

Include:
- completed phases
- domain input summary
- last build/test status
- blockers + owner (AI vs engineer)
- next phase + exact files to load
- key decisions/deviations

## Instruction Maintenance

Capture improvements in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md):
- file(s) to update
- current behavior gap
- recommended change
- reason and priority