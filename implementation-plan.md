# Implementation Plan (Phase 3 Output)

Produced after Phase 1 (domain definition) and Phase 2 (resource mapping) are complete. This is the bridge between design and code.

**Create this file in the target project root as `implementation-plan.md`.**

## Template

```markdown
# Implementation Plan — {{ProjectName}}

## Inputs Summary

- Domain definition: `.instructions/domain-definition.yaml` (or inline reference)
- Resource mapping: `.instructions/resource-implementation.yaml` (or inline reference)
- Mode: {{scaffoldMode}} | Testing: {{testingProfile}}
- Enabled hosts: {{list}}

## Implementation Steps

### Phase 4a — Foundation
- [ ] Solution structure + .slnx + Directory.Packages.props
- [ ] Domain model entities + enums + value objects
- [ ] EF configurations per entity (using Phase 2 datatypes)
- [ ] Repository interfaces + implementations
- [ ] Domain rules + state machines
- [ ] **Checkpoint:** `dotnet build` passes

### Phase 4b — App Core
- [ ] DTOs + mappers
- [ ] Application services
- [ ] Message handlers (if events defined)
- [ ] Bootstrapper DI wiring
- [ ] API endpoints
- [ ] **Checkpoint:** `dotnet build` passes, endpoint smoke test

### Phase 4c — Runtime/Edge
- [ ] Gateway (if enabled)
- [ ] Aspire orchestration (if enabled)
- [ ] Configuration + appsettings
- [ ] Multi-tenant middleware (if enabled)
- [ ] Caching (if enabled)
- [ ] **Checkpoint:** app starts via Aspire, basic request succeeds

### Phase 4d — Optional Hosts
- [ ] Background services / scheduler (if enabled)
- [ ] Function app (if enabled)
- [ ] Uno UI (if enabled; dedicated session preferred)
- [ ] Notifications (if enabled)
- [ ] **Checkpoint:** `dotnet build`, optional host responds

### Phase 4e — Quality + Delivery
- [ ] Unit tests per testing profile
- [ ] Integration tests
- [ ] Architecture tests (if enabled)
- [ ] Identity management wiring
- [ ] IaC templates
- [ ] CI/CD pipeline
- [ ] **Checkpoint:** full test suite passes

## Open Questions

Resolve before Phase 4 starts:

1. _[list any unresolved design decisions]_
2. _[ambiguous requirements]_
3. _[external dependency unknowns]_

## Decisions Log

| # | Decision | Rationale |
|---|---|---|
| 1 | _e.g., SQL for Orders, CosmosDB for ActivityLog_ | _Relational joins needed for Orders; ActivityLog is append-only_ |

## Risk / Blockers

| Risk | Mitigation |
|---|---|
| _e.g., Private NuGet feed access_ | _Engineer to configure feed auth before Phase 4_ |
```

## Usage

1. AI fills in the template based on Phase 1 + Phase 2 outputs
2. Human reviews, resolves open questions, confirms decisions
3. Phase 4 implementation follows the step order above
4. Check off items as completed during implementation
