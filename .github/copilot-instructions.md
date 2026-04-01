# AI-Instructions-NewProject

This repository is an AI instruction set for scaffolding C#/.NET business applications. It is NOT a .NET project itself — it contains markdown instructions, templates, and schemas.

## Session Bootstrap

**Always start from `START-AI.md`.** It contains the session router, version checks, and phase loading rules. Do not preload the full instruction set.

If `HANDOFF.md` exists in the target project root, read it to resume from the last session.

## Phase Loading

Use `phase-load-packs.json` as the primary interface for determining which files to load per phase. It is pre-computed from `_manifest.json` and keyed by scaffold mode (`full`, `lite`, `api-only`).

Only load files for the current phase. Unload prior-phase files when transitioning.

## Guardrails

- Generate code only in the **target project directory** (not this repo).
- `support/sampleapp-patterns.md` contains composition wiring patterns and the expected output file index — load it when building new slices or debugging cross-file wiring.
- Conflict resolution: `support/execution-gates.md` > `ai/SKILL.md` > individual skills > templates.

## Key Files

- `START-AI.md` — session bootstrap and phase router
- `ai/SKILL.md` — Phase 4 execution rules
- `ai/placeholder-tokens.md` — token substitution glossary
- `phase-load-packs.json` — pre-computed phase file lists by mode
- `_manifest.json` — token estimates, phase membership, dependencies
- `templates/index.md` — quick lookup for "I need to scaffold X → load Y"
- `support/sampleapp-patterns.md` — composition wiring patterns + expected output file index
