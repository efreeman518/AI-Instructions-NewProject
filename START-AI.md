# START-AI

This file is the canonical AI session bootstrap for this repository. Keep `CLAUDE.md` and `.github/copilot-instructions.md` as thin entry-point summaries that point back here.

Load this file first. Do not preload the full instruction set.

## Session Model

**Each phase — and each Phase 5 sub-phase — runs in its own AI session.**
Do not attempt multiple phases in a single session. When a phase or sub-phase is complete, create/update `HANDOFF.md` in the target project root and close the session. The next session starts fresh from `START-AI.md` + `HANDOFF.md` only.

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

**To regenerate** (after adding/removing instruction files): run `python scripts/generate-phase-load-packs.py`.

**To resolve a current load set**: run `python scripts/get-phase-load-set.py --phase <phase> --mode <full|lite|api-only> [--slice <name>] [feature flags]`. The resolver expands transitive `requires`/`dependencies` and applies manifest-driven mode exclusions.

For quick template lookups, see `templates/index.md`.

## Phase Router

Each phase is one session. Load only the files listed for the current phase.

- **Phase 1 (Domain Discovery)** — Session 1
  - `ai/domain-specification-schema.md`
  - Output: `domain-specification.yaml` in target project root
  - Session end: YAML is complete and human-reviewed → write `HANDOFF.md` → close session

- **Phase 2 (Resource Definition)** — Session 2
  - `ai/resource-implementation-schema.md`
  - `ai/domain-specification-schema.md` (reference)
  - Output: `resource-implementation.yaml` in target project root
  - Session end: YAML complete, `externalDependencyModes` declared for every external dep → write `HANDOFF.md` → close session

- **Phase 3 (Implementation Plan)** — Session 3
  - `ai/implementation-plan.md`
  - `ai/domain-specification-schema.md` (reference)
  - `ai/resource-implementation-schema.md` (reference)
  - **Pre-flight:** Ask for custom/private NuGet feed URLs and auth method. Update `nuget.config`. Require `dotnet restore` exit 0.
  - **Pre-flight:** Verify `dotnet ef` is available (`dotnet tool list`). If missing: `dotnet new tool-manifest && dotnet tool install dotnet-ef`. If `nuget.config` uses package source mapping, add `<package pattern="dotnet-ef" />` under the `nuget.org` source.
  - **Tooling discovery:** Analyze `resource-implementation.yaml` to identify all required CLIs and beneficial MCP servers for Phases 4–5. Search npm (`mcp + <library/service>`) and MCP registries for project-specific servers. For libraries with no CLI or MCP, locate documentation URLs and GitHub repos. Populate the Tooling & Environment Readiness section of the implementation plan. Preference order: CLI → MCP → online resources.
  - Output: `implementation-plan.md` in target project root, open questions resolved, tooling identified
  - Session end: plan reviewed, blockers recorded in `HANDOFF.md` → close session

- **Phase 4 (Contract Scaffolding)** — Session 4
  - `ai/contract-scaffolding.md`
  - `skills/solution-structure.md`
  - `skills/package-dependencies.md`
  - `ai/placeholder-tokens.md`
  - `support/ef-packages-reference.md`
  - Generates: solution structure, interfaces, DTOs, entity shells, test infrastructure, no-op DI stubs
  - Gate: `dotnet build` succeeds on full solution including test projects
  - Output: compilable skeleton that enables TDD in Phase 5a/5b
  - Session end: gate passes, `contractsScaffolded: true` in `HANDOFF.md` → close session

- **Phase 5 (Implementation)** — One session per sub-phase (5a through 5g)
  - Base: `ai/SKILL.md` + `ai/placeholder-tokens.md` + `ai/tdd-protocol.md` + `support/ef-packages-reference.md`
  - Plus only the skill/template files for the current sub-phase (use load set script)
  - **Phase 5a/5b use TDD:** contracts, entity shells, and test infrastructure already exist from Phase 4. Write tests first (red), then implement (green). See `ai/tdd-protocol.md`.
  - **Phase 5c/5d use tests-after:** implement infrastructure, then write tests at end of session.
  - Session end: sub-phase gate passes → update `HANDOFF.md` → close session

## Strict On-Demand Files

Do not preload these. Load only when the trigger condition is met:

- `support/quick-reference.md` — **load when** scaffolding entities/endpoints and you need naming conventions, DI patterns, or config key lookups
- `support/pattern-dispatcher.md` — **load when** you need the pattern index to find the right `patterns/` file for the current phase
- `patterns/data-layer-wiring.md` — **load before Phase 5a/5b** for DB context pooling, OnModelCreating, startup tasks, seed data, scaffold migrations
- `patterns/api-host-wiring.md` — **load before Phase 5b/5c** for API startup sequence, request context, conditional auth
- `patterns/infrastructure-wiring.md` — **load before Phase 5c/5d** for multi-cache config, Aspire resource wiring
- `patterns/expected-output-index.md` — **load when** verifying scaffolded file layout
- `support/ef-packages-reference.md` — **load before Phase 5a** to know which base types come from EF.Packages (do not regenerate these)
- `support/data-persistence-advanced.md` — **load when** Phase 5a/5c needs design-time factory setup, migrations, JSON mapping fallback, startup seeding, or zero-downtime schema guidance
- `support/taskflow-proof-map.md` — **load when** you need a fast reference-app proof map from instruction topic to TaskFlow implementation area
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
