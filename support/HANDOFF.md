# HANDOFF.md

Create this file in the **target project root** at the end of every phase and each Phase 5 sub-phase session. The next AI session loads `START-AI.md` + this file only — nothing else — and resumes from `currentSubPhase`.

```yaml
instructionVersion: ""
currentPhase: ""           # 1 | 2 | 3 | 4 | 5
currentSubPhase: ""        # 5a | 5b | 5c | 5d | 5e (Phase 5 only)
scaffoldMode: ""           # full | lite | api-only
testingProfile: ""         # minimal | balanced | comprehensive
contractsScaffolded: false # set true after Phase 4 completes
enabledFeatures:
  includeGateway: false
  includeScheduler: false
  includeFunctionApp: false
  includeUnoUI: false
  includeBlazorUI: false
  includeNotifications: false
  includeAiServices: false
testStatus:                # updated per sub-phase
  unitTests: not-started   # not-started | red | green
  endpointTests: not-started
  infrastructureTests: not-started
hostGates:                 # Phase 5c per-host status: not-started | scaffolded | partially-validated | validated | blocked
  scheduler: not-started
  functionApp: not-started
  unoUI: not-started       # always a dedicated session
resumeCommand: ""          # exact prompt to paste at the next session start
toolingNotes: ""           # CLIs/MCPs discovered in Phase 3; note any missing or unavailable
instructionGapsPath: "INSTRUCTION-GAPS.md"
```

## Next Step

- 

## Next Load Set

Load only these files next session (do not load anything else until this list is confirmed):

- `START-AI.md`
- `HANDOFF.md`
- 

## Environment Setup

Run before `dotnet restore` in any new session:

- 

## Current Objective

- Goal:
- Scope for this session:

## Deferred

Out of scope for this session — do not attempt unless explicitly re-scoped:

- 

## Blockers

- None

## Notes

- Record any non-default paths for `domain-specification.yaml` or `resource-implementation.yaml`.
- Record any non-default paths for `UBIQUITOUS-LANGUAGE.md` or `DESIGN-DECISIONS.md`.
- At Phase 1 close, summarize unresolved/deferred design decisions and confirm they do not block Phase 2.
- Keep `enabledFeatures` flags in sync with `resource-implementation.yaml`.
- For Phase 4, set `contractsScaffolded: true` after the gate passes. Phase 5a/5b require this flag.
- For Phase 5a/5b, update `testStatus` as tests transition: `not-started` → `red` (tests written, failing) → `green` (implementation complete, tests passing). This tracks TDD progress across sessions.
- For Phase 5c (Optional Hosts), update `hostGates` per-host as each host moves through `scaffolded` → `partially-validated` → `validated` or `blocked`. Do not mark the sub-phase complete until all enabled hosts reach `validated` or have a recorded blocker.
- Note unresolved infra/auth/package-feed issues here rather than retrying them repeatedly.
- Record instruction gaps in root `INSTRUCTION-GAPS.md`, not inside `.instructions/`, during consumer app scaffolding.
- Keep entries short so the next AI turn can resume without reloading unnecessary docs.
- Verify HANDOFF.md is well-formed (correct sub-phase, gate result, next-load-set populated, blockers itemized) before ending a phase session.
- **Ephemeral URLs:** Do not record Aspire dashboard URLs, proxy ports, or host endpoints. These are assigned at runtime and change between launches. Instead, record the discovery method (e.g., "read dashboard URL from `dotnet run` output, then check resource list for host URLs").

## Residual Environment Note

Known local or CI quirks not resolved this session:

- 

## Validation Findings Resolved

Issues encountered and fixed this session (so the next session does not re-investigate):

- 

## Completed

- 

## Validation

- Command:
- Result:
- Notes:

### Per-Host Gate Status (Phase 5c)

For each enabled optional host, record its individual gate result. Use `validated`, `scaffolded`, `partially-validated`, or `blocked` — never claim Phase 5c complete if any enabled host is only scaffolded.

| Host | Build | Host-Specific Gate | Status | Notes |
|------|-------|--------------------|--------|-------|
| Scheduler | | | | |
| Function App | | | | |
| Uno UI | | | | |
| Gateway | | | | |
