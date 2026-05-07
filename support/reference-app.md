# Reference Application — TaskFlow

A companion reference app that demonstrates every pattern and convention this instruction set produces.

**Repository:** <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

TaskFlow is a fully scaffolded task-management application built by following this instruction set end-to-end. It covers dual DbContext pooling, YARP gateway with claims transformation, Aspire orchestration, FusionCache with Redis backplane, TickerQ scheduling, Azure Functions, multi-tenancy, scaffold-mode auth, and Uno WASM UI.

## When to consult it

- **Pattern lookups** — when an instruction or template describes a pattern (e.g., middleware ordering, repository split, cache key format), TaskFlow contains the working implementation.
- **Wiring verification** — cross-project DI registration, startup sequences, and Aspire resource definitions are all present and buildable.
- **Test structure** — unit, integration, architecture, and endpoint test projects are scaffolded with builder patterns.

For a phase-by-phase pointer map into the reference app, use [taskflow-proof-map.md](taskflow-proof-map.md).

## How the AI accesses it

- **Local clone preferred:** if `../AI-Instructions-ReferenceApp/` exists relative to the target project's parent, the AI reads TaskFlow files via the Read tool.
- **Fallback:** GitHub MCP. Use only when the local clone is absent.

## Rules

- Do not copy TaskFlow files wholesale — use as a verified example and generate code matching the target project's domain.
- When stuck on how a pattern should look in practice, consult TaskFlow before inventing a new approach.
- The reference app is always available as a live codebase the AI can search, read, and cross-reference during any phase.
