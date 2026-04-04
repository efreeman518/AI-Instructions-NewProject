# HANDOFF.md

Create this file in the **target project root** at the end of every phase and each Phase 4 sub-phase session. The next AI session loads `START-AI.md` + this file only — nothing else — and resumes from `currentSubPhase`.

```yaml
instructionVersion: ""
currentPhase: ""           # 1 | 2 | 3 | 4
currentSubPhase: ""        # 4a | 4b | 4c | 4d | 4e | 4f | 4g (Phase 4 only)
scaffoldMode: ""           # full | lite | api-only
testingProfile: ""         # minimal | balanced | comprehensive
enabledFeatures:
  includeGateway: false
  includeScheduler: false
  includeFunctionApp: false
  includeUnoUI: false
  includeAiServices: false
hostGates:                 # Phase 4d per-host status: not-started | scaffolded | validated
  scheduler: not-started
  functionApp: not-started
  unoUI: not-started       # always a dedicated session
resumeCommand: ""          # exact prompt to paste at the next session start
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
- Keep `enabledFeatures` flags in sync with `resource-implementation.yaml`.
- For Phase 4d, update `hostGates` per-host as each host moves through scaffolded → validated. Do not mark the sub-phase complete until all enabled hosts reach `validated`.
- Note unresolved infra/auth/package-feed issues here rather than retrying them repeatedly.
- Keep entries short so the next AI turn can resume without reloading unnecessary docs.
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

### Per-Host Gate Status (Phase 4d)

For each enabled optional host, record its individual gate result. Use "complete", "scaffolded", or "partially validated" — never claim Phase 4d complete if any enabled host is only scaffolded.

| Host | Build | Host-Specific Gate | Status | Notes |
|------|-------|--------------------|--------|-------|
| Scheduler | | | | |
| Function App | | | | |
| Uno UI | | | | |
| Gateway | | | | |
