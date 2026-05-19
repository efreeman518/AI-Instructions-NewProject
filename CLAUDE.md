# AI Scaffold Entry

This is a Claude Code project-memory entrypoint for AI-assisted C#/.NET scaffolding.

Do not activate the scaffold workflow automatically for ordinary repository work.

## Scope

- If this file is installed in a target app repository, use `.claude/commands/` for explicit scaffold requests, or load `.instructions/START-AI.md` when a command is unavailable.
- If working in the instruction repository itself, use [README.md](README.md) for maintenance context and author-side validation.
- If this file is read from inside `.instructions/`, paths are relative to that folder.
- `/scaffold` runs the full phased workflow.
- `/vertical-slice` adds a single entity to an existing scaffolded app.
- `/scaffold-adopt` derives Phase-1 artifacts from an existing C#/.NET solution (brownfield onramp; replaces the Phase 1 interview).

Scaffold-specific rules, phase routing, reference-app guidance, and context-loading policy belong in `START-AI.md` / `ai/SKILL.md` in this repo, `.instructions/START-AI.md` / `.instructions/ai/SKILL.md` in installed apps, and the scoped command files only.
