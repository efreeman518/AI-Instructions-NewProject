# AI Instructions - New .NET App/Service Scaffolding

Pragmatic instruction set for AI-assisted scaffolding of C#/.NET applications and services.

## AI Agents & Harnesses - Quick Start

Clone this repo then run the install script to copy the instruction files into any target app repository root folder. The installer copies runtime instructions into `<app>/.instructions/` and places thin harness entrypoints at the app root. Scaffold rules stay app-scoped; do not put phase routing, TaskFlow rules, or generated-code conventions in global Codex, Claude, or Copilot instruction files.

### Supported harnesses

Each harness has a project-memory file (auto-loaded on session start) and, where supported, a scoped command/agent picker. The three project-memory files (`AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`) play the same role for their respective tools — the installer writes all three so any agent that opens the repo finds its native entrypoint.

| Harness | Project-memory file | Scoped command/agent picker | Full scaffold | Vertical slice |
|---|---|---|---|---|
| Codex CLI (and other CLI agents that read `AGENTS.md`) | `AGENTS.md` | — (no stable convention yet) | Prompt: `Load .instructions/START-AI.md and run the scaffold router` | Prompt: `Load .instructions/support/vertical-slice-checklist.md` |
| GitHub Copilot in VS Code | `.github/copilot-instructions.md` | `.github/agents/` | Select `dotnet-scaffold` agent | Select `vertical-slice` agent |
| Claude Code / Claude VS Code | `CLAUDE.md` | `.claude/commands/` | `/scaffold <domain>` | `/vertical-slice <Entity>` |
| Generic AI assistant | — (point it at `.instructions/START-AI.md` manually) | — | Prompt: `Load .instructions/START-AI.md and run the scaffold router` | Prompt: `Load .instructions/support/vertical-slice-checklist.md` |

All harnesses follow the same flow: scaffold sessions boot from `.instructions/START-AI.md`, resume from `HANDOFF.md` if present, run one phase, write `HANDOFF.md`, stop. Vertical-slice sessions load `.instructions/support/vertical-slice-checklist.md` and generate one full entity stack against the gate.

Canonical execution rules: [START-AI.md](START-AI.md) (session model, phase router) and [support/execution-gates.md](support/execution-gates.md) (gate commands).

### Install into a new app

Use `install-to-project.py` from a local clone of this repo. It copies only the runtime payload - instruction files, scoped agents, CLI entrypoint, and slash commands - into your app, and skips repo-maintenance files (tests, CI workflows, global assistant instruction files, git hooks, virtualenvs).

