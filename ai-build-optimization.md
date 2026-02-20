# AI Build Optimization Playbook

Use this guide to scaffold faster with fewer retries when building C#/.NET apps locally with Aspire and preparing for Azure deployment.

## Goals

- Keep generation deterministic and cohesive across layers
- Minimize token/context noise for the AI assistant
- Catch issues early with short build/test feedback loops
- Keep local Aspire topology aligned with Azure IaC targets

## High-Confidence Workflow

0. **Start with domain discovery**
   - Before collecting YAML inputs or generating any code, lead a collaborative conversation to explore the business domain
   - Probe entities, relationships, lifecycle states, business rules, and data store fit
   - Summarize the emerging model iteratively and get engineer confirmation
   - Generate structured domain inputs from the agreed model
   - See the **Domain Discovery Protocol** in [SKILL.md](SKILL.md) and the **Domain Discovery Prompt** in [scaffold-execution-prompts.md](scaffold-execution-prompts.md)
1. **Pin scope per turn**
   - Ask for one phase at a time (foundation, app core, runtime, delivery)
   - Explicitly state what to skip for now (e.g., no UI, no Functions yet)
2. **Provide structured inputs**
   - Include `ProjectName`, `scaffoldMode`, `testingProfile`, entity list, and enabled hosts
   - Prefer YAML from [domain-inputs.schema.md](domain-inputs.schema.md)
3. **Anchor to canonical files**
   - Always reference [SKILL.md](SKILL.md), [placeholder-tokens.md](placeholder-tokens.md), and the exact skill file being executed
4. **Generate, then validate immediately**
   - Run `dotnet build` after each phase
   - Run targeted tests before broad test suites
5. **Fail fast, flag for human** (see protocol below)
   - One fix pass for code-generation errors (usings, references, packages)
   - **Never** attempt infrastructure fixes (Docker, SQL, NuGet auth, Aspire env vars)
   - Flag unresolved issues in `HANDOFF.md` and point to [engineer-checklist.md](engineer-checklist.md)
6. **Use vertical slices for feature increments**
   - For new entities, reference [vertical-slice-checklist.md](vertical-slice-checklist.md) + `templates/`
7. **Promote complexity gradually**
   - Start with `functionProfile: starter`, `unoProfile: starter`, `testingProfile: balanced`
   - Upgrade once core slices are stable
8. **Keep Aspire and Azure in lockstep**
   - Any new runtime dependency in Aspire should be reflected in IaC and config naming

## Fail Fast, Flag for Human Protocol

The AI agent's job is **code generation**, not **infrastructure debugging**. Follow this decision tree after every `dotnet build`:

```
dotnet build
  ├── Green → continue to next phase
  └── Red → classify each error:
        ├── Code-generation issue (missing using, reference, package, DbSet, DI wiring)
        │     → Fix in ONE pass → re-run dotnet build
        │           ├── Green → continue
        │           └── Still red → flag in HANDOFF.md, continue with non-blocked phases
        └── Infrastructure issue (NuGet auth, Docker, SQL, Aspire ports, certs, tooling)
              → Immediately flag in HANDOFF.md
              → Reference relevant section of engineer-checklist.md
              → Continue with non-blocked phases
```

**Never** spend more than one fix attempt on any single error. The engineer resolves infrastructure issues using [engineer-checklist.md](engineer-checklist.md).

## Prompt Patterns

## Initial Scaffold Prompt

```text
Use the instructions in `.instructions/`.
Inputs:
- ProjectName: <Name>
- scaffoldMode: <full|lite>
- testingProfile: <minimal|balanced|comprehensive>
- includeApi/includeGateway/includeFunctionApp/includeScheduler/includeUnoUI: <values>
- customNugetFeeds: <list of {name, url} for private feeds>
- entities: <list>

Execute only these skills now:
1) solution-structure
2) domain-model
3) data-access

Constraints:
- Follow placeholder token rules from `placeholder-tokens.md`
- Do not scaffold optional hosts beyond the requested list
- Configure nuget.config with nuget.org + all custom feeds
- After adding packages, update Directory.Packages.props to latest stable versions
- Stub any external services that require credentials so the project compiles locally
- After generation, run `dotnet build` — fix code errors in one pass; flag infrastructure errors for engineer
- If you discover instruction gaps or improvements, append to UPDATE_INSTRUCTIONS.md
- When context exceeds 50% and at a good stopping point, create/update HANDOFF.md
```

