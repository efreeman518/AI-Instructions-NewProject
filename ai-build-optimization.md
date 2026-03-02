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
  - document blocker in `HANDOFF.md`
  - point engineer to [engineer-checklist.md](engineer-checklist.md)

## Missing-Inputs Protocol

When domain inputs are absent or ambiguous before scaffolding:

- **Required** (`ProjectName`, `customNugetFeeds`, at least one entity): ask before proceeding
- **Defaults** (modes/profiles/flags): use [resource-implementation-schema.md](resource-implementation-schema.md) **Canonical Defaults**; note assumptions inline and continue
- **Partial entity definitions**: scaffold what is defined; emit `// TODO` stubs for missing properties/rules and list them under Blockers in `HANDOFF.md`

### Phase 3 Pre-Flight: Custom NuGet Feeds

At the **beginning of Phase 3**, before any code generation:

1. Ask: _"Do you have any custom/private NuGet feeds required for this project? If so, provide the feed URL(s) and authentication method (PAT, Azure Artifacts credential provider, etc.)."_
2. Update `nuget.config` with provided feeds, or remove the placeholder if not needed.
3. Run `dotnet restore` as a build checkpoint — must exit 0 before continuing to Phase 4.
4. If restore fails due to feed auth, classify as infrastructure issue and document in `HANDOFF.md`.

## Session Bootstrap

For a new AI session, load [START-AI.md](START-AI.md) first, then load only the phase files required for the active step.

## Strict On-Demand Files

Do not preload these files:

- [quick-reference.md](quick-reference.md)
- [sampleapp-patterns.md](sampleapp-patterns.md)
- [troubleshooting.md](troubleshooting.md)
- [engineer-checklist.md](engineer-checklist.md)

## Prompt Patterns

### Domain discovery (Phase 1)

```text
Help me model this business domain.
Define entities, relationships, lifecycle states, business rules, events, and workflows.
Use business language — no databases, no datatypes, no implementation.
Ask clarifying questions, summarize each iteration, then produce domain-specification YAML.
Write the artifact to `.instructions/domain-specification.yaml` unless another path is explicitly agreed.
```

### Resource definition (Phase 2)

```text
Review the domain definition and help me choose:
- Data stores per entity (SQL, CosmosDB, Table, Blob)
- Datatypes, lengths, precision for EF configuration
- Messaging infrastructure
- Hosting model
Map domain constructs to Aspire/Azure resources.
Write the artifact to `.instructions/resource-implementation.yaml` unless another path is explicitly agreed.
```

### Implementation plan (Phase 3)

```text
Produce implementation-plan.md for this project.
Use domain-specification + resource-implementation schemas.
List ordered steps, open questions, decisions, risks.
```

### Initial scaffold (Phase 4)

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
- Never modify sample-app/
- Never build or compile sample-app/
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

### Composite vertical slice

```text
Add a composite slice for <Feature> spanning entities <A, B, C>.
Use vertical-slice-checklist in composite mode.
Generate all required artifacts across participating entities,
including cross-entity domain rules, handlers, and wiring.
Run build + profile-required tests and report consistency checks.
```

## Validation Cadence

- Foundation/App Core: `dotnet build`
- Feature slice: build + targeted unit, endpoint, and integration tests
- Pre-merge baseline: full test run
- IaC validation: run commands from [engineer-checklist.md](engineer-checklist.md)

## Mixed-Store Slice Gate

For any slice spanning SQL + Cosmos/Table/Blob + messaging, require:

- explicit consistency boundary (authoritative store + projection store)
- reconciliation handler/job with replay-safe correction logic
- drift detection check in post-generation verification

## Phase Loading Manifest

### Session bootstrap
- [START-AI.md](START-AI.md)

### Phase 4 base set
- [SKILL.md](SKILL.md)
- [placeholder-tokens.md](placeholder-tokens.md)
- [ai-build-optimization.md](ai-build-optimization.md)

### Phase 1 — Domain Discovery
- [domain-specification-schema.md](domain-specification-schema.md)
- [domain-design-guide.md](domain-design-guide.md)

### Phase 2 — Resource Definition
- [resource-implementation-schema.md](resource-implementation-schema.md)
- [domain-specification-schema.md](domain-specification-schema.md) *(read-only reference)*

### Phase 3 — Implementation Plan
- [implementation-plan.md](implementation-plan.md)
- [domain-specification-schema.md](domain-specification-schema.md) *(read-only reference)*
- [resource-implementation-schema.md](resource-implementation-schema.md) *(read-only reference)*
- [domain-design-guide.md](domain-design-guide.md) *(optional, for relationship/workflow review)*

### Phase 4a — Foundation
- `skills/solution-structure.md`
- `skills/domain-model.md`
- `skills/data-access.md`
- `skills/package-dependencies.md`
- `templates/entity-template.md`
- `templates/ef-configuration-template.md`
- `templates/repository-template.md`
- `templates/appsettings-template.md`
- relevant sections of [resource-implementation-schema.md](resource-implementation-schema.md)
- `skills/cosmosdb-data.md` / `skills/table-storage.md` / `skills/blob-storage.md` *(if non-SQL entities)*

### Phase 4b — App Core
- `skills/application-layer.md`
- `skills/bootstrapper.md`
- `skills/api.md`
- `templates/dto-template.md`
- `templates/mapper-template.md`
- `templates/service-template.md`
- `templates/endpoint-template.md`
- `templates/message-handler-template.md` *(if events/handlers used)*

### Phase 4c — Runtime/Edge
Load only enabled concerns:
- `skills/gateway.md`
- `skills/aspire.md`
- `skills/configuration.md`
- `skills/multi-tenant.md`
- `skills/caching.md`

### Phase 4d — Optional Hosts
- `skills/background-services.md` (scheduler)
- `skills/function-app.md` (functions)
- `skills/uno-ui.md` (UI; dedicated session preferred)
- `skills/notifications.md` (if enabled)
- UI templates when `includeUnoUI: true`

### Phase 4e — Quality + Delivery
- `skills/testing.md`
- relevant `templates/test-template-*.md`
- `skills/iac.md`
- `skills/cicd.md`

### Phase 4f — Authentication (Final)
- `skills/identity-management.md`
- Replace auth stubs with real provider configuration

## Session State (`HANDOFF.md`)

Create in the target project root during Phase 4 execution when context is high or at natural session boundaries. Not needed during Phases 1-3 (design artifacts handle continuity). See [HANDOFF.md](HANDOFF.md) for template.

Include: current sub-phase, what was completed, build/test status, blockers + owner, files to load next, key decisions.

## Instruction Maintenance

Capture improvements in [UPDATE-INSTRUCTIONS.md](UPDATE-INSTRUCTIONS.md):
- file(s) to update
- current behavior gap
- recommended change
- reason and priority