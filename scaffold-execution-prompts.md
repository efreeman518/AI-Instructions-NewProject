# Scaffold Execution Prompts

Reusable prompt snippets for AI agents performing scaffolding work. Copy-paste or reference these in agent conversations.

Use [engineer-checklist.md](engineer-checklist.md) as the single compile/run execution checklist for engineer-owned actions.

---

## Domain Discovery Prompt

Run this prompt **before** any scaffolding work. The goal is to explore the business domain collaboratively and arrive at a well-thought-out entity model.

### Prompt

> I want to build a new application for **[brief description of the business domain]**.
>
> Before we scaffold anything, let's think through the domain together. Here's what I know so far:
> - [List your initial entities, concepts, or business processes]
> - [Any known constraints: multi-tenant, specific data stores, scale expectations]
>
> Please help me explore:
> 1. **Entity discovery** — What are the core entities? What properties, states, and lifecycle do they have?
> 2. **Relationships** — How do entities relate? Where are the aggregate boundaries?
> 3. **Business rules** — What invariants and validation rules apply?
> 4. **Data store choices** — Which entities fit SQL, Cosmos DB, Table Storage, or Blob Storage?
> 5. **AI services** — Could any entity benefit from semantic search via vector indexing (Azure SQL, Cosmos DB, Azure AI Search)? Are there opportunities for AI agent or multi-agent workflows (Microsoft Foundry, Microsoft Agent Framework)?
> 6. **Anything I'm missing** — Edge cases, future growth, common patterns for this type of domain
>
> Summarize the emerging model as we go so I can react and refine it. When we're both happy with the design, generate the YAML domain inputs.

### Expected Agent Behavior

- Lead with open-ended questions to understand the business context and goals.
- For each entity, probe: identity, lifecycle states, ownership (tenant-scoped?), cardinality, and natural keys.
- Challenge oversimplifications (e.g., "Is this really one-to-many, or could it be many-to-many?").
- Suggest patterns the engineer may not have considered (flags enums instead of booleans, value objects, embedded documents).
- Proactively explore AI service opportunities: vector indexing for semantic search, AI agent workflows for complex decision-making or classification, and multi-agent orchestration for cross-cutting processes.
- Summarize the model iteratively after each topic area — present a compact table of entities, properties, and relationships.
- Know when to stop — once 3-8 core entities are well-defined, propose transitioning to structured inputs.
- Generate the YAML domain inputs based on the agreed model and confirm before proceeding to scaffolding.

---

## Environment Preflight

Run this prompt before starting any major scaffolding phase. It validates the environment is ready and reports blockers.

### Prompt

> Before starting the next scaffolding phase, run these checks:
>
> 1. **Restore/build/unit loop:** Run `dotnet restore`, `dotnet build`, and `dotnet test --filter "TestCategory=Unit"`.
> 2. **NuGet feed check:** Confirm `nuget.config` includes `nuget.org` and all custom feeds from `customNugetFeeds`.
> 3. **External service stubs:** Confirm external services that require credentials are stubbed for local dev.
> 4. **Report:** List any blockers found.
> 5. **If blockers are infrastructure-related** (Docker, SQL connectivity, Aspire env vars, NuGet auth), **flag them for the engineer** via `HANDOFF.md` and reference [engineer-checklist.md](engineer-checklist.md). Do not attempt deep infrastructure troubleshooting.
> 6. **Proceed only if build is green** (or the user explicitly overrides a blocker).

### Expected Agent Behavior

- If build passes: report "Preflight green — proceeding with phase work."
- If build fails with code-generation issues (missing usings, references, packages): fix them in one pass and re-check.
- If build fails with infrastructure issues: flag for engineer and proceed with non-blocked phases.
- Keep troubleshooting output brief; execution specifics belong in [engineer-checklist.md](engineer-checklist.md).

---

## Post-Phase Verification

Run after completing a scaffolding phase to confirm stability before moving on.

### Prompt

> Phase work is complete. Run post-phase verification:
>
> 1. `dotnet build` — zero errors
> 2. `dotnet test --filter "TestCategory=Unit"` — all pass
> 3. If integration tests exist: `dotnet test --filter "TestCategory=Integration"` — all pass
> 4. Report results and confirm ready for the next phase.
> 5. If any failures remain after one fix attempt, add them to `HANDOFF.md` for the engineer.

---

## Initial Scaffold Prompt

> Use the instructions in `.instructions/`.
> Inputs:
> - ProjectName: <Name>
> - scaffoldMode: <full|lite>
> - testingProfile: <minimal|balanced|comprehensive>
> - includeApi/includeGateway/includeFunctionApp/includeScheduler/includeUnoUI: <values>
> - customNugetFeeds: <list of {name, url} for private feeds>
> - entities: <list>
>
> Execute only these skills now:
> 1) solution-structure
> 2) domain-model
> 3) data-access
>
> Constraints:
> - Follow placeholder token rules from `placeholder-tokens.md`
> - Do not scaffold optional hosts beyond the requested list
> - Configure nuget.config with nuget.org + all custom feeds
> - After adding packages, update Directory.Packages.props to latest stable versions
> - Stub any external services that require credentials so the project compiles locally
> - After generation, run `dotnet build` — if code-level errors, fix in one pass; if infrastructure errors, flag for engineer
> - **Before starting, search for the latest MCP servers** relevant to this phase's libraries/services. Suggest any useful new MCPs to the engineer and log findings in UPDATE-INSTRUCTIONS.md.
> - If you discover instruction gaps, append to UPDATE-INSTRUCTIONS.md
> - When context exceeds 50% and at a good stopping point, create/update HANDOFF.md

---

## Vertical Slice Prompt

> Add entity `<Entity>` as a complete vertical slice.
> Requirements:
> - Generate domain/data/application/api artifacts + DI wiring + migration command
> - Include unit + endpoint tests
> - Use `vertical-slice-checklist.md` and relevant files in `templates/`
> - Keep naming and paths aligned with `placeholder-tokens.md`
> - Run `dotnet build` after generation — fix code errors in one pass, flag infrastructure issues
> - Return a checklist of generated files and any follow-up items for the engineer

---

## Fix-Only Prompt

> Fix only the current build/test failures.
> Do not refactor unrelated files.
> Keep public contracts unchanged unless required by the errors.
> After fixes, re-run the same validation command and report remaining failures.
> If failures persist after one fix pass, flag them in HANDOFF.md for the engineer.
