# START-AI

Load this file first. Do not preload the full instruction set.

## Session Model

**Each phase — and each Phase 4 sub-phase — runs in its own AI session.**
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
- **MISMATCH** → stop. Warn the user: *"Target project was scaffolded with instruction set v{old}; current is v{new}. Review README.md release notes for breaking changes before continuing."*
- **FIRST-TIME** → proceed. After Phase 4a completes, create `.instruction-version` in the target project root with the manifest version string.

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
| 4a–4b (Foundation/Core) | GitHub MCP |
| 4c (Runtime/Edge) | GitHub MCP, Azure MCP |
| 4d (Optional Hosts) | Playwright MCP (if Uno UI), Fetch MCP (external specs) |
| 4e (Quality/Delivery) | GitHub MCP (CI workflows), Azure MCP (IaC validation), Playwright MCP (E2E) |
| 4f (Auth) | Azure MCP (Entra config) |
| 4g (AI Integration) | Azure MCP (Foundry/AI Search) |

If a suggested MCP server is not available, note it in `HANDOFF.md` under Residual Environment Note and continue without it.

## Conflict Resolution Order

When instructions in different files disagree:
`support/execution-gates.md` > `ai/SKILL.md` > individual skill files > templates

## Phase Load Resolution

Use `phase-load-packs.json` as the primary interface for phase file lists. It is pre-computed from `_manifest.json` and keyed by scaffold mode.

1. Read `phase-load-packs.json` → `packs.<mode>.<phase>` for the current phase and mode.
2. Load only the returned files.
3. For Phase 4g, scope further: load only AI search or agent files as needed.

**To regenerate** (after adding/removing instruction files): run `python scripts/get-phase-load-set.py --phase <phase> --mode <full|lite|api-only> [feature flags]`. The resolver expands transitive `requires`/`dependencies` and applies manifest-driven mode exclusions.

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
  - Output: `implementation-plan.md` in target project root, open questions resolved
  - Session end: plan reviewed, blockers recorded in `HANDOFF.md` → close session

- **Phase 4 (Implementation)** — One session per sub-phase (4a through 4g)
  - Base: `ai/SKILL.md` + `ai/placeholder-tokens.md`
  - Plus only the skill/template files for the current sub-phase (use load set script)
  - Session end: sub-phase gate passes → update `HANDOFF.md` → close session

## Strict On-Demand Files

Do not preload these. Load only when the trigger condition is met:

- `support/quick-reference.md` — **load when** scaffolding entities/endpoints and you need naming conventions, DI patterns, or config key lookups
- `support/sampleapp-patterns.md` — **load when** you need the pattern index to find the right `patterns/` file for the current phase
- `patterns/data-layer-wiring.md` — **load before Phase 4a/4b** for DB context pooling, OnModelCreating, startup tasks, seed data, scaffold migrations
- `patterns/api-host-wiring.md` — **load before Phase 4b/4c** for API startup sequence, request context, conditional auth
- `patterns/infrastructure-wiring.md` — **load before Phase 4c/4d** for multi-cache config, Aspire resource wiring
- `patterns/expected-output-index.md` — **load when** verifying scaffolded file layout
- `support/ef-packages-reference.md` — **load before Phase 4a** to know which base types come from EF.Packages (do not regenerate these)
- `support/troubleshooting.md` — **load when** a build/test/run failure occurs that isn't resolved by the one-pass fix attempt
- `support/execution-gates.md` — **load when** validating phase completion gates or running operator setup checks
- `templates/index.md` — **load when** you need a quick lookup for "I need to scaffold X → load template Y + skill Z"

## Defaults Source of Truth

All defaults and profile values must come from `ai/resource-implementation-schema.md` (**Canonical Defaults** section).

## Guardrails

- Generate code only in the target project.
- Keep context minimal per phase; unload prior-phase docs when transitioning.
