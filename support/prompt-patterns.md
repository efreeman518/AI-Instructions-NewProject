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
Using the domain specification in .instructions/domain-specification.yaml, generate the resource
implementation YAML per .instructions/ai/resource-implementation-schema.md.
Mode: {full|lite|api-only}. Testing profile: {minimal|balanced|comprehensive}.
Data store: {sqlServer|cosmosDb|etc}. Include: {gateway/functions/uno-ui/scheduler as needed}.
```

## Phase 3 — Implementation Plan

```text
Using the domain spec and resource implementation, generate an implementation plan
per .instructions/ai/implementation-plan.md template. Flag any open questions before proceeding.
```

## Phase 4 — Vertical Slice

```text
Scaffold a complete vertical slice for entity {Entity}:
domain model → EF config → repositories → DTOs → mappers → service → endpoints → tests.
Follow .instructions/ai/SKILL.md sub-phase order. Validate with 'dotnet build' after each sub-phase.
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
