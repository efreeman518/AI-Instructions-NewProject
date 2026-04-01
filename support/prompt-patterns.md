# Prompt Patterns

Copy-paste these prompts to start a phase. Replace `{...}` with actual values.

**Session model:** Each phase (and each Phase 4 sub-phase) runs in its own AI session. At the end of each session the AI writes/updates `HANDOFF.md` in the target project root, then the session closes. The next session starts from `START-AI.md` + `HANDOFF.md` only.

---

## Phase 1 — Domain Discovery (Session 1)

```text
Load .instructions/START-AI.md. This is a new project — no HANDOFF.md yet.
Generate a domain-specification YAML for a new application called {ProjectName}.
The business is: {one-sentence business description}.
Key entities: {entity list}.
Follow .instructions/ai/domain-specification-schema.md.
When the YAML is complete and reviewed, write HANDOFF.md to the project root and close the session.
```

## Phase 2 — Resource Definition (Session 2)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Generate the resource implementation YAML per .instructions/ai/resource-implementation-schema.md.
Mode: {full|lite|api-only}. Testing profile: {minimal|balanced|comprehensive}.
Declare externalDependencyModes for every external dependency before finalizing.
When the YAML is complete and the Phase 2→3 transition gate passes, update HANDOFF.md and close the session.
```

## Phase 3 — Implementation Plan (Session 3)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Generate an implementation plan per .instructions/ai/implementation-plan.md template.
Run Phase 3 pre-flights: NuGet feeds configured (dotnet restore exits 0), dotnet ef available.
Flag open questions before writing implementation-plan.md to the project root.
When the plan is reviewed and open questions resolved, update HANDOFF.md and close the session.
```

## Phase 4 — Sub-Phase Start Prompts

Each sub-phase is its own session. Start every Phase 4 session with:

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
```

Then append the sub-phase–specific block below.

### 4a — Foundation

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4a -Mode {full|lite|api-only} to resolve the load set.
Scaffold Phase 4a: solution structure, domain model, data access, core entity/config/repository/appsettings.
Gate: 'dotnet build' passes. When gate passes, update HANDOFF.md (currentSubPhase: 4b) and close session.
```

### 4b — App Core

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4b -Mode {full|lite|api-only}.
Scaffold Phase 4b: DTOs, mappers, services, endpoints, bootstrapper DI wiring.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Endpoint'. When gate passes, update HANDOFF.md and close session.
```

### 4c — Runtime / Edge

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4c -Mode {full|lite|api-only}.
Scaffold only the enabled runtime concerns: {gateway/aspire/caching/observability/security}.
Gate: 'dotnet build' + app starts via Aspire. When gate passes, update HANDOFF.md and close session.
```

### 4d — Optional Hosts

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4d -Mode {full|lite|api-only}.
Scaffold only the enabled optional hosts: {scheduler/functionapp/notifications}.
Update hostGates in HANDOFF.md per host as each reaches scaffolded → validated.
Close session when all enabled hosts are validated (or blockers are recorded for deployment-only deps).
```

> Note: Uno UI is always a dedicated session within 4d. Use the same session start prompt but scope only to Uno UI.

### 4e — Quality + Delivery

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4e -Mode {full|lite|api-only}.
Scaffold tests (profile: {minimal|balanced|comprehensive}), IaC, and CI/CD as in scope.
Gate: 'dotnet test' passes; 'az bicep build --file infra/main.bicep' passes (if IaC enabled). Update HANDOFF.md and close session.
```

### 4f — Authentication

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4f -Mode {full|lite|api-only}.
Finalize auth: replace stubs with config-driven scaffold principal ({Entra|OAuth2|social|hybrid}).
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Endpoint'. Update HANDOFF.md and close session.
```

### 4g — AI Integration

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4g -Mode {full|lite|api-only} {-IncludeAiSearch} {-IncludeAgents}.
Scaffold AI integration: {search indexing/agent service} as enabled.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Unit'. Update HANDOFF.md and close session.
```

---

## Resume from HANDOFF

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Resume from currentSubPhase. Load the files listed in Next Load Set. Check Blockers before proceeding.
```

## Add New Entity (Existing Project)

```text
Load .instructions/START-AI.md. No HANDOFF.md needed — this is an add-entity operation.
Add entity {Entity} to the existing solution using .instructions/support/vertical-slice-checklist.md fast-path.
Follow patterns established by {ExistingEntity}.
```
