# New Business Project — C#/.NET Scaffolding Skill

## Purpose

Use this skill set to scaffold new C#/.NET business applications with clean architecture, optional Gateway/Functions/Scheduler/Uno UI, and Azure-ready deployment patterns.

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
- Generate code only in the user's new project directory.
- Use `.slnx` (not legacy `.sln`).
- Use central package management (`Directory.Packages.props`).
- After adding packages, update to latest stable and verify restore/build.
- Record instruction gaps in [../support/UPDATE-INSTRUCTIONS.md](../support/UPDATE-INSTRUCTIONS.md) (do not hot-edit baseline instructions mid-scaffold).
- Prefer latest stable .NET SDK and package releases. MCP server setup: see [../README.md](../README.md).
- All mode/profile/flag defaults come from [resource-implementation-schema.md](resource-implementation-schema.md) (**Canonical Defaults**).

## Context Budget Rules (Mandatory)

1. Load at most **4 skills + 5 templates** per turn.
2. Keep instruction context around **≤30K tokens per phase** (see `_manifest.json` `contextBudget` for model-specific overrides).
3. Use the **Phase Loading Manifest** (below) for per-phase file lists.
4. Unload prior phase docs when transitioning.
5. Keep [../support/quick-reference.md](../support/quick-reference.md), pattern files, and advanced support docs on-demand.
6. Load the phase-relevant pattern file before cross-project wiring: `patterns/data-layer-wiring.md` before 5a/5b, `patterns/api-host-wiring.md` before 5b/5c, `patterns/infrastructure-wiring.md` before 5c/5d.
7. Load only the current sub-phase test templates. `skills/ui-uno.md` remains a dedicated-session file.
8. Checkpoint immediately after **15+ generated files** or **more than 3 build/fix cycles**. Update `HANDOFF.md`; do not wait for the gate.

## Session Start (Every AI Turn)

Follow [../START-AI.md](../START-AI.md) for session bootstrap, version checks, phase routing, and the session-per-phase model. This file does not repeat those steps.

**Each Phase 5 sub-phase runs in its own session.** At session start for Phase 5:
1. Load `SKILL.md` + [placeholder-tokens.md](placeholder-tokens.md) + [tdd-protocol.md](tdd-protocol.md).
2. Read [resource-implementation-schema.md](resource-implementation-schema.md) for `scaffoldMode`, `testingProfile`, host profiles, enabled flags, and canonical defaults.
3. Resolve the current sub-phase load set: `python scripts/get-phase-load-set.py --phase <5x> --mode <mode>`.
4. For compact 5a/5b execution, keep the same sub-phase but optionally resolve a narrower curated slice: `--slice domain|repository|service|endpoint`.
5. Keep only the current sub-phase docs loaded; unload prior sub-phase docs before continuing.
6. For Phase 5a/5b: verify `contractsScaffolded: true` in `HANDOFF.md` — Phase 4 must have completed before TDD begins.

**Session end — after each sub-phase gate passes:**
1. Update `HANDOFF.md` with `currentSubPhase`, gate result, and next load set.
2. Record any blockers, deferred items, and residual environment notes.
3. Close the session. The next session starts from `START-AI.md` + `HANDOFF.md` only.

---

## Mode + Load Resolution

Follow the **Phase Load Resolution** procedure in [../START-AI.md](../START-AI.md). Key points for Phase 5:

- [resource-implementation-schema.md](resource-implementation-schema.md) is the source of truth for `scaffoldMode`, `testingProfile`, enabled capabilities, and defaults.
- For `phase-5a` and `phase-5b`, `--slice` resolves a curated compact bundle inside the current sub-phase without creating a new gate or `HANDOFF.md` state.
- Use `-IncludeAiSearch` and/or `-IncludeAgents` for Phase 5g when only one AI capability is in scope.
- Load only files returned by the script.

## Phase 4 — Contract Scaffolding (Prerequisite for Phase 5)

Phase 4 must finish before any Phase 5 sub-phase starts. It generates the solution structure, interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Gate: `dotnet build` succeeds on the full solution including test projects.

## Phase 5 Sub-Phases (Execution Order)

Each sub-phase is one session. Gate must pass before the next session begins.