## Vertical Slice Prompt

```text
Add entity `<Entity>` as a complete vertical slice.
Requirements:
- Generate domain/data/application/api artifacts + DI wiring + migration command
- Include unit + endpoint tests
- Use `vertical-slice-checklist.md` and relevant files in `templates/`
- Keep naming and paths aligned with `placeholder-tokens.md`
- Run `dotnet build` after generation — fix code errors in one pass, flag infrastructure issues
- Return a checklist of generated files and any follow-up items for the engineer
```

## Fix-Only Prompt

```text
Fix only the current build/test failures.
Do not refactor unrelated files.
Keep public contracts unchanged unless required by the errors.
After fixes, re-run the same validation command and report remaining failures.
If failures persist after one fix pass, flag them in HANDOFF.md for the engineer.
```

## Validation Cadence

- Foundation phase: `dotnet build`
- Feature slice: `dotnet build` + `dotnet test --filter "TestCategory=Unit|TestCategory=Endpoint"`
- Pre-merge baseline: `dotnet test`
- IaC checks: `az bicep build --file infra/main.bicep` *(engineer runs this — see [engineer-checklist.md](engineer-checklist.md))*

> After every validation step: if green, continue. If red after one code-fix pass, flag and move on.

## Context Control Rules

- Keep active context to the current skill + related templates only
- Avoid pasting large generated files back into prompts
- Prefer file references and short diffs over long narrative instructions
- Re-state constraints each turn: scope, non-goals, validation command, done criteria
- **Use the Phase Loading Manifest below** — only load the files listed for the current phase
- **For composite patterns, read `sampleapp-patterns.md`** — this distills all 16 cross-cutting patterns from sampleapp into one file (~500 lines)
- **Never read sampleapp `.cs` files speculatively** — only open a specific sampleapp source file as a last resort when `sampleapp-patterns.md` is insufficient and you know the exact file path
- **Use MCP servers for current docs** — When you need up-to-date API signatures, configuration patterns, or package details, use MCP lookups instead of relying on training data. Use **Microsoft Docs MCP** for .NET/Azure APIs (Aspire, EF Core, Bicep, Functions, Entra ID) and **Context7 MCP** for third-party libraries (Uno Platform, YARP, FusionCache, Kiota, TickerQ, NBomber). This is especially important for Aspire hosting packages, Uno.Extensions APIs, and Azure SDK patterns that evolve between releases.

---

## Phase Loading Manifest

Load **only** the files needed for the current phase. This prevents context window bloat.

### Always Loaded (small, essential)
- `SKILL.md` (124 lines) — orchestration entry point
- `placeholder-tokens.md` (81 lines) — token substitution rules
- `ai-build-optimization.md` (this file)
- `sampleapp-patterns.md` (~500 lines) — load specific sections on demand for composite patterns (self-referencing, polymorphic joins, Aspire wiring, etc.)

### Phase 1 — Foundation (solution-structure → domain-model → data-access)
| Load | Lines | Why |
|------|-------|-----|
| `skills/solution-structure.md` | 245 | Project layout, .csproj references, Directory.Packages.props |
| `skills/domain-model.md` | 122 | Entities, value objects, DomainResult |
| `skills/data-access.md` | 178 | DbContext, EF configs, repositories |
| `skills/package-dependencies.md` | 320 | NuGet feed setup, base class packages |
| `templates/entity-template.md` | 75 | Entity starter template |
| `templates/ef-configuration-template.md` | 63 | EF config starter |
| `templates/repository-template.md` | 134 | Repository starter |
| `domain-inputs.schema.md` | **Only the Entity & Relationship sections** (~200 of 857 lines) | |
| **Do NOT load yet** | | templates/test-*, skills/testing, skills/uno-ui, skills/iac, skills/cicd, skills/gateway, skills/function-app |

