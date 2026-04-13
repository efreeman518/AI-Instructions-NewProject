# Prompt Catalog

Human-facing prompt catalog for starting or resuming scaffolding sessions.

Use this file as a convenience layer only. [START-AI.md](../START-AI.md) remains the canonical operational bootstrap for the AI session router, version checks, phase loading rules, and session boundaries.

## How To Use

1. Start from [START-AI.md](../START-AI.md).
2. Copy the prompt for the current phase or resume path.
3. Replace `{...}` placeholders with real values.
4. Keep the one-phase-per-session rule from [START-AI.md](../START-AI.md).

## Phase 1 — Domain Discovery (Session 1)

```text
Load .instructions/START-AI.md. This is a new project — no HANDOFF.md yet.
Run the MCP Server Check from START-AI.md — ensure Microsoft Docs and Context7 MCPs are active.
Generate a domain-specification YAML for a new application called {ProjectName}.
The business is: {one-sentence business description}.
Key entities: {entity list}.
Follow .instructions/ai/domain-specification-schema.md.
When the YAML is complete and reviewed, write HANDOFF.md to the project root and close the session.
```

## Phase 2 — Resource Definition (Session 2)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Run the MCP Server Check — ensure Microsoft Docs and Context7 MCPs are active.
Generate the resource implementation YAML per .instructions/ai/resource-implementation-schema.md.
Mode: {full|lite|api-only}. Testing profile: {minimal|balanced|comprehensive}.
Declare externalDependencyModes for every external dependency before finalizing.
When the YAML is complete and the Phase 2→3 transition gate passes, update HANDOFF.md and close the session.
```

## Phase 3 — Implementation Plan (Session 3)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Run the MCP Server Check — enable GitHub MCP and Azure MCP for this phase.
Generate an implementation plan per .instructions/ai/implementation-plan.md template.
Run Phase 3 pre-flights: NuGet feeds configured (dotnet restore exits 0), dotnet ef available.
Flag open questions before writing implementation-plan.md to the project root.
When the plan is reviewed and open questions resolved, update HANDOFF.md and close the session.
```

## Phase 4 — Contract Scaffolding (Session 4)

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Run the MCP Server Check — ensure GitHub MCP is active.
Run python scripts/get-phase-load-set.py --phase 4 --mode {full|lite|api-only} to resolve the load set.
Generate the contract scaffold per .instructions/ai/contract-scaffolding.md:
  solution structure, interfaces, DTOs, entity shells, test infrastructure, no-op DI stubs.
Gate: 'dotnet build' succeeds on the full solution including test projects.
When gate passes, set contractsScaffolded: true in HANDOFF.md and close the session.
```

## Phase 5 — Session Start

Start every Phase 5 sub-phase with:

```text
Load .instructions/START-AI.md and HANDOFF.md from the project root.
Run the MCP Server Check for this sub-phase.
```

Then append the relevant sub-phase block below.

### 5a — Foundation (TDD)

```text
Run python scripts/get-phase-load-set.py --phase 5a --mode {full|lite|api-only} to resolve the load set.
If context is tight, use --slice domain for entity/rule work or --slice repository for EF/repository work. These are curated compact bundles inside the same sub-phase; they do not create a new sub-phase.
Follow .instructions/ai/tdd-protocol.md: write domain/rule/repository tests first (red), then implement to green.
Contracts and entity shells already exist from Phase 4. Activate builders after entity logic is implemented.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Unit'. When gate passes, update HANDOFF.md (currentSubPhase: 5b) and close session.
```

### 5b — App Core (TDD)

```text
Run python scripts/get-phase-load-set.py --phase 5b --mode {full|lite|api-only}.
If context is tight, use --slice service for mapping/service work or --slice endpoint for API/exception/endpoint-test work. These are curated compact bundles inside the same sub-phase; they do not create a new sub-phase.
Follow .instructions/ai/tdd-protocol.md: write service tests (red), implement services (green), write endpoint tests (red), implement endpoints (green).
Replace no-op DI stubs with real implementations.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Unit|TestCategory=Endpoint'. When gate passes, update HANDOFF.md and close session.
```

### 5c — Runtime / Edge (Tests-After)

```text
Run python scripts/get-phase-load-set.py --phase 5c --mode {full|lite|api-only}.
Scaffold only the enabled runtime concerns: {gateway/aspire/caching/observability/security}.
After implementation, write infrastructure tests (health checks, config loading, caching).
Gate: 'dotnet build' + 'dotnet test' + app starts via Aspire. When gate passes, update HANDOFF.md and close session.
```

### 5d — Optional Hosts

```text
Run python scripts/get-phase-load-set.py --phase 5d --mode {full|lite|api-only}.
Scaffold only the enabled optional hosts: {scheduler/functionapp/notifications}.
Update hostGates in HANDOFF.md per host as each reaches scaffolded → validated.
Close session when all enabled hosts are validated (or blockers are recorded for deployment-only deps).
```

Note: Uno UI is always a dedicated session within 5d. Use the same session start prompt but scope only to Uno UI.

### 5e — Quality Gates + Delivery

```text
Run python scripts/get-phase-load-set.py --phase 5e --mode {full|lite|api-only}.
Unit/endpoint/integration tests already exist from 5a/5b/5c/5d.
Scaffold quality gate tests (architecture, load, benchmarks per profile: {minimal|balanced|comprehensive}), IaC, CI/CD, Dockerfile.
Run full regression: 'dotnet test'. Also 'az bicep build --file infra/main.bicep' (if IaC enabled). Update HANDOFF.md and close session.
```

### 5f — Authentication

```text
Run python scripts/get-phase-load-set.py --phase 5f --mode {full|lite|api-only}.
Finalize auth: replace stubs with config-driven scaffold principal ({Entra|OAuth2|social|hybrid}).
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Endpoint'. Update HANDOFF.md and close session.
```

### 5g — AI Integration

```text
Run python scripts/get-phase-load-set.py --phase 5g --mode {full|lite|api-only} {--include-ai-search} {--include-agents}.
Scaffold AI integration: {search indexing/agent service} as enabled.
Gate: 'dotnet build' + 'dotnet test --filter TestCategory=Unit'. Update HANDOFF.md and close session.
```

## Resume From HANDOFF

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
