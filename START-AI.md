# START-AI

Bootstrap for `.instructions/` payloads. Load this file first when scaffold work is requested. Do not preload the full instruction set.

## Harness Adapter Rule

- **CLI agents (`AGENTS.md`):** proceed only when scaffolding is explicitly requested.
- **GitHub Copilot:** `dotnet-scaffold` runs the phase router; `vertical-slice` loads `support/vertical-slice-checklist.md`.
- **Claude commands:** `/scaffold` runs the phase router; `/vertical-slice` loads the slice checklist.
- **Generic assistants:** prompt with `Load .instructions/START-AI.md and run the scaffold router.`
- **Path rule:** in an installed app, paths are under `.instructions/`; in this repo, paths are root-relative.

## Session Model

Each phase — and each Phase 5 sub-phase — runs in its own AI session. When complete, write/update `HANDOFF.md` in the target project root and close the session. The next session starts fresh from `START-AI.md` + `HANDOFF.md` only.

## Initial Load Rule

Start each session with `START-AI.md` (this file) and `HANDOFF.md` in the target project root (if present — it is the resume contract). Then load only files needed for the current phase. Generate code only in the target project.

## Session Start Router

```
Is HANDOFF.md present?
  YES → Resume from currentPhase/currentSubPhase. Skip Phase Router.
  NO  → New project → Phase Router
        New entity  → load support/vertical-slice-checklist.md fast-path only
```

## MCP Server Check

| Phase | MCP Servers |
|---|---|
| Always on | Microsoft Docs, Context7 |
| 1–2 | + GitHub, Sequential Thinking |
| 3 | + GitHub, Azure |
| 4 | + GitHub |
| 5a | + GitHub |
| 5b | + GitHub, Azure |
| 5c | + Playwright (if Uno UI), Fetch |
| 5d | + GitHub, Azure, Playwright |
| 5e | + Azure |

If a server is unavailable, note it in `HANDOFF.md` and continue. **Prefer CLIs over MCP over online resources.** If `implementation-plan.md` exists, reload its **Tooling & Environment Readiness** section at session start and verify CLIs marked for the current phase are installed.

## Conflict Resolution Order

`support/execution-gates.md` > `ai/SKILL.md` > individual skill files > templates.

## Phase Router

Each phase = one session. Load only the files listed for the current phase.

- **Phase 1 (Domain Discovery):** `ai/shared-understanding-interview.md`, `ai/domain-specification-schema.md`, `templates/ubiquitous-language-template.md`, `templates/design-decisions-template.md`. Walk every interview branch until the developer confirms, defaults, or defers each. Output: `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, `DESIGN-DECISIONS.md` in target root. Gate: developer reviews each artifact against its schema. → `HANDOFF.md` → close.
- **Phase 2 (Resource Definition):** `ai/resource-implementation-schema.md` + `DESIGN-DECISIONS.md`. Ask clarification questions for unresolved resource decisions, API surface, external integrations, scaling, caching, messaging, optional workloads. Output: `resource-implementation.yaml` with `externalDependencyModes` declared for every external dep. Gate: developer review. → `HANDOFF.md` → close.
- **Phase 3 (Implementation Plan):** `ai/implementation-plan.md` + Phase 1/2 schemas + project YAMLs. Pre-flight: configure private NuGet feed via `python scripts/configure-ef-packages-feed.py --root . --feed-url <url> --username <github-user>` (if private packages used); confirm `NUGET_AUTH_TOKEN` is set; verify `dotnet ef` is available (`dotnet new tool-manifest && dotnet tool install dotnet-ef` if missing). Identify required CLIs/MCP servers per phase; populate the **Tooling & Environment Readiness** section of the plan. Gate: `dotnet restore` exits 0 + developer review of `implementation-plan.md`. → `HANDOFF.md` → close.
- **Phase 4 (Contract Scaffolding):** `ai/contract-scaffolding.md`, `skills/solution-structure.md`, `skills/package-dependencies.md`, `ai/placeholder-tokens.md`, `support/ef-packages-reference.md`. Generates: solution structure, interfaces, DTOs, entity shells, test infrastructure, no-op DI stubs. Gate: `dotnet build` succeeds on full solution including test projects. Set `contractsScaffolded: true` in `HANDOFF.md`. → close.
- **Phase 5 (Implementation):** one session per sub-phase (5a–5e: Foundation, App Core + Runtime, Optional Hosts, Quality + Delivery, Integration). Base: `ai/SKILL.md` + `ai/placeholder-tokens.md` + `ai/tdd-protocol.md` + `support/ef-packages-reference.md`. Per-sub-phase file lists are in `ai/SKILL.md` (Phase 5 file table). Gate per sub-phase: `dotnet build` + `dotnet test` (filter as appropriate). After the final enabled sub-phase, walk through `support/final-scaffold-checklist.md` manually.

## Reference Application

A companion reference app **TaskFlow** demonstrates every pattern these instructions produce.

**Repository:** <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

**Local clone preferred:** if `../AI-Instructions-ReferenceApp/` exists relative to the target project's parent, read TaskFlow files via the Read tool. Fall back to GitHub MCP only when the local clone is absent.

**When to consult:** wiring questions (DI, middleware, Aspire), pattern ambiguity, test structure. Use `support/taskflow-proof-map.md` for the phase → area index. Do not copy TaskFlow files wholesale — use as a verified example and generate code matching the target project's domain.

## Event Boundary Rule

Cross-process bus payloads are application/integration contracts, not domain artifacts. Place externally published event records in `Application.Contracts.Events`. Use `IIntegrationEventPublisher` for Service Bus/Event Grid. Keep domain events in Domain only when raised from aggregate invariants and handled in-process before integration mapping. Do not publish Domain namespace events directly over transport.
