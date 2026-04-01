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
- **MISMATCH** → stop. Warn the user: *"Target project was scaffolded with instruction set v{old}; current is v{new}. Review support/CHANGELOG.md for breaking changes before continuing."*
- **FIRST-TIME** → proceed. After Phase 4a completes, create `.instruction-version` in the target project root with the manifest version string.

If `HANDOFF.md` contains an `instructionVersion` field, include it in the comparison.

## Conflict Resolution Order

When instructions in different files disagree:
`support/execution-gates.md` > `ai/SKILL.md` > individual skill files > templates

## Phase Load Automation

Prefer generated load packs over manual file picking:

1. Run `./scripts/get-phase-load-set.ps1 -Phase <phase> -Mode <full|lite|api-only> [feature flags]`
2. The resolver expands transitive `requires`/`dependencies` and applies manifest-driven mode exclusions.
3. For Phase 4g, add `-IncludeAiSearch` and/or `-IncludeAgents` when only one AI capability is in scope.
4. Load only the returned files.

**If scripts are unavailable:** read `phase-load-packs.json` directly for the current phase and mode.

Canonical generated output: `phase-load-packs.json`.

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

Do not preload these. Load only when needed:

- `support/quick-reference.md` (naming/DI/config lookups)
- `support/sampleapp-patterns.md` (cross-project pattern selection; load before opening raw sampleapp source)
- `support/troubleshooting.md` (only when failures occur)
- `support/engineer-checklist.md` (execution/infra verification)
- `support/execution-gates.md` (canonical phase checkpoints and commands)

## Defaults Source of Truth

All defaults and profile values must come from `ai/resource-implementation-schema.md` (**Canonical Defaults** section).

## Guardrails

- Never edit/build `sample-app/`.
- Generate code only in the target project.
- Keep context minimal per phase; unload prior-phase docs when transitioning.
