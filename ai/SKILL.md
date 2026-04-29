# New Business Project — C#/.NET Scaffolding Skill

## Purpose

Use this skill set to scaffold new C#/.NET business applications with clean architecture, optional Gateway/Functions/Scheduler/Uno UI, and Azure-ready deployment patterns.

## Phase 5 Decision Table

Fast-lookup answer to "what do I do next?" — refer to this before scrolling for prose.

| Situation | Action |
|---|---|
| Build green after a sub-phase | Move to next sub-phase. Update `HANDOFF.md`, close session. |
| Build red, fixable in one focused pass | Fix, rebuild. If still red, stop and write `HANDOFF.md`. |
| Build red after one fix attempt | Write `HANDOFF.md` with the blocker. Do not loop. |
| Domain assumption looks wrong (entity shape, relationship) | Stop. Confirm with developer before continuing. See **Mid-Session Rollback Protocol** below. |
| External dependency cannot be stubbed locally | Mark as `deployment-only` in `resource-implementation.yaml`, generate a no-op stub anyway, log blocker in `HANDOFF.md`, continue. |
| Sub-phase has produced 15+ generated files or 3+ build cycles | Checkpoint `HANDOFF.md` mid-session. Do not wait for the gate. |
| Multiple files touched by a single structural error | Don't patch-fix. Roll back, log, re-scaffold the slice. |
| Missing required input (`ProjectName`, custom NuGet feed, at least one entity) | Ask developer before proceeding. |
| Missing optional input (mode/profile/flag default) | Apply canonical default from [resource-implementation-schema.md](resource-implementation-schema.md), state assumption inline, record in `HANDOFF.md`. |

Detail sections (Fail-Fast Protocol, Git Checkpoint Protocol, Missing-Inputs Protocol, Mid-Session Rollback Protocol, Mixed-Store Slice Gate) live below — this table is the index.

## When to Use

Use for:
- New solution scaffolding
- Adding full vertical slices (entity → data → app → API → tests)
- Adding optional workloads (Gateway, Functions, Scheduler, Uno UI)
- Preparing config/auth/infra/IaC/CI-CD foundations

## Non-Negotiables

- **Conflict resolution order:** `support/execution-gates.md` > this file (`ai/SKILL.md`) > individual skill files > templates.
- Load pattern files from `patterns/` only when needed for cross-project wiring. Use [../support/pattern-dispatcher.md](../support/pattern-dispatcher.md) as the index to find the right file.
- **Load [../support/ef-packages-reference.md](../support/ef-packages-reference.md) before Phase 5a** to know which base types (DbContextBase, DomainResult, IRequestContext, etc.) come from the EF.Packages private feed. Do not regenerate these types.
- **Reference app — TaskFlow.** When a skill or template is ambiguous, consult it. Use [../support/taskflow-proof-map.md](../support/taskflow-proof-map.md) for the phase → area index. Reference application rules (local sibling preference, do-not-copy-wholesale) live in [../START-AI.md](../START-AI.md) § Reference Application.
- Generate code only in the user's new project directory.
- Use `.slnx` (not legacy `.sln`).
- Use central package management (`Directory.Packages.props`).
- After adding packages, update to latest stable and verify restore/build.
- Record instruction gaps in `INSTRUCTION-GAPS.md` at the target project root (do not hot-edit installed `.instructions/` files mid-scaffold). Instruction maintainers can later copy approved findings into [../support/UPDATE-INSTRUCTIONS.md](../support/UPDATE-INSTRUCTIONS.md).
- Prefer latest stable .NET SDK and package releases. MCP server setup: see [../README.md](../README.md).
- All mode/profile/flag defaults come from [resource-implementation-schema.md](resource-implementation-schema.md) (**Canonical Defaults**).

## Phase 5 file table

Each Phase 5 sub-phase loads its own file set. The base context (`ai/SKILL.md`, `ai/placeholder-tokens.md`, `ai/tdd-protocol.md`, `support/ef-packages-reference.md`) is always loaded.

| Sub-phase | Required skills | Required templates | On-demand |
|---|---|---|---|
| **5a Foundation (TDD)** | `domain-model`, `data-persistence` | `entity`, `ef-configuration`, `repository`, `domain-rules`, `appsettings`, `test-templates-domain`, `test-templates-repository` | `azure-data-storage`, `updater-template` (non-SQL stores); `patterns/data-layer-wiring` (cross-project wiring) |
| **5b App Core (TDD for app/API, tests-after for runtime)** | `application-layer`, `bootstrapper`, `api`, plus enabled runtime concerns: `gateway`, `multi-tenant`, `caching`, `aspire`, `configuration-secrets`, `observability`, `security` | `data-mapping`, `service`, `endpoint`, `message-handler`, `structure-validator`, `exception-handler`, `test-templates-service`, `test-templates-endpoint`, `health-check` | `patterns/api-host-wiring`, `patterns/infrastructure-wiring` |
| **5c Optional Hosts (tests-after)** | only the enabled host(s): `background-services`, `function-app`, `ui-uno`, `ui-blazor`, `notifications` | host-matching templates: `uno-mvux-model`, `uno-ui-client-layer`, `uno-xaml-page` | `ui-uno` is a dedicated-session file |
| **5d Quality + Delivery** | `testing`, `iac`, `cicd` | `test-templates-quality`, `dockerfile` | `messaging`, `grpc`, `external-api` (if used) |
| **5e Integration (Auth + AI)** | `identity-management` (always); `ai-integration` (when `includeAiServices: true`) | `ai-search`, `agent` (when AI in scope) | scope AI further to search-only or agents-only as needed |

