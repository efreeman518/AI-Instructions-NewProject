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

- `sample-app/` is **read-only reference**; never edit/build/delete files there.
- Load [../support/sampleapp-patterns.md](../support/sampleapp-patterns.md) only when selecting cross-project patterns or before opening raw `sample-app/src/` files. Do not preload it for routine scaffolding.
- Generate code only in the user's new project directory.
- Use `.slnx` (not legacy `.sln`).
- Use central package management (`Directory.Packages.props`).
- After adding packages, update to latest stable and verify restore/build.
- Record instruction gaps in [../support/UPDATE-INSTRUCTIONS.md](../support/UPDATE-INSTRUCTIONS.md) (do not hot-edit baseline instructions mid-scaffold).
- Prefer latest stable .NET SDK and package releases. MCP server setup: see [../START-HUMAN.md](../START-HUMAN.md).
- All mode/profile/flag defaults come from [resource-implementation-schema.md](resource-implementation-schema.md) (**Canonical Defaults**).

## Context Budget Rules (Mandatory)

1. Load at most **4 skills + 5 templates** per turn.
2. Keep instruction context around **≤30K tokens per phase** (see `_manifest.json` `contextBudget` for model-specific overrides).
3. Use the **Phase Loading Manifest** (below) for per-phase file lists.
4. Unload prior phase docs when transitioning.
5. Do not preload [../support/quick-reference.md](../support/quick-reference.md) or [../support/sampleapp-patterns.md](../support/sampleapp-patterns.md); load only when needed.
6. Use [../support/sampleapp-patterns.md](../support/sampleapp-patterns.md) before opening any raw sampleapp source.
7. Large files must be loaded selectively:
   - `templates/test-template-*.md`: only needed test type
   - [skills/uno-ui.md](../skills/uno-ui.md): dedicated session preferred
8. When context is high during execution, create/update `HANDOFF.md` with resume instructions for next session.

## Session Start (Every AI Turn)

Follow [../START-AI.md](../START-AI.md) for session bootstrap, version checks, phase routing, and the session-per-phase model. This file does not repeat those steps.

**Each Phase 4 sub-phase runs in its own session.** At session start for Phase 4:
1. Load `SKILL.md` + [placeholder-tokens.md](placeholder-tokens.md).
2. Read [resource-implementation-schema.md](resource-implementation-schema.md) for `scaffoldMode`, `testingProfile`, host profiles, enabled flags, and canonical defaults.
3. Resolve the current sub-phase load set: `./scripts/get-phase-load-set.ps1 -Phase <4x> -Mode <mode>`.
4. Keep only the current sub-phase docs loaded; unload prior sub-phase docs before continuing.

**Session end — after each sub-phase gate passes:**
1. Update `HANDOFF.md` with `currentSubPhase`, gate result, and next load set.
2. Record any blockers, deferred items, and residual environment notes.
3. Close the session. The next session starts from `START-AI.md` + `HANDOFF.md` only.

---

## Mode + Load Resolution

- [resource-implementation-schema.md](resource-implementation-schema.md) is the source of truth for `scaffoldMode`, `testingProfile`, enabled capabilities, and defaults.
- `_manifest.json` and `phase-load-packs.json` are the source of truth for phase membership, dependencies, and mode exclusions.
- Do not infer current load sets from prose in this file.

Primary path:

1. Run `./scripts/generate-phase-load-packs.ps1` when `_manifest.json` changes.
2. Resolve the current load set with `./scripts/get-phase-load-set.ps1 -Phase <phase> -Mode <full|lite|api-only> [feature flags]`.
3. Use `-IncludeAiSearch` and/or `-IncludeAgents` for Phase 4g when only one AI capability is in scope.
4. Load only files returned by the script.

## Phase 4 Sub-Phases (Execution Order)

Each sub-phase is one session. Gate must pass before the next session begins.