### Phase 2 — App Core (application-layer → bootstrapper → api)
| Load | Lines | Why |
|------|-------|-----|
| `skills/application-layer.md` | 150 | DTOs, mappers, services, validation |
| `skills/bootstrapper.md` | 119 | DI wiring |
| `skills/api.md` | 121 | Minimal API endpoints |
| `templates/dto-template.md` | ~60 | DTO starter |
| `templates/mapper-template.md` | 86 | Mapper starter |
| `templates/service-template.md` | 132 | Service starter |
| `templates/endpoint-template.md` | 115 | Endpoint starter |
| `quick-reference.md` | 169 | DI patterns, route conventions |
| **Unload** | | Phase 1 skill files (solution-structure, domain-model, data-access) — keep templates if re-referencing |

### Phase 3 — Edge & Runtime (gateway → aspire → optional hosts)
| Load | Lines | Why |
|------|-------|-----|
| `skills/gateway.md` | 141 | YARP config, claims, CORS |
| `skills/aspire.md` | 145 | AppHost orchestration |
| `skills/configuration.md` | 221 | appsettings, options pattern |
| `skills/multi-tenant.md` | 214 | Only if `multiTenant: true` |
| `skills/caching.md` | 208 | Only if caching enabled |
| **Unload** | | Phase 2 skill files |

### Phase 4 — Optional Workloads (load only what's enabled)
| Load if enabled | Lines | Condition |
|------|-------|-----|
| `skills/background-services.md` | 191 | `includeScheduler: true` |
| `skills/function-app.md` | 161 | `includeFunctionApp: true` |
| `skills/uno-ui.md` | 635 | `includeUnoUI: true` — **largest skill; use a dedicated session** |
| `skills/notifications.md` | 140 | If notification providers needed |
| UI templates (mvux-model, xaml-page, ui-service, ui-model) | ~480 | Only with Uno UI |

### Phase 5 — Testing & Delivery
| Load | Lines | Why |
|------|-------|-----|
| `skills/testing.md` | 445 | Test project scaffolding |
| `templates/test-template.md` | 810 | **Largest template — load only the section for current test type** |
| `skills/identity-management.md` | 294 | Auth setup |
| `skills/iac.md` | 452 | Bicep templates |
| `skills/cicd.md` | 350 | GitHub Actions |

> **Budget guideline:** Each phase should stay under ~1,500 lines of instruction context (~35K tokens), leaving room for conversation, generated code, and tool output.

---

## Session Boundary Protocol — `handoff.md`

