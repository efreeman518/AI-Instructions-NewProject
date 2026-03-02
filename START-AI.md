# START-AI

Load this file first. Do not preload the full instruction set.

## Initial Load Rule

Start each new AI session with:

1. `START-AI.md` (this file) only
2. `HANDOFF.md` in target project root (if present)

Then load only the phase files you need.

## Version Assertion

Before starting any phase, verify instruction set compatibility:

1. Read `_manifest.json` → extract `"version"`.
2. If the target project contains a `.instruction-version` file, compare its content with the manifest version.
   - **Match** → proceed normally.
   - **Mismatch** → warn the user: *"Target project was scaffolded with instruction set v{old}; current instructions are v{new}. Review CHANGELOG.md for breaking changes before continuing."*
   - **Missing** → first-time scaffold. After Phase 4a completes, create `.instruction-version` in the target project root containing the current manifest version.
3. If `HANDOFF.md` exists and contains a `instructionVersion` field, cross-check that it matches the manifest as well.

## Phase Load Automation

Prefer generated load packs instead of manual file picking:

1. `./scripts/generate-phase-load-packs.ps1`
2. `./scripts/get-phase-load-set.ps1 -Phase <phase> -Mode <full|lite|api-only> [feature flags]`
3. Load only the returned files.

Canonical generated output: `phase-load-packs.json`.

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
  - plus only the skill/template files required for the current sub-phase (prefer generated load sets over manual selection)

## Strict On-Demand Files

Do not preload these files. Load only when needed:

- `quick-reference.md` (naming/DI/config lookups)
- `sampleapp-patterns.md` (cross-project pattern selection)
- `troubleshooting.md` (only when failures occur)
- `engineer-checklist.md` (execution/infra verification)
- `execution-gates.md` (canonical phase checkpoints and commands)
- `troubleshooting.md` (triage rules + canonical recurring test failures and fixes)

## Defaults Source of Truth

All defaults and profile values must come from `resource-implementation-schema.md` (**Canonical Defaults** section).

## Guardrails

- Never edit/build `sample-app/`.
- Generate code only in the target project.
- Keep context minimal per phase and unload prior-phase docs.