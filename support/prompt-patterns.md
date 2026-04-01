# Prompt Patterns

Copy-paste these prompts to start or resume a phase. Replace `{...}` with actual values.

## Phase 1 — Domain Discovery

```text
I need to define the domain for a new application called {ProjectName}.
The business is: {one-sentence business description}.
Key entities: {entity list}.
Please generate a domain-specification YAML following .instructions/ai/domain-specification-schema.md.
```

## Phase 2 — Resource Definition

```text
Using the domain specification in domain-specification.yaml (project root), generate the resource
implementation YAML per ai/resource-implementation-schema.md.
Mode: {full|lite|api-only}. Testing profile: {minimal|balanced|comprehensive}.
Data store: {sqlServer|cosmosDb|etc}. Include: {gateway/functions/uno-ui/scheduler as needed}.
```

## Phase 3 — Implementation Plan

```text
Using the domain spec and resource implementation, generate an implementation plan
per .instructions/ai/implementation-plan.md template. Flag any open questions before proceeding.
Pre-flight: confirm custom NuGet feeds are configured in nuget.config and dotnet restore exits 0.
Pre-flight: confirm dotnet ef is available (dotnet tool list -g or local tool manifest).
```

## Phase 4 — Sub-Phase Start Prompts

### 4a — Foundation

```text
Load START-AI.md. Check for HANDOFF.md in the project root.
Run ./scripts/get-phase-load-set.ps1 -Phase 4a -Mode {full|lite|api-only} to resolve the load set.
Scaffold Phase 4a: solution structure, domain model, data access, core entity/config/repository/appsettings.
Validate with 'dotnet build' before proceeding.
```

### 4b — App Core

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4b -Mode {full|lite|api-only}.
Scaffold Phase 4b: DTOs, mappers, services, endpoints, bootstrapper DI wiring.
Validate with 'dotnet build' and 'dotnet test --filter TestCategory=Endpoint'.
```

### 4c — Runtime / Edge

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4c -Mode {full|lite|api-only}.
Scaffold only the enabled runtime concerns: {gateway/aspire/caching/observability/security}.
Validate with 'dotnet build' and verify the app starts via Aspire (if enabled).
```

### 4d — Optional Hosts

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4d -Mode {full|lite|api-only}.
Scaffold only the enabled optional hosts: {scheduler/functionapp/uno-ui/notifications}.
Record per-host gate status in HANDOFF.md after each host is validated.
```

### 4e — Quality + Delivery

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4e -Mode {full|lite|api-only}.
Scaffold tests (profile: {minimal|balanced|comprehensive}), IaC, and CI/CD as in scope.
Validate with 'dotnet test' and (if IaC enabled) 'az bicep build --file infra/main.bicep'.
```

### 4f — Authentication

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4f -Mode {full|lite|api-only}.
Finalize auth: replace stubs with config-driven scaffold principal ({Entra|OAuth2|social|hybrid}).
Scaffold mode is the gate — live provider setup is supplemental and does not block completion.
Validate with 'dotnet build' and 'dotnet test --filter TestCategory=Endpoint'.
```

### 4g — AI Integration

```text
Run ./scripts/get-phase-load-set.ps1 -Phase 4g -Mode {full|lite|api-only} {-IncludeAiSearch} {-IncludeAgents}.
Scaffold AI integration: {search indexing/agent service} as enabled.
Scaffold mode is the gate — live Foundry/AI Search endpoints are deployment-only dependencies.
Validate with 'dotnet build' and 'dotnet test --filter TestCategory=Unit'.
```

## Resume from HANDOFF

```text
Read HANDOFF.md in the project root. Resume from the documented phase.
Load the files listed in Next Load Set. Check Blockers before proceeding.
```

## Add New Entity (Existing Project)

```text
Add entity {Entity} to the existing solution using .instructions/support/vertical-slice-checklist.md.
Follow the same patterns established by {ExistingEntity}.
```
