# AI Scaffold Entry

This is a harness-neutral entrypoint for CLI agents that read root `AGENTS.md`
files, including Codex-style and Copilot-style CLI workflows.

Do not activate the scaffold workflow automatically for ordinary repository
work.

## Scaffold Trigger

Run scaffold instructions only when the user explicitly asks to:

- scaffold a new .NET app or service;
- continue an existing scaffold phase;
- add a scaffolded vertical slice/entity.

## Scaffold Boot

1. If this file is installed in a target app repository, load `.instructions/START-AI.md`.
2. If working in the instruction repository itself, load `START-AI.md`.
3. Follow the phase router and one-phase-per-session rule.
4. Record scaffold gaps in `INSTRUCTION-GAPS.md` at the target project root.

For normal coding, review, docs, or maintenance tasks, ignore scaffold phase
rules and use regular project context only.
