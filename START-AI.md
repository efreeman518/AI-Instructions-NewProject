# START-AI

This file is the canonical scaffold bootstrap for installed `.instructions/` payloads and for this instruction repository. Keep harness entrypoints (`AGENTS.md`, `.claude/commands/*`, `.github/agents/*`, `CLAUDE.md`, `.github/copilot-instructions.md`) thin and scoped; they point here but do not duplicate phase routing.

Load this file first when scaffold workflow has been explicitly requested. Do not preload the full instruction set.

## Harness Adapter Rule

Use this rule before the Session Start Router when arriving through a harness-specific entrypoint:

- **CLI agents via `AGENTS.md`:** proceed only when the user explicitly asks to scaffold, continue a phase, or add a vertical slice. For normal coding/review/docs tasks, ignore scaffold phase rules.
- **GitHub Copilot agents:** `dotnet-scaffold` runs the full phase router; `vertical-slice` loads `support/vertical-slice-checklist.md` fast-path.
- **Claude commands/extensions:** `/scaffold` runs the full phase router; `/vertical-slice` loads `support/vertical-slice-checklist.md` fast-path. If slash commands are unavailable, the user can prompt: `Load .instructions/START-AI.md and run the scaffold router.`
- **Generic assistants:** require no special harness features. A plain prompt that names `.instructions/START-AI.md` is sufficient.
- **Path rule:** in an installed app, instruction paths are under `.instructions/`; in this instruction repository, paths are root-relative.

## Session Model

**Each phase — and each Phase 5 sub-phase — runs in its own AI session.**
Do not attempt multiple phases in a single session. When a phase or sub-phase is complete, create/update `HANDOFF.md` in the target project root, run `python .instructions/scripts/validate-handoff.py --root .`, and close the session. The next session starts fresh from `START-AI.md` + `HANDOFF.md` only.

## Initial Load Rule

Start each new AI session with:

1. `START-AI.md` (this file) only
2. `HANDOFF.md` in target project root (if present — it is the resume contract for mid-work sessions)

Then load only the phase files you need.

## Session Start Router

Use this decision tree before loading anything else:

```
Is HANDOFF.md present in the target project root?
  YES → Read it. Resume from currentPhase/currentSubPhase. Skip Phase Router below.
  NO  → Is this a new project or a new entity on an existing project?
          New project  → go to Version Assertion, then Phase Router
          New entity   → load support/vertical-slice-checklist.md fast-path only
```

## Version Assertion (Mandatory Gate)

Before starting any phase work, output this confirmation block:

```
Instruction set version: {version from _manifest.json}
Target project version:  {content of .instruction-version, or "first-time"}
Status: MATCH | MISMATCH | FIRST-TIME
```

- **MATCH** → proceed.
- **MISMATCH** → stop. Warn the user: *"Target project was scaffolded with instruction set v{old}; current is v{new}. Review the instruction set changes before continuing."*
- **FIRST-TIME** → proceed. After Phase 4 completes, create `.instruction-version` in the target project root with the manifest version string.

If `HANDOFF.md` contains an `instructionVersion` field, include it in the comparison.

## MCP Server Check

Before loading phase files, verify the AI assistant has appropriate MCP servers enabled for the current phase.

**Always on:**
- Microsoft Docs MCP — .NET/Azure docs, samples, full-page retrieval
- Context7 MCP — third-party library/API docs

**Enable by phase:**

| Phase | MCP Servers to Enable |
|---|---|
| 1–2 (Domain/Resource) | GitHub MCP (repo context), Sequential Thinking MCP (complex design) |
| 3 (Planning) | GitHub MCP, Azure MCP (resource validation) |
| 4 (Contract Scaffolding) | GitHub MCP |
| 5a–5b (Foundation/Core TDD) | GitHub MCP |
| 5c (Runtime/Edge) | GitHub MCP, Azure MCP |
| 5d (Optional Hosts) | Playwright MCP (if Uno UI), Fetch MCP (external specs) |
| 5e (Quality/Delivery) | GitHub MCP (CI workflows), Azure MCP (IaC validation), Playwright MCP (E2E) |
| 5f (Auth) | Azure MCP (Entra config) |
| 5g (AI Integration) | Azure MCP (Foundry/AI Search) |

If a suggested MCP server is not available, note it in `HANDOFF.md` under Residual Environment Note and continue without it.

### Project-Specific Tooling (Phase 3+)

If `implementation-plan.md` exists in the target project root, load its **Tooling & Environment Readiness** section at the start of every session. Before beginning phase work:

1. Check that all CLIs marked for the current phase are installed (run `--version` or equivalent).
2. Verify MCP servers listed for the current phase are enabled.
3. If a required CLI is missing, install it or record the gap in `HANDOFF.md`.