Read the table once at the start of each Phase 5 sub-phase session, load the listed files, proceed.

> **Phase 5 was consolidated from seven sub-phases (5a–5g) to five.** Old `5c` (Runtime/Edge) merged into `5b` (App Core). Old `5d` (Optional Hosts) → new `5c`. Old `5e` (Quality + Delivery) → new `5d`. Old `5f` (Auth) and `5g` (AI) merged into new `5e` (Integration). HANDOFF.md `currentSubPhase` values from before this change should be remapped: `5c → 5b`, `5d → 5c`, `5e → 5d`, `5f → 5e`, `5g → 5e`.

## Session Start (Every AI Turn)

Follow [../START-AI.md](../START-AI.md) for session bootstrap, version checks, phase routing, and the session-per-phase model. This file does not repeat those steps.

**Developer Clarification Rule:** Before generating code or configuration in any phase, ask for required or unsafe-missing inputs. For values covered by canonical defaults, apply the default, state the assumption, and record it in `HANDOFF.md`.

**Shared Understanding Rule:** In Phase 1, run [shared-understanding-interview.md](shared-understanding-interview.md) before finalizing artifacts. Produce `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md`. Later phases must preserve accepted domain terms and respect decision dependencies unless the developer explicitly revises them.

**Each Phase 5 sub-phase runs in its own session.** At session start for Phase 5:
1. Load `SKILL.md` + [placeholder-tokens.md](placeholder-tokens.md) + [tdd-protocol.md](tdd-protocol.md).
2. Read [resource-implementation-schema.md](resource-implementation-schema.md) for `scaffoldMode`, `testingProfile`, host profiles, enabled flags, and canonical defaults.
3. Look up the current sub-phase row in the **Phase 5 file table** above and load its required files. Add on-demand files only when the current sub-phase clearly needs them.
4. For Phase 5a/5b: verify `contractsScaffolded: true` in `HANDOFF.md` — Phase 4 must have completed before TDD begins.

**Session end — after each sub-phase gate passes:**
1. Update `HANDOFF.md` with `currentSubPhase`, gate result, and next load set.
2. Record any blockers, deferred items, and residual environment notes.
3. Close the session. The next session starts from `START-AI.md` + `HANDOFF.md` only.

---

## Phase 4 — Contract Scaffolding (Prerequisite for Phase 5)

Phase 4 must finish before any Phase 5 sub-phase starts. It generates the solution structure, interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Gate: `dotnet build` succeeds on the full solution including test projects.

## Phase 5 Sub-Phases (Execution Order)

Each sub-phase is one session. Gate must pass before the next session begins.

**Phase 5a uses TDD; 5b is mixed mode (TDD for application/API, tests-after for runtime concerns); 5c–5e use tests-after.** Contracts, entity shells, and test infrastructure already exist from Phase 4. Follow [tdd-protocol.md](tdd-protocol.md) where TDD applies: write tests first (red), implement to green.

1. **5a — Foundation (TDD):**
   - **Ask clarification questions first:** domain rule specifics, invariant constraints, inheritance patterns, special validations, audit/versioning needs
   - Write domain/rule/repository tests first, then implement entities, EF configs, and repositories. Load `test-templates-domain.md` + `test-templates-repository.md`. Gate: `dotnet build` + `dotnet test --filter "TestCategory=Unit"`.

2. **5b — App Core + Runtime/Edge (TDD for app/API, tests-after for runtime):**
   - **Ask clarification questions first:** service business logic, API pagination/filtering, response formats, error handling, idempotency. Plus runtime concerns: observability/tracing, health checks, rate limiting, caching, gateway routing.
   - Write service tests → implement services. Write endpoint tests → implement endpoints. Replace no-op DI stubs with real implementations. Then add enabled runtime concerns (gateway, caching, observability, security, multi-tenant) followed by their tests. Load `test-templates-service.md` + `test-templates-endpoint.md` + runtime skill files. Gate: `dotnet build` + `dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"` + app starts via Aspire (when Aspire is enabled).

3. **5c — Optional Hosts (tests-after):**
   - **Ask clarification questions first:** for each enabled host, ask host-specific details (Function App triggers/bindings/outputs, Scheduler job types/schedules, Notification channels/templates, Uno UI target platforms/responsive needs)
   - Load only the host-specific files matching the enabled hosts in `resource-implementation.yaml`. Uno UI stays a dedicated session. Gate: per-host status in `HANDOFF.md` + `dotnet test`.

4. **5d — Quality + Delivery:**
   - **Ask clarification questions first:** code quality thresholds, load test requirements, benchmark baselines, CI-CD pipeline specifics
   - Add architecture/load/benchmark/CI-CD gates and run full regression. Load `test-templates-quality.md`. Gate: `dotnet test`.

