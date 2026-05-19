# AI-Instructions-Scaffold

This repository maintains an AI instruction set for scaffolding C#/.NET business applications. It is not a .NET project itself.

This file is a neutral repository-level pointer for GitHub Copilot. Do not activate the scaffold workflow automatically from this file.

## Scope

- For ordinary repo maintenance, use [README.md](../README.md) for context.
- For author-side validation, use the scripts documented in [README.md](../README.md).
- For actual app scaffolding, use the scoped Copilot agents in [.github/agents/](agents/).
- `dotnet-scaffold` runs the full phased workflow.
- `vertical-slice` adds a single entity to an existing scaffolded app.
- `scaffold-adopt` derives Phase-1 artifacts from an existing C#/.NET solution (brownfield onramp; replaces the Phase 1 interview).

Scaffold-specific rules, phase routing, reference-app guidance, and context-loading policy belong in [START-AI.md](../START-AI.md), [ai/SKILL.md](../ai/SKILL.md), and the scoped command/agent files only.
