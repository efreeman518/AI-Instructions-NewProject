# Domain Definition Schema (Phase 1 Output)

Pure business domain model — no implementation details, no datatypes, no databases.

## Output Contract

Write Phase 1 output to `.instructions/domain-definition.yaml` by default. If another path is used, state it explicitly in handoff notes.

## Project Identity

```yaml
ProjectName: ""
ProjectDescription: ""
OrganizationName: ""       # optional namespace prefix
```

## Entities

Define what the business calls things, their lifecycle, and how they relate.

```yaml
entities:
  - name: TodoItem
    description: "A unit of work assigned to a team member"
    isTenantEntity: true
    properties:
      - { name: Title, required: true, description: "Short summary" }
      - { name: Description, required: false }
      - { name: DueDate, required: false }
      - { name: Priority, kind: enum, values: [Low, Medium, High, Critical] }
      - { name: Status, kind: flags_enum, values: [None, IsStarted, IsCompleted] }
    children:
      - { name: Comments, entity: Comment, relationship: one-to-many, cascadeDelete: true }
      - { name: Tags, entity: Tag, relationship: many-to-many }
    navigation:
      - { name: Category, entity: Category, required: false }
      - { name: AssignedTo, entity: TeamMember, required: false }
```

### Property Rules

- `kind`: `string` (default) | `enum` | `flags_enum` | `number` | `date` | `boolean` | `identifier` | `money` | `text`
- No `type`, `maxLength`, `precision` here — those are Phase 2 concerns
- `required`: business requirement, not database nullability
- For operational reason fields (`ReasonCode` style), prefer:
  - fixed, stable set -> `enum`
  - evolving/managed set -> dedicated catalog entity (for localization/versioning)

### Relationship Types

- `one-to-many` — parent owns children
- `many-to-many` — peer association
- `self-referencing` — hierarchical
- `polymorphic-join` — shared attachment pattern

Design guidance + YAML examples: [domain-design-guide.md](domain-design-guide.md).

## Business Rules

```yaml
entities:
  - name: TodoItem
    rules:
      - { name: TitleRequired, condition: "Title must not be empty", errorMessage: "Title is required." }
      - { name: DueDateFuture, condition: "DueDate must be in the future when set", errorMessage: "Due date must be future." }

domainRules:
  - { name: TenantQuotaNotExceeded, appliesTo: [TodoItem], dependsOn: [TenantQuotaPolicy], errorMessage: "Tenant quota exceeded." }
```

Rules use business language here. Exact C# conditions are Phase 3/4 concerns.

### Policy Matrix (Optional)

Use for actor/state dependent outcomes that are difficult to represent as isolated rules.

```yaml
policyMatrices:
  - name: CancellationPolicyMatrix
    dimensions: [RequestedByRole, CurrentStatus]
    outputs: [Allowed, FeePolicy, RefundPolicy]
    rows:
      - { RequestedByRole: Customer, CurrentStatus: Placed, Allowed: true, FeePolicy: None, RefundPolicy: Full }
      - { RequestedByRole: Customer, CurrentStatus: InTransit, Allowed: true, FeePolicy: Partial, RefundPolicy: Partial }
```

## State Machines

```yaml
entities:
  - name: TodoItem
    stateMachine:
      field: Status
      initial: None
      states: [None, InProgress, Completed, Cancelled]
      transitions:
        - { from: None, to: InProgress, action: Start }
    customActions:
      - { name: Reschedule, params: [{ name: NewDueDate }] }
```

Design guidance: [domain-design-guide.md](domain-design-guide.md#state-machine-design).

## Events

```yaml
events:
  - name: TodoItemCreated
    raisedBy: TodoItem
    trigger: afterCreate
    payload: [TenantId, TodoItemId, Title]
  - name: TodoItemCompleted
    raisedBy: TodoItem
    trigger: afterStatusChange
    payload: [TenantId, TodoItemId, CompletedBy]
```

## Workflows

```yaml
workflows:
  - name: TodoItemEscalation
    pattern: orchestrator
    involvedEntities: [TodoItem, Team, TeamMember, Reminder]
    steps: ["Check overdue items", "Notify member", "Escalate after threshold"]
    compensationRequired: false
    compensation:
      rollbackOrder: reverse-step-order
      rules:
        - { onFailureOfStep: "Notify member", compensationAction: "Revoke pending notification" }
    notes: "Thresholds configurable per tenant"
```

Skip workflows when CRUD + state transitions suffice. Design guidance: [domain-design-guide.md](domain-design-guide.md#workflow-design).

### Ingestion Semantics (Optional)

For event/time-series workflows, capture business-level ordering and lateness expectations.

```yaml
ingestionSemantics:
  eventTimePolicy: event-time
  orderingExpectation: per-entity-ordered
  allowedLateness: "PT10M"
  outOfOrderHandling: reconcile-window
```

### Entitlement Policy (Optional)

Use when multiple grant sources (tier/purchase/promo) must be combined deterministically.

```yaml
entitlementPolicy:
  sourcePriority: [Tier, Purchase, Promo]
  conflictResolution: highest-priority-wins
  revokeBehavior: source-scoped-revocation
```

### Content Lifecycle Policy (Optional)

Use for publish/schedule/rollback scenarios.

```yaml
contentLifecyclePolicy:
  supportsDraftSnapshot: true
  supportsPublishedSnapshot: true
  rollbackStrategy: prior-published-version
  scheduledPublishIdempotent: true
```

### UGC Lifecycle Policy (Optional)

Use for comments/favorites and moderation-sensitive interactions.

```yaml
ugcLifecyclePolicy:
  moderationMode: post-moderation
  visibilityStates: [Visible, Hidden, Removed]
  softDeleteEnabled: true
  authorRedactionSupported: true
```

## Tenancy & Auth Model

```yaml
multiTenant: true
tenantIsolation: "row-level"     # row-level | schema | database
globalAdminRole: GlobalAdmin
authProvider: EntraID             # EntraID | EntraExternal | None
```