**Prefer CLIs over MCP servers over online resources.** CLIs produce structured output with lower token cost. MCP servers add value for interactive exploration. When neither exists, use documentation URLs and GitHub repos recorded in the implementation plan (fetch via Fetch MCP or direct file read).

## Conflict Resolution Order

When instructions in different files disagree:
`support/execution-gates.md` > `ai/SKILL.md` > individual skill files > templates

## Phase Load Resolution

Use `phase-load-packs.json` as the primary interface for phase file lists. It is pre-computed from `_manifest.json` and keyed by scaffold mode.

1. Read `phase-load-packs.json` → `packs.<mode>.<phase>` for the current phase and mode.
2. Load only the returned files.
3. For Phase 5a/5b in constrained sessions, you may scope further with `python scripts/get-phase-load-set.py --phase <5a|5b> --mode <mode> --slice <domain|repository|service|endpoint>`. Slices are curated compact bundles inside the same sub-phase. They narrow context only; they do not create new sub-phases or change gates.
4. For Phase 5g, scope further: load only AI search or agent files as needed.

Compact slice note: the `phase-5b:endpoint` compact slice intentionally omits `skills/testing.md` to stay within compact budget. For endpoint test implementation in compact sessions, rely on `templates/test-templates-endpoint.md` and phase-specific endpoint/service templates.

When this instruction set is installed under `.instructions/` and commands are run from the target project root, prefix script paths with `.instructions/` (for example `python .instructions/scripts/get-phase-load-set.py ...`).

**To regenerate** (after adding/removing instruction files in this instruction repository only): run `python scripts/generate-phase-load-packs.py`.

**To resolve a current load set**: run `python scripts/get-phase-load-set.py --phase <phase> --mode <full|lite|api-only> [--slice <name>] [feature flags]`. The resolver expands transitive `requires`/`dependencies` and applies manifest-driven mode exclusions. Exception: Phase 5d optional-host sessions return only the enabled host guidance so prior-phase docs remain on-demand and Uno/UI sessions stay within budget.

For quick template lookups, see `templates/index.md`.

## Phase Router

Each phase is one session. Load only the files listed for the current phase.

**Developer Clarification Rule:** At the start of each phase, ask for required or unsafe-missing inputs before generating code or configuration. Do not block on values covered by canonical defaults; apply the default, state the assumption, and record it in `HANDOFF.md`.

- **Phase 1 (Domain Discovery)** — Session 1
  - `ai/shared-understanding-interview.md`
  - `ai/domain-specification-schema.md`
  - `templates/ubiquitous-language-template.md`
  - `templates/design-decisions-template.md`
  - **Interview first:** walk every branch in `shared-understanding-interview.md` until the developer confirms, defaults, or explicitly defers each branch. Resolve decision dependencies before finalizing child decisions.
  - Output: `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md` in target project root
  - Gate: `python .instructions/scripts/validate-domain-spec.py --file-path domain-specification.yaml` passes (or `python scripts/validate-domain-spec.py ...` when running from the instruction repo root)
  - Gate: `python .instructions/scripts/validate-ubiquitous-language.py --root .` passes (or `python scripts/validate-ubiquitous-language.py --root <target-root>` from the instruction repo root)
  - Session end: domain spec, language, and decisions are complete, validated, and human-reviewed → write `HANDOFF.md` → close session

- **Phase 2 (Resource Definition)** — Session 2
  - `ai/resource-implementation-schema.md`
  - `ai/domain-specification-schema.md` (reference)
  - `DESIGN-DECISIONS.md` from the target project root
  - **Ask clarification questions first:** unresolved or dependent resource decisions from `DESIGN-DECISIONS.md`, resource types, API surface, external integrations, data volumes, scaling needs, caching strategy, messaging patterns, optional workloads (Function App, Scheduler, Gateway)
  - Output: `resource-implementation.yaml` in target project root
  - Gate: `python .instructions/scripts/validate-resource-impl.py --file-path resource-implementation.yaml --domain-spec-path domain-specification.yaml` passes (or `python scripts/validate-resource-impl.py ...` when running from the instruction repo root)
  - Session end: YAML complete, validated, `externalDependencyModes` declared for every external dep → write `HANDOFF.md` → close session

