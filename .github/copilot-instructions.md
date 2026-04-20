# AI-Instructions-NewProject

This repository is an AI instruction set for scaffolding C#/.NET business applications. It is not a .NET project itself.

Use [START-AI.md](../START-AI.md) as the canonical operational bootstrap. Keep this file short and defer detailed routing, version checks, and phase-loading rules to that file.

## Minimum Rules

- Load [START-AI.md](../START-AI.md) first.
- If `HANDOFF.md` exists in the target project root, read it next and resume from its recorded phase/sub-phase.
- Use [phase-load-packs.json](../phase-load-packs.json) to load only the current phase files.
- Generate code only in the target project directory, never in this instruction repo.
- Conflict precedence is [support/execution-gates.md](../support/execution-gates.md) > [ai/SKILL.md](../ai/SKILL.md) > individual skills > templates.

Copilot agents live in [.github/agents/](agents/) — `dotnet-scaffold` runs the full phased workflow, `vertical-slice` adds a single entity.

For broader repository context, use [README.md](../README.md). For execution rules, fall back to [START-AI.md](../START-AI.md).