`--target` is the **app repo root** (not the `.instructions/` folder). The script creates `<target>/.instructions/` if it does not exist, and writes harness entrypoints (`AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, `.claude/commands/`, `.github/agents/`) at the target root so CLI agents, Claude, and Copilot discover the scoped scaffold instructions. Existing root-level `AGENTS.md`/`CLAUDE.md`/`copilot-instructions.md` files are preserved — the installer appends the scaffold block inside sentinel markers (`<!-- ai-scaffold: start --> ... <!-- ai-scaffold: end -->`) so re-running the installer is idempotent.

```bash
# from a clone of this repo
python scripts/install-to-project.py --target /path/to/your-app-repo
# tip: run with --dry-run first to preview what gets copied
```

**Windows Python launcher fallback chain.** Try in order; the first one that prints a version is the one to use for every `scripts/*.py` invocation in this repo:

```powershell
python --version            # may resolve to Microsoft-Store stub; if it errors, try the next
py -3 --version             # works only when the Python launcher (py.exe) is installed
.venv\Scripts\python.exe --version   # works inside a project venv
```

Then call the script with the same launcher, e.g. `python scripts/install-to-project.py --target C:\path\to\your-app` or `py -3 scripts/install-to-project.py --target ...` or `.venv\Scripts\python.exe scripts/install-to-project.py --target ...`. The same fallback applies to `configure-ef-packages-feed.py`, `validate-instructions.py`, and any other `scripts/*.py` invocation.

What it places:

| Source in this repo | Destination in your app | Mode |
|---|---|---|
| `README.md`, `CLAUDE.md`, `START-AI.md` | `<app>/.instructions/` | copy |
| `ai/`, `patterns/`, `schemas/`, `skills/`, `support/`, `templates/`, `scripts/` | `<app>/.instructions/` | copy |
| `AGENTS.md` | `<app>/AGENTS.md` (Codex-style CLI agents) | merge |
| `CLAUDE.md` | `<app>/CLAUDE.md` (Claude Code project memory) | merge |
| `.github/copilot-instructions.md` | `<app>/.github/copilot-instructions.md` (Copilot global guidance) | merge |
| `.claude/commands/` | `<app>/.claude/commands/` (Claude slash commands) | dir |
| `.github/agents/` | `<app>/.github/agents/` (Copilot scoped agents) | dir |

**Merge mode** appends the scaffold block inside `<!-- ai-scaffold: start --> ... <!-- ai-scaffold: end -->` markers when the target file already exists; existing user content is preserved.

Flags:

| Flag | Purpose |
|---|---|
| `--dry-run` | Print planned copies without writing anything. |
| `--update` | Re-run against an existing install; preserves any target file with a newer mtime than the source. Leaves `HANDOFF.md` untouched. |
| `--instructions-only` | Copy only `<app>/.instructions/`; skip `AGENTS.md`, `.claude/commands/`, and `.github/agents/` placement (useful if you manage those separately). |
| `--verify` | After install, smoke-check that the expected entrypoints and payload files exist. Non-zero exit if anything is missing. Cheap insurance after a manual edit or selective copy. |
| `--verify-only` | Skip install entirely; just run the smoke check against an existing target. Useful in CI or to confirm an unfamiliar repo is correctly wired. |

After install:

- [ ] Configure the private EF.Packages feed with `python .instructions/scripts/configure-ef-packages-feed.py --root . --feed-url https://nuget.pkg.github.com/{owner}/index.json --username {github-user}`.
- [ ] Confirm `dotnet restore` exits 0.
- [ ] Phase gates rely on `dotnet build` and `dotnet test`; the scaffold checklist at `support/final-scaffold-checklist.md` covers end-to-end acceptance.

### Manual copy (alternative)

If you prefer to copy by hand: harness discovery files (`AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, `.github/agents/`, `.claude/commands/`) live at the **app repo root**, not inside `.instructions/`. Everything else goes under `.instructions/`. Do not copy scaffold routing into a developer's global assistant instruction files — the per-app placement is what keeps scope correct. The install script above does this automatically and handles the merge into existing root-level files.

---

## Purpose

This instruction set turns an AI coding assistant into a guided scaffolding engine for production-grade C#/.NET solutions. Instead of generating throwaway boilerplate, it drives a structured five-phase process — from domain discovery through implementation — producing consistent, buildable, testable code that follows clean architecture and the conventions of a mature engineering team.

The goal is not to replace engineering judgment but to compress the multi day/week "green-field to first vertical slice" timeline down to hours, with guardrails that prevent common shortcuts (missing tests, leaky abstractions, inconsistent naming).

Phase 1 also creates durable collaboration artifacts before code planning starts:

- `UBIQUITOUS-LANGUAGE.md` records accepted domain terms, rejected synonyms, states, commands/actions, roles, events, policies, and naming guidance so later AI sessions use consistent expressions.
- `DESIGN-DECISIONS.md` records design choices and dependencies between decisions, so downstream resource and code choices do not silently contradict earlier domain answers.

### Phase-1 Artifact Lifecycle

`domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md` are **living source of truth**, not snapshots. Every phase consumes them, so they must stay current as the project evolves — otherwise later AI sessions reason from a stale model and naming/decision drift creeps in.

**When to update each:**

- **New entity, term, role, event, or domain action** → append the term to `UBIQUITOUS-LANGUAGE.md` and update the relevant section of `domain-specification.yaml` (entity, customAction, event, etc.) before generating code. The `/vertical-slice` checklist enforces this as a pre-flight step.
- **New design choice or revision of an earlier one** → append to `DESIGN-DECISIONS.md`. Do not silently rewrite earlier entries; mark the prior decision as superseded and link forward, so the dependency graph remains traceable.
- **Schema or relationship change** → update `domain-specification.yaml` first, then propagate to EF configuration, repositories, DTOs, mappers, and tests in that order.

**Drift signal.** If `UBIQUITOUS-LANGUAGE.md` and code identifiers diverge, the doc is wrong, not the code (per [support/final-scaffold-checklist.md](support/final-scaffold-checklist.md) language-failure rule). Update the doc to match accepted reality before changing code names. The same applies to `DESIGN-DECISIONS.md` — if the implemented architecture has moved past a recorded decision, supersede the entry rather than leaving the doc to contradict the code.

**Mid-scaffold corrections.** When a domain misunderstanding surfaces mid-Phase-5 (entity purpose wrong, term mismatched, decision violated), [support/OPERATIONS.md](support/OPERATIONS.md) is the canonical recovery path: clarify with the user, update the Phase-1 artifacts, then re-scaffold the affected slice.

**Do not delete.** These artifacts are the onboarding surface for every future AI session, code reviewer, and new team member. Keep them in the repo root for the life of the project — even when they grow long, the cost of reading them is far lower than the cost of an AI session reasoning from absent context.

## Phases

Each phase runs in its own AI session and produces artifacts the next phase consumes.

| Phase | Purpose | Output |
|---|---|---|
| **1 — Domain Discovery** | Structured interview to reach shared understanding, define ubiquitous language, resolve decision dependencies, and capture entities, relationships, events, workflows, and business rules in pure business language — no implementation details. | `domain-specification.yaml`, `UBIQUITOUS-LANGUAGE.md`, `DESIGN-DECISIONS.md` |
| **2 — Resource Definition** | Map each resource requirement to concrete technology choices — data stores, messaging, AI capabilities, hosting models. | `resource-implementation.yaml` |
| **3 — Implementation Planning** | Resolve open questions, verify tooling (NuGet feeds, CLIs), discover project-specific CLIs and MCP servers, and produce a sequenced build plan. | `implementation-plan.md` |
| **4 — Contract Scaffolding** | Generate solution structure, interfaces, DTOs, entity shells, test infrastructure, and no-op DI stubs. Gate: `dotnet build` succeeds on the full solution. | Compilable skeleton |
| **5 — Implementation (TDD)** | Build vertical slices entity-by-entity across sub-phases 5a–5e (Foundation, App Core + Runtime, Optional Hosts, Quality + Delivery, Integration). Phase 5a uses test-driven development (write tests first → red → implement → green); 5b is mixed (TDD for app/API, tests-after for runtime); 5c–5e are tests-after. | Production code + passing tests |

Phase details: [START-AI.md](START-AI.md) § Phase Router.

## Approach

The instruction set is designed around three core ideas:

**1. Phased workflow with TDD.**
Five phases prevent hallucinated architecture by ensuring verified context before code is written. Phase 4 produces a compilable skeleton so that Phase 5a/5b can follow a strict red/green TDD cycle — tests are written against contracts before any implementation exists. See [ai/tdd-protocol.md](ai/tdd-protocol.md).

**2. Skills and templates as composable units.**
Implementation knowledge is split into 32 skill files (how things work) and 27 template files plus a `templates/index.md` index (what to generate). The Phase Router in `START-AI.md` and the Phase 5 file table in `ai/SKILL.md` tell the agent which files to load for the current phase or sub-phase.

**3. Composition patterns, not documentation alone.**
Pattern files in `patterns/` document how generated components wire together across projects — database context pooling, API startup sequences, request context resolution, cache configuration, and Aspire resource wiring. The pattern index lives in `ai/SKILL.md` § Non-Negotiables (one bullet per pattern, with the phase to load it). This grounds the generated output in proven, real-world patterns rather than abstract descriptions.

## Reference Application

A companion reference app — **TaskFlow** — demonstrates every pattern and convention these instructions produce: dual DbContext pooling, YARP gateway, Aspire orchestration, FusionCache + Redis backplane, TickerQ scheduling, Azure Functions, multi-tenancy, scaffold-mode auth, and Uno WASM UI.

**Repository:** <https://github.com/efreeman518/AI-Instructions-ReferenceApp>

For consultation rules, AI access (local clone vs GitHub MCP), and the phase → area pointer map, see [support/reference-app.md](support/reference-app.md) and [support/taskflow-proof-map.md](support/taskflow-proof-map.md).

## Quick Start

If you want the shortest path from zero context to first scaffold:

1. Create a new app repo.
2. Run `python scripts/install-to-project.py --target /path/to/your-app-repo --verify` from this repo.
3. Start through the harness table above: `AGENTS.md`, Copilot agent, Claude command, or a prompt that loads `.instructions/START-AI.md`.

**First time?** Use the [Minimum Viable Scaffold (MVS)](support/minimum-viable-scaffold.md) — paste-ready prompts (one per phase, with Phase 5 split into 5a + 5b sessions) that produce an API-only app with one entity, no Gateway/UI/AI/messaging. You can promote to a richer profile once the loop feels familiar.

Read the rest of this guide when you need setup details, MCP recommendations, or troubleshooting rules.

## Prerequisites

- `git`
- A current Python 3 to run `install-to-project.py`, `configure-ef-packages-feed.py`, and `validate-instructions.py`. **On Windows, no single launcher is universal:** `py -3` works on machines with the Python launcher installed, the bare `python` command works when a real Python is on PATH (it may resolve to the Microsoft-Store stub if not), and `.venv\Scripts\python.exe` works inside a project venv. Run `where python` and `where py` to see what's available, then pick the one that prints a real path. The scripts have no Python-version-specific syntax — any current 3.x works.
- Latest stable `.NET SDK`
- Docker engine running (Docker Desktop not required) — Aspire relies on it for hosting local container services
- VS Code + AI assistant
- Local SQL Server/Azure SQL access for dev scenarios
- GitHub Packages read access for the private EF.Packages NuGet feed; expose it through `NUGET_AUTH_TOKEN` or an approved credential provider before Phase 3/4 restore
- If using Uno UI:
  - `dotnet new install Uno.Templates`
  - `dotnet tool install -g uno.check` then `uno-check`
  - `dotnet tool install -g Microsoft.OpenApi.Kiota`

Version policy: prefer latest stable packages and SDKs.

### EF.Packages Feed Setup

The scaffold depends on private `EF.*` packages. Local restore requires package read access through `NUGET_AUTH_TOKEN` or an approved credential provider.

Set the token in your shell rather than committing secrets or pasting PATs into chat:

```powershell
$env:NUGET_AUTH_TOKEN = "<package-read-token>"
```

Then ensure `nuget.config` maps `EF.*` to the private feed and uses `%NUGET_AUTH_TOKEN%` for the feed password. Configure once, then verify with `dotnet restore`:

```powershell
python .instructions/scripts/configure-ef-packages-feed.py --root . --feed-url https://nuget.pkg.github.com/{owner}/index.json --username {github-user}
dotnet restore
```

Never commit a PAT. CI should inject the same token through secret variables.

## Repository Setup

1. Create a new empty app repo.
2. Install this instruction set with `python scripts/install-to-project.py --target /path/to/your-app-repo`.
3. Open the app repo in VS Code or your CLI harness.

Expected shape (note `AGENTS.md`, `.github/agents/`, and `.claude/commands/` live at the app repo root, not inside `.instructions/`):

```text
<YourApp>/
  AGENTS.md                       # CLI agents discover here; loads .instructions/START-AI.md only on explicit scaffold request
  .github/
    agents/                        # Copilot discovers scoped agents here
      dotnet-scaffold.agent.md
      vertical-slice.agent.md
  .claude/
    commands/                      # Claude discovers scoped commands here
      scaffold.md
      vertical-slice.md
  .instructions/
    README.md
    CLAUDE.md
    START-AI.md
    ai/
      SKILL.md
      domain-specification-schema.md
      resource-implementation-schema.md
      implementation-plan.md
      placeholder-tokens.md
    support/
      HANDOFF.md
      execution-gates.md
      troubleshooting.md
    skills/
    scripts/
    templates/
    schemas/
  README.md
```

Notes:
- `.instructions/support/HANDOFF.md` is the template.
- A working `HANDOFF.md` is created in the target project root at the end of every phase and updated after each Phase 5 sub-phase.

## Recommended MCP Servers

> **Prefer CLI over MCP when both exist.** CLI tools have lower token cost and faster execution. Use MCP servers for interactive exploration or when no CLI equivalent is available.

### Core (always on)

| Server | Why | CLI alternative |
|---|---|---|
| Microsoft Docs MCP | Official .NET/Azure docs, samples, full-page retrieval | - |
| Context7 MCP | Third-party library/API docs | - |

### Enable by phase

| Server | Enable when | CLI alternative |
|---|---|---|
| GitHub MCP | Repo workflows, issues/PRs, CI visibility | `gh` CLI (preferred) |
| Azure MCP | IaC/deployment/resource validation | `az` CLI (preferred) |
| Playwright MCP | UI E2E validation/debugging | - |
| Fetch MCP | Pull external specs/docs into markdown | `curl` / `Invoke-RestMethod` |
Optional additions: Git, Docker (`docker` CLI preferred), Memory, web-search MCPs, Azure DevOps MCP (`az devops` CLI preferred).

### Tooling discovery

Phase 3 analyzes `resource-implementation.yaml` technology choices and actively researches available CLIs and MCP servers for the project's specific libraries and services. Results are recorded in the implementation plan's **Tooling & Environment Readiness** section and verified at the start of each subsequent phase.

**CLI → MCP → online resources:** Prefer CLI tools first (lowest token cost), then MCP servers for interactive exploration, then documentation URLs and GitHub repos the AI can fetch during implementation.

## Where Things Live

A short map of the repo so you know which directory owns what kind of content. Useful when deciding where to put a new instruction or fix.

| Folder | Owns | Loaded when |
|---|---|---|
| Root project-memory files: `AGENTS.md` (Codex / CLI agents), `CLAUDE.md` (Claude), `.github/copilot-instructions.md` (Copilot) — three vendor-specific entrypoints, same role | Harness discovery and explicit scaffold trigger detection | Auto-loaded by the harness; must stay thin |
| Root scoped pickers: `.claude/commands/` (Claude slash commands), `.github/agents/` (Copilot agent picker) | Harness-specific scaffold launchers | Selected only for scaffold or vertical-slice work |
| `START-AI.md` in this repo / `.instructions/START-AI.md` in installed apps | Canonical bootstrap: session router, phase router, load rules, reference-app pointer | Loaded only after explicit scaffold request |
| `ai/` | Phase orchestration: schemas, interview, contract scaffolding, TDD protocol, placeholder tokens, Phase 5 file table (`SKILL.md`) | Per-phase routing |
| `skills/` | Reusable how-to per concern (domain, data, API, gateway, caching, identity, testing, UI, etc.) | Per-sub-phase, by `SKILL.md` file table |
| `templates/` | Code-shape templates for generated artifacts (entity, EF config, repository, service, endpoint, tests) | Per-sub-phase, paired with the matching skill |
| `patterns/` | Cross-project wiring (data layer, API host, infrastructure, expected output) | On-demand; index in `ai/SKILL.md` § Non-Negotiables |
| `support/` | Operator-facing detail: execution gates, operations protocols, troubleshooting, HANDOFF template, MVS, golden path, prompt catalog, reference-app proof map | On failure, on session boundary, or on need |
| `schemas/` | JSON Schemas for `domain-specification.yaml` and `resource-implementation.yaml` | Phase 1/2 validation |
| `scripts/` | Author + operator tooling: install-to-project, configure-ef-packages-feed, validate-instructions | One-off |

Rule of thumb when adding new content: it goes in `skills/` if it's "how to do X", in `templates/` if it's "the shape of the file you generate", in `patterns/` if it's "how multiple projects wire together", in `support/` if it's operator-facing, and in `ai/` only if it's phase orchestration the AI needs at session start.

## Mode Selection

| Need | Mode |
|---|---|
| Fast internal tool, minimal infra | `scaffoldMode: lite` |
| Production-ready with optional hosts | `scaffoldMode: full` |
| Smallest viable scaffold (single API, defaults to all optional hosts off) | `scaffoldMode: api-only` |

`scaffoldMode` drives load-set sizing per Phase 5 sub-phase; the optional-host toggles (`includeGateway`, `includeUnoUI`, `includeScheduler`, etc.) are independent flags in `resource-implementation.yaml` and can be enabled in any mode. `api-only` simply biases the defaults toward "off".

Defaults: [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md) **Canonical Defaults**. Load-set sizing: [ai/SKILL.md](ai/SKILL.md) § Load-Set Sizing.

## Happy Path

1. Prerequisites and repo setup (see [Quick Start](#quick-start) and [Prerequisites](#prerequisites))
2. Phase 1: Domain YAML + language + decisions → [ai/shared-understanding-interview.md](ai/shared-understanding-interview.md)
3. Phase 2: Resource YAML → [ai/resource-implementation-schema.md](ai/resource-implementation-schema.md)
4. Phase 3: Implementation plan → [ai/implementation-plan.md](ai/implementation-plan.md)
5. Phase 4: Contract scaffolding → [ai/contract-scaffolding.md](ai/contract-scaffolding.md)
6. Phase 5: Implementation (TDD) → [ai/SKILL.md](ai/SKILL.md) + [ai/tdd-protocol.md](ai/tdd-protocol.md)
7. Validate gates → [support/execution-gates.md](support/execution-gates.md)
8. Final scaffold check → [support/final-scaffold-checklist.md](support/final-scaffold-checklist.md)
9. Troubleshoot → [support/troubleshooting.md](support/troubleshooting.md)

## Prompt Catalog

For copy-paste phase prompts, see [support/prompt-catalog.md](support/prompt-catalog.md). The catalog is a convenience layer for engineers; [START-AI.md](START-AI.md) remains the canonical operational bootstrap for AI execution.

## Operational References

These references are for **maintaining and developing the instruction set itself** - not for using it to scaffold a new application. For app scaffolding, see [Quick Start](#quick-start) and [Happy Path](#happy-path).

- [START-AI.md](START-AI.md) - canonical AI bootstrap, version checks, phase routing, and load rules
- [support/prompt-catalog.md](support/prompt-catalog.md) - copy-paste prompts for starting or resuming a session
- [support/phase-1-worked-example.md](support/phase-1-worked-example.md) - condensed transcript of the TaskFlow Phase 1 interview (pacing, branch recaps, mid-interview corrections)
- [support/execution-gates.md](support/execution-gates.md) - canonical validation gates and operator setup checklist
- [support/golden-path-sample.md](support/golden-path-sample.md) - canonical small sample for regression-checking scaffold instructions
- [support/final-scaffold-checklist.md](support/final-scaffold-checklist.md) - final generated-app scaffold acceptance checklist
- [support/troubleshooting.md](support/troubleshooting.md) - failure triage and recurring issue guidance
- [support/taskflow-proof-map.md](support/taskflow-proof-map.md) - fast reference-app proof map from instruction concern to TaskFlow area
- [support/UPDATE-INSTRUCTIONS.md](support/UPDATE-INSTRUCTIONS.md) - capture improvements discovered during scaffolding

Useful script entrypoints:

- `scripts/install-to-project.py` - copy the runtime payload into a consumer app's `.instructions/` directory and place harness entrypoints at the app root. `--verify` smoke-checks the install; `--verify-only` runs the smoke check without copying.
- `scripts/configure-ef-packages-feed.py` - create/update target-app `nuget.config` for EF.Packages without writing PATs.
- `scripts/validate-instructions.py` - author-side sanity check: relative-link integrity, phase-label canonical set, harness command-file shape, payload shape vs installer declaration. Run before committing edits to instruction files.