- **Phase 3 (Implementation Plan)** — Session 3
  - `ai/implementation-plan.md`
  - `ai/domain-specification-schema.md` (reference)
  - `ai/resource-implementation-schema.md` (reference)
  - `UBIQUITOUS-LANGUAGE.md` and `DESIGN-DECISIONS.md` from the target project root
  - **Ask clarification questions first:** any remaining design dependency conflicts, tooling preferences (ORM specifics, caching lib, messaging transport), deployment regions, cost constraints, team constraints
  - **Pre-flight:** Ask for custom/private NuGet feed URL and confirm the developer has a GitHub PAT with package read access exposed via `NUGET_AUTH_TOKEN` or an approved credential provider. Update `nuget.config`. Require `dotnet restore` exit 0.
  - **Feed setup helper:** Prefer `python .instructions/scripts/configure-ef-packages-feed.py --root . --feed-url <feed-url> --username <github-user>` so `nuget.config` uses `%NUGET_AUTH_TOKEN%` and never stores the PAT.
  - **Pre-flight:** Verify `dotnet ef` is available (`dotnet tool list`). If missing: `dotnet new tool-manifest && dotnet tool install dotnet-ef`. If `nuget.config` uses package source mapping, add `<package pattern="dotnet-ef" />` under the `nuget.org` source.
  - **Tooling discovery:** Analyze `resource-implementation.yaml` to identify all required CLIs and beneficial MCP servers for Phases 4–5. Search npm (`mcp + <library/service>`) and MCP registries for project-specific servers. For libraries with no CLI or MCP, locate documentation URLs and GitHub repos. Populate the Tooling & Environment Readiness section of the implementation plan. Preference order: CLI → MCP → online resources.
  - **Feed validation:** Run `python .instructions/scripts/validate-ef-packages-feed.py --root . --config-only --require-auth-env` (or `python scripts/validate-ef-packages-feed.py ...` from the instruction repo root) before closing Phase 3.
  - **Plan validation:** Run `python .instructions/scripts/validate-implementation-plan.py --root .` before closing Phase 3.
  - Output: `implementation-plan.md` in target project root, open questions resolved, tooling identified
  - Session end: plan reviewed, blockers recorded in `HANDOFF.md` → close session

- **Phase 4 (Contract Scaffolding)** — Session 4
  - `ai/contract-scaffolding.md`
  - `skills/solution-structure.md`
  - `skills/package-dependencies.md`
  - `ai/placeholder-tokens.md`
  - `support/ef-packages-reference.md`
  - Generates: solution structure, interfaces, DTOs, entity shells, test infrastructure, no-op DI stubs
  - Gate: `dotnet build` succeeds on full solution including test projects; `python .instructions/scripts/validate-ef-packages-feed.py --root .` passes; `python .instructions/scripts/validate-scaffold-output.py --root . --phase 4` passes
  - Output: compilable skeleton that enables TDD in Phase 5a/5b
  - Session end: gate passes, `contractsScaffolded: true` in `HANDOFF.md` → close session

- **Phase 5 (Implementation)** — One session per sub-phase (5a through 5g)
  - Base: `ai/SKILL.md` + `ai/placeholder-tokens.md` + `ai/tdd-protocol.md` + `support/ef-packages-reference.md`
  - Plus only the skill/template files for the current sub-phase (use load set script)
  
  1. **5a — Foundation (TDD):** 
     - **Ask clarification questions first:** domain rule specifics, invariant constraints, inheritance patterns, special validations, audit/versioning needs
     - Write domain/rule/repository tests first, then implement entities, EF configs, and repositories. Load `test-templates-domain.md` + `test-templates-repository.md`. Gate: `dotnet build` + `dotnet test --filter "TestCategory=Unit"`.
  
  2. **5b — App Core (TDD):** 
     - **Ask clarification questions first:** service business logic details, API pagination/filtering strategy, response formats, error handling approach, idempotency needs
     - Write service tests, implement services, then write endpoint tests and implement endpoints. Replace no-op DI stubs with real implementations. Load `test-templates-service.md` + `test-templates-endpoint.md`. Gate: `dotnet build` + `dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"`.
  
  3. **5c — Runtime/Edge (tests-after):** 
     - **Ask clarification questions first:** observability/tracing preferences, health check specific needs, rate limiting strategy, caching specifics
     - Implement runtime infrastructure (health checks, observability, caching, middleware). Add tests at session end. Gate: `dotnet build` + `dotnet test` + app starts via Aspire.
  
  4. **5d — Optional Hosts (tests-after):** 
     - **Ask clarification questions first:** for each enabled host, ask host-specific details (Function App triggers/bindings/outputs, Scheduler job types/schedules, Notification channels/templates, Uno UI target platforms/responsive needs)
     - Resolve the load set with explicit feature flags (`--include-scheduler`, `--include-function-app`, `--include-uno-ui`, `--include-blazor-ui`, `--include-notifications`) and load only returned files. Uno UI stays a dedicated session. Gate: per-host status in `HANDOFF.md` + `dotnet test`.
  
  5. **5e — Quality Gates + Delivery:** 
     - **Ask clarification questions first:** code quality thresholds, load test requirements, benchmark baselines, CI-CD pipeline specifics
     - Add architecture/load/benchmark/CI-CD gates and run full regression. Load `test-templates-quality.md`. Gate: `dotnet test`.
  
  6. **5f — Authentication:** 
     - **Ask clarification questions first:** authentication provider details, custom claims/roles, B2B vs B2C needs, token expiry policies
     - Finalize identity configuration last. Use stubs in earlier sub-phases when needed for compile-time flow. Gate: authenticated endpoints respond correctly.
  
  7. **5g — AI Integration:** 
     - **Ask clarification questions first:** AI search scope/filters, agent capabilities/tools, content ingestion strategy, cost/latency constraints
     - Run only when `includeAiServices: true`, scope further with `-IncludeAiSearch` / `-IncludeAgents`. Gate: search returns results, agent responds to test prompt.
  
  - Session end: sub-phase gate passes → update `HANDOFF.md` → close session
  - After the final enabled Phase 5 sub-phase, load `support/final-scaffold-checklist.md` and run `python .instructions/scripts/run-final-scaffold-check.py --root . --require-auth-env`.

