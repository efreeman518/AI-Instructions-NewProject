# Implementation Plan (Phase 3 Output)

Produced after Phase 1 (domain definition) and Phase 2 (resource mapping) are complete. This is the bridge between design and code.

**Create this file in the target project root as `implementation-plan.md`.**

## Template

```markdown
# Implementation Plan — {{ProjectName}}

## Inputs Summary

- Domain specification: `.instructions/domain-specification.yaml` (or inline reference)
- Resource mapping: `.instructions/resource-implementation.yaml` (or inline reference)
- Mode: {{scaffoldMode}} | Testing: {{testingProfile}}
- Enabled hosts: {{list}}

## Implementation Steps

### Phase 4a — Foundation
- [ ] Solution structure + .slnx + Directory.Packages.props
- [ ] Domain model entities + enums + value objects
- [ ] EF configurations per entity (using Phase 2 datatypes)
- [ ] Repository interfaces + implementations
- [ ] Domain rules (co-located in Domain.Model/Rules/) + state machines
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
- [ ] IaC templates
- [ ] CI/CD pipeline
- [ ] **Checkpoint:** full test suite passes

### Phase 4f — Authentication (Final)
- [ ] Prompt for identity provider scenario (see options below)
- [ ] Replace auth stubs with real identity configuration
- [ ] Wire auth middleware, token validation, and role/scope checks
- [ ] Update appsettings with provider-specific configuration
- [ ] **Checkpoint:** authenticated endpoints respond correctly

**Identity Provider Options:**
- **Enterprise / internal users:** Microsoft Entra ID — SSO, conditional access, group-based roles
- **External / consumer users:** Microsoft Entra External ID, Google, Facebook, Apple, OAuth2/OIDC
- **Hybrid:** Entra ID for internal + Entra External ID or social providers for external users

### Phase 4g — AI Integration (if `includeAiServices: true`)
- [ ] `Infrastructure.AI` project with search/agent service interfaces
- [ ] Azure AI Search index definitions + client wiring (if search configured)
- [ ] Embedding pipeline: on-write handler (domain event → vectorize → index) or batch job
- [ ] Agent service scaffolding (Microsoft Agent Framework `ChatClientAgent` / `FoundryAgent`)
- [ ] Agent function tools wrapping existing `I{Entity}Service` domain operations
- [ ] Agent middleware (logging, auth context propagation, content safety)
- [ ] Multi-agent workflow with executors + edges (if `workflow.enabled: true`)
- [ ] Aspire resource wiring (`AddAzureAISearch()`, `AddAzureOpenAI()`)
- [ ] Bootstrapper DI registration for AI services
- [ ] API endpoints for search + agent interactions
- [ ] Configuration: Foundry endpoint, model deployment names, search index names in appsettings
- [ ] **Checkpoint:** search returns results, agent responds to test prompt

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

---

## Phase 3 → 4 Pre-Flight

Before starting Phase 4 implementation, verify all of the following:

- [ ] `nuget.config` validated (`dotnet restore` exits 0)
- [ ] All open questions resolved or explicitly deferred with TODO
- [ ] `scaffoldMode`, `testingProfile`, and all host flags confirmed
- [ ] Custom NuGet feed URLs and auth configured (if any)
- [ ] Domain specification and resource implementation YAML files are complete
- [ ] Implementation plan reviewed and approved by human
