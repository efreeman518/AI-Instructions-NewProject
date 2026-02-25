# AI Build Optimization Playbook

Use this file for prompt patterns, fail-fast decisions, validation cadence, and context-load details. Workflow and context rules are in [SKILL.md](SKILL.md).

## Fail-Fast Protocol

After every build:

- **Code-generation issue** (usings/references/DI/wiring/packages):
  - attempt one focused fix pass
  - rebuild
- **Missing package in `Directory.Packages.props`** (package reference not found in central props):
  - add the package at latest stable version, then restore and rebuild
  - if it requires a private feed that is not restoring, classify as an infrastructure issue
- **Infrastructure issue** (feed auth, env vars, Docker, certs, SQL/cloud access):
  - do not loop fixes
  - document blocker in `HANDOFF.md` (create if missing)
  - point engineer to [engineer-checklist.md](engineer-checklist.md)

## Missing-Inputs Protocol

When domain inputs are absent or ambiguous before scaffolding:

- **Required** (`ProjectName`, `customNugetFeeds`, at least one entity): ask before proceeding
- **Mode/profile defaults** (`scaffoldMode`, `testingProfile`): infer `full` / `balanced`; note the assumption inline and continue
- **Optional feature flags** (`includeGateway`, `includeFunctionApp`, `includeUnoUI`, etc.): default to `false`; note and continue
- **Partial entity definitions**: scaffold what is defined; emit `// TODO` stubs for missing properties/rules and list them under Blockers in `HANDOFF.md`

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
- Never build or compile sampleapp/
- Use templates + placeholder tokens
- Build after generation
- One code-fix pass max; infra blockers go to HANDOFF.md (create if missing)
```

### Fix-only

```text
Fix only current build/test failures.
No unrelated refactors.
Preserve public contracts unless required.
Re-run same validation command.
If still failing after one pass, log in HANDOFF.md (create if missing).
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
- Feature slice: build + targeted unit, endpoint, and integration tests
- Pre-merge baseline: full test run
- IaC validation: run commands from [engineer-checklist.md](engineer-checklist.md)

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
- `templates/domain-rules-template.md` *(if rules/state machine used)*
- `templates/appsettings-template.md`
- relevant sections of [domain-inputs.schema.md](domain-inputs.schema.md)
- `skills/cosmosdb-data.md` / `skills/table-storage.md` / `skills/blob-storage.md` *(if non-SQL entities)*

### Phase 2 — App Core
- `skills/application-layer.md`
- `skills/bootstrapper.md`
- `skills/api.md`
- `templates/dto-template.md`
- `templates/mapper-template.md`
- `templates/service-template.md`
- `templates/endpoint-template.md`
- `templates/message-handler-template.md` *(if events/handlers used)*

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

## Session State (`HANDOFF.md`, create/update)

Create this file in the target project root if it does not exist, then update it at phase boundaries or when context is high.

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