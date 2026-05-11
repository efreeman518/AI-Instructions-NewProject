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

## File Loading Rule

Just-in-time. One phase per session; for Phase 5, one sub-phase per session. Load only the files listed for the current phase or sub-phase (see `ai/SKILL.md` § Phase 5 file table). Add on-demand files only when the current sub-phase clearly needs them. Close the session when the gate passes; the next session resumes from `START-AI.md` + `HANDOFF.md`.

This rule is the same regardless of the model's context window. Lost-in-the-middle is real even at 200K — loading every skill hurts output quality. See [`support/OPERATIONS.md`](support/OPERATIONS.md) § Context Budgets.

Load-set sizing is derived from `scaffoldMode` (`api-only` → required-only; `lite`/`full` → required + on-demand). See [`ai/SKILL.md`](ai/SKILL.md) § Load-Set Sizing.

## Session Start Router

```
Is HANDOFF.md present?
  YES → Resume from currentPhase/currentSubPhase. Skip Phase Router.
  NO  → New project → Phase Router
        New entity  → load support/vertical-slice-checklist.md fast-path only
```

## Tooling Check

Prefer CLIs over MCP over online resources. Use Microsoft Docs or Context7 when current docs are needed; add GitHub, Azure, Playwright, or Fetch only when the current phase needs repo, cloud, UI, or external document access. If a server is unavailable, note it in `HANDOFF.md` and continue. If `implementation-plan.md` exists, reload its **Tooling & Environment Readiness** section at session start and verify CLIs marked for the current phase are installed.

## Conflict Resolution Order

See `ai/SKILL.md` § Non-Negotiables (canonical).

## Phase Router

Each phase = one session. Load only the files listed for the current phase.

- **Phase 1 (Domain Discovery):** `ai/shared-understanding-interview.md`, `ai/domain-specification-schema.md`, `templates/ubiquitous-language-template.md`, `templates/design-decisions-template.md`. Walk every interview branch until the developer confirms, defaults, or defers each. Output: `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, `DESIGN-DECISIONS.md` in target root. Gate: developer reviews each artifact against its schema. → `HANDOFF.md` → close.
- **Phase 2 (Resource Definition):** `ai/resource-implementation-schema.md` + `DESIGN-DECISIONS.md`. Ask clarification questions for unresolved resource decisions, API surface, external integrations, scaling, caching, messaging, optional workloads. Output: `resource-implementation.yaml` with `externalDependencyModes` declared for every external dep. Gate: developer review. → `HANDOFF.md` → close.
- **Phase 3 (Implementation Plan):** `ai/implementation-plan.md` + Phase 1/2 schemas + project YAMLs. Pre-flight branches on `packageStrategy` (resolved in Phase 2):
  - `feed` or `hybrid` — configure the private NuGet feed via `python {instructionsRoot}/scripts/configure-ef-packages-feed.py --root . --feed-url <url> --username <github-user> --prefix <packagePrefix>` (`{instructionsRoot}` is `.instructions` in an installed app and `.` in this repo); confirm `NUGET_AUTH_TOKEN` or an approved credential provider is available.
  - `local` — no feed wiring required. Phase 4 generates `src/Packages/<packagePrefix>.*` projects from `localPackageLayers` and the solution references them via `<ProjectReference>`.
  - `hybrid` only — Phase 4 also generates `src/Packages/<packagePrefix>.*` projects for every layer in `localPackageLayers`; layers covered by the feed remain `<PackageReference>` against `customNugetFeeds`.

  In every mode: verify `dotnet ef` is available (`dotnet new tool-manifest && dotnet tool install dotnet-ef` if missing). Identify required CLIs/MCP servers per phase; populate the **Tooling & Environment Readiness** section of the plan. Gate: `dotnet restore` exits 0 + developer review of `implementation-plan.md`. → `HANDOFF.md` → close.
- **Phase 4 (Contract Scaffolding):** `ai/contract-scaffolding.md`, `skills/solution-structure.md`, `skills/package-dependencies.md`, `ai/placeholder-tokens.md`, `support/ef-packages-reference.md`. Generates: solution structure, interfaces, DTOs, entity shells, test infrastructure, no-op DI stubs. Gate: `dotnet build` succeeds on full solution including test projects. Set `currentPhase: 5`, `currentSubPhase: 5a`, and `contractsScaffolded: true` in `HANDOFF.md`. → close.
- **Phase 5 (Implementation):** one session per sub-phase (5a–5e: Foundation, App Core + Runtime, Optional Hosts, Quality + Delivery, Integration). Base: `ai/SKILL.md` + `ai/placeholder-tokens.md` + `ai/tdd-protocol.md` + `support/ef-packages-reference.md`. Per-sub-phase file lists are in `ai/SKILL.md` (Phase 5 file table). Gate per sub-phase: `dotnet build` + `dotnet test` (filter as appropriate). After the final enabled sub-phase, walk through `support/final-scaffold-checklist.md` manually.

## Reference Application

A companion reference app **TaskFlow** demonstrates every pattern these instructions produce. Canonical detail (repo URL, AI access rules, do-not-copy-wholesale rule, when to consult): see [`support/reference-app.md`](support/reference-app.md) and [`support/taskflow-proof-map.md`](support/taskflow-proof-map.md) for the phase → area index.

## Event Boundary Rule

See [skills/messaging.md](skills/messaging.md) § Event Boundary Rule (canonical) and [ai/contract-scaffolding.md](ai/contract-scaffolding.md) § Integration Events for Phase 4 contract placement.
