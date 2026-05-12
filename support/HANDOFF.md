# HANDOFF.md

Create this file in the **target project root** at the end of every phase and each Phase 5 sub-phase session. The next AI session loads `START-AI.md` + this file only — nothing else — and resumes from `currentPhase` / `currentSubPhase`.

```yaml
instructionVersion: ""
currentPhase: ""           # next phase to run: 1 | 2 | 3 | 4 | 5
currentSubPhase: ""        # next Phase 5 sub-phase to run: 5a | 5b | 5c | 5d | 5e (blank before Phase 5)
scaffoldMode: ""           # full | lite | api-only — drives load-set sizing (see ai/SKILL.md § Load-Set Sizing)
testingProfile: ""         # minimal | balanced | comprehensive
contractsScaffolded: false # set true after Phase 4 completes
enabledFeatures:
  includeApi: true
  useAspire: true
  includeGateway: false
  includeScheduler: false
  includeFunctionApp: false
  includeUnoUI: false
  includeBlazorUI: false
  includeNotifications: false
  includeIaC: true
  includeGitHubActions: false
  includeAzd: false
  includeAiServices: false
testStatus:                # updated per sub-phase — keys match TestCategory values
  unitTests: not-started   # TestCategory=Unit       — not-started | red | green
  endpointTests: not-started # TestCategory=Endpoint
  integrationTests: not-started # TestCategory=Integration (Phase 5d; Testcontainers SQL / real external services)
hostGates:                 # Phase 5c per-host status: not-started | scaffolded | partially-validated | validated | blocked
  scheduler: not-started
  functionApp: not-started
  unoUI: not-started       # always a dedicated session
  blazorUI: not-started
  notifications: not-started
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

- Keep `domain-specification.yaml`, `resource-implementation.yaml`, `UBIQUITOUS-LANGUAGE.md`, and `DESIGN-DECISIONS.md` in the target project root.
- `currentPhase` and `currentSubPhase` always describe the next work to run, not the phase just completed. Record completed gate evidence in `Completed` and `Validation`.
- At Phase 1 close, summarize unresolved/deferred design decisions and confirm they do not block Phase 2.
- Keep `enabledFeatures` flags in sync with `resource-implementation.yaml` canonical hosting/IaC/AI toggles.
- For Phase 4, set `currentPhase: 5`, `currentSubPhase: 5a`, and `contractsScaffolded: true` after the gate passes. Phase 5a/5b require this flag.
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

### Validation since the last AppHost change

Use this section for Phase 5b runtime/Aspire evidence. Leave blank when `useAspire: false`.

- AppHost changed:
- Command:
- Result:
- Evidence:
- Data-plane spot check:

### Per-Host Gate Status (Phase 5c)

For each enabled optional host, record its individual gate result. Use `validated`, `scaffolded`, `partially-validated`, or `blocked` — never claim Phase 5c complete if any enabled host is only scaffolded. Gateway is a Phase 5b runtime concern, not a Phase 5c host — record its status under § Validation, not here.

| Host | Build | Host-Specific Gate | Status | Notes |
|------|-------|--------------------|--------|-------|
| Scheduler | | | | |
| Function App | | | | |
| Uno UI | | | | |
| Blazor UI | | | | |
| Notifications | | | | |

## Scaffold Acceptance

Filled out at the end of the final enabled Phase 5 sub-phase, before closing the scaffold. Mirrors the Scaffold Definition of Done in `ai/SKILL.md`.

| Gate | Command | Result | Notes |
|------|---------|--------|-------|
| Build | `dotnet build` | | |
| Tests (all categories) | `dotnet test` | | Include passing count, ignored count, inconclusive count |
| Aspire AppHost startup | `dotnet run --project src/Host/Aspire/AppHost` | | Confirm every resource reached Running and `/healthz` returns 200 |
| Blazor host (when enabled) | `dotnet run --project src/UI/{Project}.Blazor` | | Standalone + Aspire-registered (both) |
| Uno host (when enabled) | `dotnet build src/UI/{Project}.UI -f <target>` + launch | | Per chosen platform target |
| API smoke (one entity) | curl/HTTPie/Scalar against the gateway or API | | Record discovery method, not the ephemeral URL |

### Deferred External Dependencies

For every `[Ignore]` test or `Assert.Inconclusive` branch left in the scaffold, record: what it gates, what step unblocks it, and the named test/assembly that turns green when unblocked.

| Test / Assembly | Gates | Unblocking Step | Owner |
|-----------------|-------|-----------------|-------|
| | | | |