**Phase 5a and 5b use TDD:** contracts, entity shells, and test infrastructure already exist from Phase 4. Follow [tdd-protocol.md](tdd-protocol.md): write tests first (red), implement to green. Load the phase-specific test templates alongside production templates.

**Phase 5c and 5d use tests-after:** implement infrastructure/hosts first, then write tests at end of session to verify behavior.

1. **5a — Foundation (TDD):** write domain/rule/repository tests first, then implement entities, EF configs, and repositories. Load `test-templates-domain.md` + `test-templates-repository.md`. Gate: `dotnet build` + `dotnet test --filter "TestCategory=Unit"`.
2. **5b — App Core (TDD):** write service tests, implement services, then write endpoint tests and implement endpoints. Replace no-op DI stubs with real implementations. Load `test-templates-service.md` + `test-templates-endpoint.md`. Gate: `dotnet build` + `dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"`.
3. **5c — Runtime/Edge (tests-after):** load only enabled runtime concerns, then add health/config tests. Gate: `dotnet build` + `dotnet test` + app starts via Aspire.
4. **5d — Optional Hosts (tests-after):** load scheduler, Function App, Uno UI, and notifications only when enabled. Uno UI stays dedicated. Gate: per-host status in `HANDOFF.md` + `dotnet test`.
5. **5e — Quality Gates + Delivery:** add architecture/load/benchmark/CI-CD gates and run full regression. Load `test-templates-quality.md`. Gate: `dotnet test`.
6. **5f — Authentication:** finalize identity last. Use stubs in earlier sub-phases when needed for compile-time flow. Gate: authenticated endpoints respond correctly.
7. **5g — AI Integration:** run only when `includeAiServices: true`, scope further with `-IncludeAiSearch` / `-IncludeAgents`. Gate: search returns results, agent responds to test prompt.

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

## Fail-Fast Protocol

After every build:
- **Code-generation issue** (usings/references/DI/wiring/packages): attempt one focused fix pass, rebuild.
- **Missing package in `Directory.Packages.props`**: add at latest stable version, restore, rebuild.
- **Infrastructure issue** (feed auth, env vars, Docker, certs, SQL/cloud access): do not loop fixes. Document blocker in `HANDOFF.md`, point engineer to [../support/execution-gates.md](../support/execution-gates.md).

## Git Checkpoint Protocol

Cut a git commit after each successful sub-phase gate. This is **not optional** — checkpoints are required for the mid-session rollback protocol in [../support/execution-gates.md](../support/execution-gates.md) to work.

If a sub-phase fails after the one-pass fix attempt:
- isolate the broken changes from the last clean state,
- log the blocker in `HANDOFF.md`,
- continue only with non-blocked work.

## Missing-Inputs Protocol

When domain inputs are absent or ambiguous:
- **Required** (`ProjectName`, `customNugetFeeds`, at least one entity): ask before proceeding.
- **Defaults** (modes/profiles/flags): use [resource-implementation-schema.md](resource-implementation-schema.md) **Canonical Defaults**; note assumptions inline.
- **Partial entity definitions**: scaffold what is defined; emit `// TODO` stubs for missing properties/rules.

### Phase 3 Pre-Flight: Custom NuGet Feeds

At Phase 3 start: ask for custom/private NuGet feed URLs and auth method. Update `nuget.config`, run `dotnet restore`, and require exit code 0 before Phase 4.

## Validation Cadence

Canonical validation gates and commands are defined in [../support/execution-gates.md](../support/execution-gates.md).

## Mixed-Store Slice Gate

For slices spanning SQL + Cosmos/Table/Blob + messaging:
- Explicit consistency boundary (authoritative store + projection store)
- Reconciliation handler/job with replay-safe correction logic
- Drift detection check in post-generation verification

## Session State (`HANDOFF.md`)

Create or update in the target project root at the end of **every** phase and sub-phase session — not only when context is high. Phases 1–3 use it to hand off their output artifacts and open questions. Phase 5 sub-phases use it to record gate results, blockers, and the next load set. See [../support/HANDOFF.md](../support/HANDOFF.md) for the template.

---

## Prompt Catalog

Copy-paste prompts live in [../support/prompt-catalog.md](../support/prompt-catalog.md). Load that file only when starting or resuming a phase; keep it out of the default Phase 5 execution context.


