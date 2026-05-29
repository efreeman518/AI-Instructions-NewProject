# AI Scaffold Entry

This is a GitHub Copilot project-memory entrypoint for AI-assisted C#/.NET scaffolding.

Do not activate the scaffold workflow automatically for ordinary repository work.

## Scope

- If this file is installed in a target app repository, use the scoped Copilot agents in [.github/agents/](agents/) for explicit scaffold requests, or load `.instructions/START-AI.md` when an agent is unavailable.
- If working in the instruction repository itself, use [README.md](../README.md) for maintenance context and author-side validation.
- `dotnet-scaffold` runs the full phased workflow.
- `vertical-slice` adds a single entity to an existing scaffolded app.
- `scaffold-adopt` derives Phase-1 artifacts from an existing C#/.NET solution (brownfield onramp; replaces the Phase 1 interview).

Phase 1 is the universal core (stack-agnostic). Phases 2-5 run under the C#/.NET/Azure profile - see [../profiles/csharp-dotnet-azure.md](../profiles/csharp-dotnet-azure.md) (this repo) or `.instructions/profiles/csharp-dotnet-azure.md` (installed apps). `vertical-slice` and `scaffold-adopt` are profile-bound.

Scaffold-specific rules, phase routing, reference-app guidance, and context-loading policy belong in `START-AI.md` / `ai/SKILL.md` in this repo, `.instructions/START-AI.md` / `.instructions/ai/SKILL.md` in installed apps, and the scoped agent files only.

<!-- rtk-instructions v2 -->
# RTK - Token-Optimized CLI

**rtk** is a CLI proxy that filters and compresses command outputs, saving 60-90% tokens.

## Rule

Always prefix shell commands with `rtk`:

```bash
# Instead of:              Use:
git status                 rtk git status
git log -10                rtk git log -10
cargo test                 rtk cargo test
docker ps                  rtk docker ps
kubectl get pods           rtk kubectl pods
```

## Meta commands (use directly)

```bash
rtk gain              # Token savings dashboard
rtk gain --history    # Per-command savings history
rtk discover          # Find missed rtk opportunities
rtk proxy <cmd>       # Run raw (no filtering) but track usage
```
<!-- /rtk-instructions -->