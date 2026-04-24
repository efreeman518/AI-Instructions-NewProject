# Scaffold New C#/.NET Application

Scaffold a new C#/.NET business application using the phased instruction set under `.instructions/`.

**Target project:** $ARGUMENTS

All instruction files (skills, templates, support docs, schemas, scripts) live under `.instructions/` in the project root. All file references below are relative to that folder.

## Instructions

You are executing the phased scaffolding workflow. Follow these steps exactly:

1. Read `.instructions/START-AI.md` — it is the canonical bootstrap. Follow it precisely.
2. Check for `HANDOFF.md` in the project root (NOT inside `.instructions/`).
   - If present: resume from `currentPhase` / `currentSubPhase`.
   - If absent: new project — run Version Assertion, then Phase Router.
3. Read `.instructions/phase-load-packs.json` to resolve the file set for the current phase. Load only those files (paths relative to `.instructions/`).

## Rules

- Generate code in `src/`, `tests/`, and project root. Never modify files under `.instructions/`.
- One phase per session. Each Phase 5 sub-phase is its own session.
- Conflict precedence: `.instructions/support/execution-gates.md` > `.instructions/ai/SKILL.md` > individual skills > templates.
- Context budget: ≤30K tokens per phase. Unload prior-phase docs when transitioning.
- Checkpoint after 15+ generated files or 3+ build/fix cycles — update `HANDOFF.md` (project root).
- After each phase gate passes, update `HANDOFF.md` and stop.
- Do not preload files from "Strict On-Demand Files" unless their trigger condition is met.
- All defaults come from `.instructions/ai/resource-implementation-schema.md` (Canonical Defaults).
- Record instruction gaps in `INSTRUCTION-GAPS.md` at the project root, do not modify instruction files.
- TaskFlow (<https://github.com/efreeman518/AI-Instructions-ReferenceApp>) is the canonical working reference app for generated patterns. When a pattern is ambiguous, consult it via GitHub MCP and `.instructions/support/taskflow-proof-map.md` before inventing a new approach. Do not copy files wholesale.

## Phases

Canonical phase list (files, outputs, gates) lives in `.instructions/START-AI.md` § Phase Router. Do not duplicate it here.

Run the Session Start Router now.