## Strict On-Demand Files

Do not preload these. Load only when the trigger condition is met:

- `support/quick-reference.md` — **load when** scaffolding entities/endpoints and you need naming conventions, DI patterns, or config key lookups
- `support/pattern-dispatcher.md` — **load when** you need the pattern index to find the right `patterns/` file for the current phase
- `support/golden-path-sample.md` — **load when** validating instruction changes against a small canonical sample scaffold
- `support/final-scaffold-checklist.md` — **load when** the final enabled Phase 5 sub-phase completes and the app needs end-to-end scaffold validation
- `patterns/data-layer-wiring.md` — **load before Phase 5a/5b** for DB context pooling, OnModelCreating, startup tasks, seed data, scaffold migrations
- `patterns/api-host-wiring.md` — **load before Phase 5b/5c** for API startup sequence, request context, conditional auth
- `patterns/infrastructure-wiring.md` — **load before Phase 5c/5d** for multi-cache config, Aspire resource wiring
- `patterns/expected-output-index.md` — **load when** verifying scaffolded file layout
- `support/ef-packages-reference.md` — **load before Phase 5a** to know which base types come from EF.Packages (do not regenerate these)
- `support/data-persistence-advanced.md` — **load when** Phase 5a/5c needs design-time factory setup, migrations, JSON mapping fallback, startup seeding, or zero-downtime schema guidance
- `support/taskflow-proof-map.md` — **load when** you need a fast reference-app proof map from instruction topic to TaskFlow implementation area
- `support/taskflow-ubiquitous-language-sample.md` — **load when** validating or shaping `UBIQUITOUS-LANGUAGE.md` and `DESIGN-DECISIONS.md` against the TaskFlow reference app
- `support/troubleshooting.md` — **load when** a build/test/run failure occurs that isn't resolved by the one-pass fix attempt
- `support/execution-gates.md` — **load when** validating phase completion gates or running operator setup checks
- `templates/index.md` — **load when** you need a quick lookup for "I need to scaffold X → load template Y + skill Z"
- `support/vertical-slice-checklist.md` — **load when** adding a new entity to an existing project (fast-path) or verifying slice completeness
- `templates/test-templates.md` — **load when** scaffolding test projects and you need the base test class patterns
- `support/UPDATE-INSTRUCTIONS.md` — **load when** recording instruction gaps or improvements discovered during scaffolding

## Defaults Source of Truth

All defaults and profile values must come from `ai/resource-implementation-schema.md` (**Canonical Defaults** section).

## Reference Application

A companion reference app (**TaskFlow**) demonstrates every pattern these instructions produce.

**Repository:** <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

**When to consult it:**
- Wiring questions — how DI registration, middleware ordering, or Aspire resources connect across projects
- Pattern ambiguity — when a skill or template describes a pattern but the concrete implementation is unclear
- Test structure — builder patterns, test base classes, InMemoryDbBuilder usage

**How to access** (in preference order):
- GitHub MCP (`github_repo` tools) — preferred; fetch specific files on demand without cloning
- Fetch MCP or WebFetch against raw GitHub URLs (`https://raw.githubusercontent.com/efreeman518/AI-Instructions-ReferenceApp/main/...`) — use when GitHub MCP is unavailable and you only need one or two files
- Local clone — last resort for broad multi-file exploration; clone into a scratch directory outside the target project

Do not copy files wholesale from the reference app. Use it as a verified example, then generate code that matches the target project's domain and naming.

## Guardrails

- Generate code only in the target project.
- Keep context minimal per phase; unload prior-phase docs when transitioning.

## Event Boundary Rule (High Priority)

Treat cross-process bus payloads as application/integration contracts, not domain model artifacts.

- Place externally published event records in `Application.Contracts.Events`.
- Use `IIntegrationEventPublisher` for Service Bus/Event Grid publication.
- Keep domain events in Domain only when they are raised from aggregate invariants and handled in-process before integration mapping.
- Do not publish Domain namespace events directly over transport.
