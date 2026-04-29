# Scaffold New C#/.NET Application

Scaffold a new C#/.NET business application using the phased instruction set under `.instructions/`.

**Target project:** $ARGUMENTS

All instruction files (skills, templates, support docs, schemas, scripts) live under `.instructions/` in the project root. All file references below are relative to that folder.

## Instructions

You are executing the phased scaffolding workflow. Follow these steps exactly:

1. Read `.instructions/START-AI.md` — it is the canonical bootstrap. Follow it precisely.
2. Check for `HANDOFF.md` in the project root (NOT inside `.instructions/`).
   - If present: resume from `currentPhase` / `currentSubPhase`.
   - If absent: new project — run the Phase Router.
3. Load only the files listed for the current phase in the Phase Router. For Phase 5, use the Phase 5 file table in `.instructions/ai/SKILL.md`.

## Rules

- Generate code in `src/`, `tests/`, and project root. Never modify files under `.instructions/`.
- Conflict precedence: `.instructions/support/execution-gates.md` > `.instructions/ai/SKILL.md` > individual skills > templates.
- Checkpoint after 15+ generated files or 3+ build/fix cycles — update `HANDOFF.md` (project root).
- After each phase gate passes, update `HANDOFF.md` and stop.
- Record instruction gaps in `INSTRUCTION-GAPS.md` at the project root, do not modify instruction files.
- Session model, default sources, and TaskFlow rules: see `.instructions/START-AI.md` § Session Model and § Reference Application, plus `.instructions/ai/SKILL.md` Non-Negotiables.
- Phase 1 produces `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md` after the shared-understanding interview.

## Phases

Canonical phase list (files, outputs, gates) lives in `.instructions/START-AI.md` § Phase Router. Do not duplicate it here.

Run the Session Start Router now.