1. **4a — Foundation:** solution structure, domain model, data access, packages, and core entity/config/repository/appsettings templates. Add non-SQL store skills only when the slice uses them. Gate: `dotnet build`.
2. **4b — App Core:** application layer, bootstrapper, API, and the DTO/mapper/service/endpoint validator templates. Add message handlers only when events exist. Gate: `dotnet build` + endpoint tests.
3. **4c — Runtime/Edge:** load only enabled gateway, aspire, configuration, multi-tenant, caching, observability, and security concerns. Gate: `dotnet build` + app starts via Aspire.
4. **4d — Optional Hosts:** load scheduler, Function App, Uno UI, and notifications only when enabled. Uno UI is a dedicated session on its own. Gate: per-host status recorded in `HANDOFF.md` `hostGates`.
5. **4e — Quality + Delivery:** testing plus only the test templates in scope. Add IaC, CI/CD, and optional integrations when they are part of the current milestone. Gate: full test suite passes.
6. **4f — Authentication:** finalize identity last. Use stubs in earlier sub-phases when needed for compile-time flow. Gate: authenticated endpoints respond correctly.
7. **4g — AI Integration:** run only when `includeAiServices: true`, scope further with `-IncludeAiSearch` / `-IncludeAgents`. Gate: search returns results, agent responds to test prompt.

## Template Usage

Use templates for generated artifacts and keep naming aligned with [placeholder-tokens.md](placeholder-tokens.md).

- Backend templates: entity/config/repository/dto/mapper/service/endpoint/rules/message-handler/structure-validator/exception-handler
- UI templates: MVUX model/XAML page/UI model/UI service
- Tests: load only needed file from `templates/test-template-*.md`

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
- **Every external dependency must declare one scaffold-time mode** before Phase 4 code is generated for it. Valid modes:
  - `emulator` — Aspire-hosted or local emulator available (SQL, Redis, Azure Storage Emulator, Service Bus emulator)
  - `lazy-optional` — config-driven; service activates only when config section is present/non-empty; absent = no-op passthrough
  - `no-op stub` — compile-time stub that satisfies the interface and returns safe defaults; no cloud call made
  - `deployment-only` — live integration deferred to deployment; **a no-op stub must still be generated** so the solution compiles and runs locally. Stub must satisfy the interface, return safe defaults, and carry a `// TODO: [CONFIGURE]` comment. Blocker logged in `HANDOFF.md`.
- **Scaffold is complete when: solution builds, unit/endpoint tests pass, and the app boots end-to-end without any manual cloud setup.** Manual cloud provisioning (Entra, Key Vault, Foundry, ACS) must use `lazy-optional` or `no-op stub` mode and cannot block scaffold completion.
- No commercial-licensed test packages. Use MSTest built-in assertions as the baseline. See [../skills/testing.md](../skills/testing.md) for approved assertion options.
- Keep Aspire config and IaC names aligned
- Start with minimal viable profiles, promote later

---

## Fail-Fast Protocol

After every build:
- **Code-generation issue** (usings/references/DI/wiring/packages): attempt one focused fix pass, rebuild.
- **Missing package in `Directory.Packages.props`**: add at latest stable version, restore, rebuild.
- **Infrastructure issue** (feed auth, env vars, Docker, certs, SQL/cloud access): do not loop fixes. Document blocker in `HANDOFF.md`, point engineer to [../support/engineer-checklist.md](../support/engineer-checklist.md).

## Git Checkpoint Protocol

If the engineer wants source-control checkpoints, cut one clean checkpoint after each successful sub-phase.

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

Create or update in the target project root at the end of **every** phase and sub-phase session — not only when context is high. Phases 1–3 use it to hand off their output artifacts and open questions. Phase 4 sub-phases use it to record gate results, blockers, and the next load set. See [../support/HANDOFF.md](../support/HANDOFF.md) for the template.

---

## Prompt Starters

Copy-paste prompt starters live in [../support/prompt-patterns.md](../support/prompt-patterns.md). Load them only when starting or resuming a phase, not as part of the default Phase 4 base context.


