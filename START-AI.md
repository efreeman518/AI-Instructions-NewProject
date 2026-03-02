# START-AI

Load this file first. Do not preload the full instruction set.

## Initial Load Rule

Start each new AI session with:

1. `START-AI.md` (this file) only
2. `HANDOFF.md` in target project root (if present)

Then load only the phase files you need.

## Phase Router

- **Phase 1 (Domain Discovery)**
  - `domain-specification-schema.md`
- **Phase 2 (Resource Definition)**
  - `resource-implementation-schema.md`
  - `domain-specification-schema.md` (reference)
- **Phase 3 (Implementation Plan)**
  - `implementation-plan.md`
  - `domain-specification-schema.md` (reference)
  - `resource-implementation-schema.md` (reference)
  - **Pre-flight:** Prompt user for any custom/private NuGet feed URLs and auth method before proceeding. Update `nuget.config` and verify `dotnet restore` exits 0.
- **Phase 4 (Implementation)**
  - `SKILL.md`
  - `placeholder-tokens.md`
  - plus only the skill/template files required for the current sub-phase (see Phase Loading Manifest in `SKILL.md`)

## Strict On-Demand Files

Do not preload these files. Load only when needed:

- `quick-reference.md` (naming/DI/config lookups)
- `sampleapp-patterns.md` (cross-project pattern selection)
- `troubleshooting.md` (only when failures occur)
- `engineer-checklist.md` (execution/infra verification)

## Defaults Source of Truth

All defaults and profile values must come from `resource-implementation-schema.md` (**Canonical Defaults** section).

## Guardrails

- Never edit/build `sample-app/`.
- Generate code only in the target project.
- Keep context minimal per phase and unload prior-phase docs.