When the context window is filling up (e.g., after completing a phase, or when the AI's responses degrade), **create a `handoff.md` file** in the project root to enable a clean session restart.

### When to Create a Handoff

- After completing any phase (Foundation, App Core, Edge, etc.)
- When the AI starts producing incomplete or repetitive output
- Before switching to a large optional workload (especially Uno UI at 635+ lines)
- When `dotnet build` succeeds and you want to checkpoint progress

### Handoff File Format

Create/overwrite `handoff.md` in the project root:

```markdown
# Session Handoff — {ProjectName}
Generated: {timestamp}

## Completed Phases
- [x] Foundation (solution-structure, domain-model, data-access)
- [x] App Core (application-layer, bootstrapper, api)
- [ ] Edge & Runtime
- [ ] Optional Workloads
- [ ] Testing & Delivery

## Domain Inputs Summary
- ProjectName: {value}
- scaffoldMode: {full|lite}
- testingProfile: {value}
- Entities: {list with status — scaffolded / pending}
- Enabled hosts: API, Gateway, Scheduler, FunctionApp, UnoUI (mark which)

## Current State
- Last successful build: {phase name}
- Pending migrations: {list or "none"}
- Known issues: {any open build/test failures}
- Files generated this session: {count or key files}

## Next Steps
- Resume with Phase {N}: {phase name}
- Load these files: {list from Phase Loading Manifest}
- First action: {specific next task}

## Key Decisions Made
- {Any architecture decisions, naming choices, or deviations from defaults}
```

### Resume Prompt (for new session)

```text
Read `handoff.md` and `.instructions/SKILL.md`.
Resume scaffolding from where the last session left off.
Load only the files listed in the "Next Steps" section.
Do not regenerate completed phases — validate with `dotnet build` first, then continue.
```

### Compact Prompt (to create handoff mid-session)

```text
Create a `handoff.md` in the project root summarizing:
- Which phases and entities are complete
- Current build/test status
- What to do next
Use the format from `.instructions/ai-build-optimization.md` Session Boundary Protocol.
```

### Auto-Trigger Rule

> **When session context is over 50% and work is at a good stopping point, the AI should proactively create/update `HANDOFF.md` without being asked.** This ensures the developer can validate progress and a new session can continue seamlessly.

---

## Large File Strategies

Some files are too large to load whole. Use these strategies:

| File | Lines | Strategy |
|------|-------|----------|
| `domain-inputs.schema.md` | 857 | Load only the sections relevant to current entities. Skip relationship modeling examples if relationships are simple. |
| `templates/test-template.md` | 810 | Load only the test type section needed (Unit OR Integration OR E2E — not all at once). |
| `skills/uno-ui.md` | 635 | Dedicate a separate session. Load after backend phases are complete and `handoff.md` exists. |
| `skills/iac.md` | 452 | Load only in Phase 5. Skip if not deploying yet. |
| `skills/testing.md` | 445 | Load only the profile section matching `testingProfile` (minimal/balanced/comprehensive). |
| `sampleapp/` source | ~5,000+ | **Never bulk-read.** Only open a specific file when a skill says "see sampleapp/src/.../File.cs". |

---

## Done Criteria (per increment)

- Builds successfully (or infrastructure blockers flagged in `HANDOFF.md`)
- Required tests pass for current scope
- New files follow naming/token conventions
- DI wiring and endpoint registration are complete
- For infrastructure changes: Aspire references, config keys, and IaC names are consistent
- **Handoff file is current** (update after each completed phase)
- **Unresolved issues reference [engineer-checklist.md](engineer-checklist.md)** for the human to action

---

## Instruction Maintenance — `UPDATE_INSTRUCTIONS.md`

During implementation, the AI will often discover gaps, ambiguities, or better approaches that aren't captured in the current instruction/skill/template files. **Create or append to `UPDATE_INSTRUCTIONS.md`** in the project root to capture these findings for the instruction maintenance agent.

### When to Write

- A pattern from a skill file didn't compile or required undocumented workarounds
- A template was missing a common case (e.g., a property type, relationship pattern, or configuration)
- A NuGet package API changed from what the instructions describe
- A better default or convention was discovered during scaffolding
- An Aspire hosting package or emulator was needed but not mentioned in the skills
- External service stubbing required a pattern not documented in `configuration.md`

### Format

```markdown
# UPDATE_INSTRUCTIONS.md
Generated/updated: {timestamp}

## Findings

### {Finding Title}
- **File(s) to update:** `skills/{file}.md`, `templates/{file}.md`
- **Current behavior:** {what the instructions say or omit}
- **Recommended change:** {what should be added/changed}
- **Reason:** {why this improves future scaffolding}
- **Priority:** low | medium | high
```

### Rules

- **Append, don't overwrite** — each session adds findings; don't clear previous entries.
- Keep findings actionable — reference specific file paths and section names.
- The instruction maintenance agent reads this file and applies approved changes to the baseline instructions.
- Do NOT modify instruction files directly during scaffolding — capture findings here for review.

---

## Session Context Management — HANDOFF.md

When session context usage is **over 50% and steps are at a good stopping point**, create or update `HANDOFF.md` in the project root. This enables the next new session to continue the mission.

### When to Create/Update

- Session context window is estimated to be over 50% consumed
- A logical phase or set of vertical slices has been completed
- Before switching to a large workload (Uno UI, IaC, comprehensive testing)
- When `dotnet build` succeeds and a checkpoint is appropriate
- The AI starts producing incomplete, repetitive, or degraded output

### HANDOFF.md Format