5. **5e — Integration (Auth + AI):**
   - **Ask clarification questions first:** authentication provider, custom claims/roles, B2B vs B2C, token expiry. If AI in scope: AI search scope/filters, agent capabilities, content ingestion, cost/latency.
   - Finalize identity (replace earlier stubs with config-driven scaffold principal). When `includeAiServices: true`, scaffold AI search and/or agents — load only the templates matching the enabled capability. Gate: authenticated endpoints respond correctly; if AI enabled, search returns results and agent responds to test prompt.

## Template Usage

Use templates for generated artifacts and keep naming aligned with [placeholder-tokens.md](placeholder-tokens.md).

- Backend templates: entity/config/repository/dto/mapper/service/endpoint/rules/message-handler/structure-validator/exception-handler
- UI templates: MVUX model/XAML page/UI model/UI service
- Tests: load only the phase-specific split test template for the current sub-phase; use `templates/test-templates.md` only as an on-demand reference

## Vertical Slice Shortcut

For an existing solution, use:
- [../support/vertical-slice-checklist.md](../support/vertical-slice-checklist.md)
- Relevant `templates/`

Generate one complete slice, validate, then move to next slice.

## Key Principles

- Clean architecture boundaries
- Bootstrapper-based shared DI wiring
- Static mappers + EF-safe projectors
- `DomainResult`-style railway flow
- Tenant-safe defaults where enabled
- SQL defaults: `nvarchar(N)`, `decimal(10,4)`, `datetime2`
- Stub external dependencies for local compile/run — generate compilable no-op implementations with `// TODO: [CONFIGURE]` comments at every integration point (stub class, DI registration, appsettings section)
- **Every external dependency must declare one scaffold-time mode** before Phase 5 code is generated for it. Valid modes:
  - `emulator` — Aspire-hosted or local emulator available (SQL, Redis, Azure Storage Emulator, Service Bus emulator)
  - `lazy-optional` — config-driven; service activates only when config section is present/non-empty; absent = no-op passthrough
  - `no-op stub` — compile-time stub that satisfies the interface and returns safe defaults; no cloud call made
  - `deployment-only` — live integration deferred to deployment; **a no-op stub must still be generated** so the solution compiles and runs locally. Stub must satisfy the interface, return safe defaults, and carry a `// TODO: [CONFIGURE]` comment. Blocker logged in `HANDOFF.md`.
- **Schema ownership for third-party operational stores:** When a dependency (scheduler, queue dashboard, job runner, etc.) persists data through its own EF-backed or SQL-backed operational tables, determine **who owns schema creation** before writing startup code. Check for: (a) library-managed auto-create (e.g., `EnsureCreated` at startup), (b) library-provided migrations or SQL scripts you must run, (c) a design-time factory the library expects in your project, or (d) manual migration ownership where you must add `DbSet`/configurations for the library's tables. Record the answer in `resource-implementation.yaml` under `externalDependencyModes`. Do not assume a library will create its tables just because its startup code runs without error — missing schema often surfaces as seeding or runtime failures, not startup crashes.
- **Scaffold is complete when: solution builds, unit/endpoint tests pass, and the app boots end-to-end without any manual cloud setup.** Manual cloud provisioning (Entra, Key Vault, Foundry, ACS) must use `lazy-optional` or `no-op stub` mode and cannot block scaffold completion.
- No commercial-licensed test packages. Use MSTest built-in assertions as the baseline. See [../skills/testing.md](../skills/testing.md) for approved assertion options.
- Keep Aspire config and IaC names aligned
- Start with minimal viable profiles, promote later
- **Seed deterministic startup data early.** For scaffold, demo, or local-development modes, generate seed data for the minimum required user/profile/domain state so that first-run UI and API flows work against real records. Do not spend time hardening first-run UX around empty datasets — seed first, polish later.

---

## Operational Protocols

Fail-Fast, Git Checkpoint, Missing-Inputs, Mid-Session Rollback, Mixed-Store Slice Gate, and Context Budgets all live in a single source of truth: [../support/OPERATIONS.md](../support/OPERATIONS.md). Read it when something fails, when state changes, or when an assumption breaks. The Phase 5 **Decision Table** at the top of this file is the fast-lookup index for what to do; OPERATIONS.md is the detail.

## Validation Cadence

Canonical validation gates and commands are defined in [../support/execution-gates.md](../support/execution-gates.md).

## Session State (`HANDOFF.md`)

Create or update in the target project root at the end of **every** phase and sub-phase session — not only when context is high. Phases 1–3 use it to hand off their output artifacts and open questions. Phase 5 sub-phases use it to record gate results, blockers, and the next load set. See [../support/HANDOFF.md](../support/HANDOFF.md) for the template.

---

## Prompt Catalog

Copy-paste prompts live in [../support/prompt-catalog.md](../support/prompt-catalog.md). Load that file only when starting or resuming a phase; keep it out of the default Phase 5 execution context.

## Context budgets

See [../support/OPERATIONS.md](../support/OPERATIONS.md) § Context Budgets.


