# Domain Specification Schema (Phase 1 Output)

Pure business domain model — no implementation details, no datatypes, no databases.

## Output Contract

Write Phase 1 output to `.instructions/domain-specification.yaml` by default. If another path is used, state it explicitly in handoff notes.

## Project Identity

```yaml
ProjectName: ""
ProjectDescription: ""
OrganizationName: ""       # optional namespace prefix
```

## Entities

Define what the business calls things, their lifecycle, and how they relate.

> **⚠️ Naming Conflicts:** Avoid entity names that collide with C# framework types. Common conflicts:
> - `Task` → conflicts with `System.Threading.Tasks.Task` — use `WorkItem`, `ProjectTask`, or `JobTask`
> - `Thread` → conflicts with `System.Threading.Thread` — use `Discussion`, `Conversation`
> - `Timer` → conflicts with `System.Threading.Timer` — use `Reminder`, `Schedule`
> - `Type` → conflicts with `System.Type` — use `Category`, `Classification`
> - `String`, `Object`, `Action`, `Attribute`, `File`, `Path` → all conflict with System types
>
> These collisions cause subtle compilation errors or require `global::` disambiguations throughout the codebase.

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

- `one-to-many` — parent owns children. Specify cascade behavior.
  ```yaml
  children:
    - { name: Comments, entity: Comment, relationship: one-to-many, cascadeDelete: true }
  ```
- `many-to-many` — peer association. Join entity details are Phase 2.
  ```yaml
  children:
    - { name: Tags, entity: Tag, relationship: many-to-many }
  ```
- `self-referencing` — hierarchical structures within the same entity.
  ```yaml
  children:
    - { name: Children, entity: TodoItem, relationship: self-referencing, selfReferenceKey: ParentId }
  ```
- `polymorphic-join` — shared attachment pattern across parent types.
  ```yaml
  children:
    - { name: Attachments, entity: Attachment, relationship: polymorphic-join, polymorphicEntityTypes: [TodoItem, Comment] }
  ```
- Reference navigation (no ownership):
  ```yaml
  navigation:
    - { name: Category, entity: Category, required: false }
  ```

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

Define lifecycle states and valid transitions in business terms. States = named business conditions. Transitions = allowed moves with named actions. Guards = business rules that gate transitions.

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

## Events

### Trigger Types

| Trigger | Description |
|---------|-------------|
| `afterCreate` | Raised after an entity is created |
| `afterUpdate` | Raised after any property is updated |
| `afterStatusChange` | Raised after a state machine transition completes |
| `afterAction(<ActionName>)` | Raised after a named custom domain action (e.g., `afterAction(Reschedule)`) |
| `afterDelete` | Raised after an entity is (soft-)deleted |
| `scheduled` | Emitted by a background job or scheduler on a time-based trigger |

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
  - name: TodoItemOverdueSuspected
    raisedBy: TodoItem
    trigger: scheduled
    payload: [TenantId, TodoItemId, DueDate]
  - name: TodoItemRescheduled
    raisedBy: TodoItem
    trigger: afterAction(Reschedule)
    payload: [TenantId, TodoItemId, NewDueDate]
```

## Workflows

```yaml
workflows:
  - name: TodoItemEscalation
    pattern: orchestrator
    involvedEntities: [TodoItem, Team, TeamMember, Reminder]
    steps:
      - "Check overdue items"
      - "Notify member"
      - "Escalate after threshold"
    compensationRequired: true
    compensation:
      rollbackOrder: reverse-step-order
      rules:
        - { onFailureOfStep: "Notify member", compensationAction: "Cancel queued notification" }
        - { onFailureOfStep: "Escalate after threshold", compensationAction: "Revoke escalation and reset status" }
    notes: "Thresholds configurable per tenant"
```

Skip workflows when CRUD + state transitions suffice.

### When to add workflows
- Multiple entities must coordinate in sequence
- Steps may fail and need compensation/rollback
- Async waits (human approval, external callback)
- Time-based escalation or retry logic

### What scaffolding produces from workflows
- Orchestrator service shell + step method stubs
- Compensation stubs (if `compensationRequired`)
- DI registration
- No API endpoints by default

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
authProvider: EntraID             # EntraID | EntraExternal | Google | Facebook | Apple | OAuth2 | None
authScenario: enterprise          # enterprise | external | hybrid
```

Auth provider options:
- **Enterprise / internal:** `EntraID` — SSO, conditional access, group-based roles
- **External / consumer:** `EntraExternal`, `Google`, `Facebook`, `Apple`, `OAuth2` — social/OIDC providers
- **Hybrid:** combine `EntraID` for internal with `EntraExternal` or social providers for external users

> **Note:** Authentication is configured in the final phase (Phase 4f). During earlier phases, auth is stubbed. See [skills/identity-management.md](skills/identity-management.md).

---

## Discovery Conversation Pattern

Work through these in order during Phase 1:

1. **Core entities** — what does the business call things?
2. **Relationships** — who owns what? What references what?
3. **Lifecycle** — what states does each entity go through?
4. **Rules** — what must be true? What constraints exist?
5. **Events** — what happens that other parts of the system care about?
6. **Workflows** — what multi-step processes exist beyond CRUD?
7. **Tenancy/auth** — who can see/do what?
