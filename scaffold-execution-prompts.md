# Scaffold Execution Prompts

Reusable prompt snippets for AI agents performing scaffolding work. Copy-paste or reference these in agent conversations.

Use [engineer-checklist.md](engineer-checklist.md) as the single compile/run execution checklist for engineer-owned actions.

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
> - If you discover instruction gaps, append to UPDATE_INSTRUCTIONS.md
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
