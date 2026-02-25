# Domain Design Guide (Phase 1 Reference)

Guidance for domain discovery: relationship patterns, state modeling, workflow design. Use during Phase 1 to produce [domain-definition-schema.md](domain-definition-schema.md).

For data store selection, datatypes, and EF configuration → [resource-implementation-schema.md](resource-implementation-schema.md) (Phase 2).

---

## Relationship Modeling

### One-to-many
Parent owns children. Specify cascade behavior.
```yaml
children:
  - { name: Comments, entity: Comment, relationship: one-to-many, cascadeDelete: true }
```

### Reference navigation
Entity references another without ownership.
```yaml
navigation:
  - { name: Category, entity: Category, required: false }
```

### Many-to-many
Peer association. Join entity details are a Phase 2 concern.
```yaml
children:
  - { name: Tags, entity: Tag, relationship: many-to-many }
```

### Self-referencing
Hierarchical structures within the same entity.
```yaml
children:
  - { name: Children, entity: TodoItem, relationship: self-referencing, selfReferenceKey: ParentId }
```

### Polymorphic join
Shared attachment/comment pattern across multiple parent types.
```yaml
children:
  - { name: Attachments, entity: Attachment, relationship: polymorphic-join, polymorphicEntityTypes: [TodoItem, Comment] }
```

---

## State Machine Design

Define lifecycle states and valid transitions in business terms.

- **States** = named business conditions (not database values)
- **Transitions** = allowed moves with named actions
- **Guards** = business rules that must be true for a transition (expressed as rules)

```yaml
stateMachine:
  field: Status
  initial: None
  states: [None, InProgress, Completed, Cancelled]
  transitions:
    - { from: None, to: InProgress, action: Start }
    - { from: InProgress, to: Completed, action: Complete }
    - { from: InProgress, to: Cancelled, action: Cancel }
    - { from: Cancelled, to: None, action: Reopen }
```

---

## Business Rules

Express rules in business language. Implementation conditions are Phase 3/4.

- **Entity rules** — validated on the entity itself
- **Domain rules** — cross-entity or require external dependencies
- **Transition guards** — rules that gate state transitions

### Policy Matrix Rules

Use a policy matrix when decisions depend on multiple dimensions (for example actor role + entity status).

```yaml
policyMatrices:
  - name: CancellationPolicy
    dimensions: [RequestedByRole, CurrentStatus]
    outputs: [Allowed, FeePolicy, RefundPolicy]
```

---

## Workflow Design

Complex workflows go beyond CRUD + state transitions. Use when you need multi-entity coordination, retries/compensation, async wait states, or approval chains.

### When to add workflows

- Multiple entities must coordinate in sequence
- Steps may fail and need compensation/rollback
- Async waits (human approval, external callback)
- Time-based escalation or retry logic

### When workflows are NOT needed

CRUD + state transitions fully express the behavior.

### Workflow definition

```yaml
workflows:
  - name: TodoItemEscalation
    pattern: orchestrator
    involvedEntities: [TodoItem, Team, TeamMember, Reminder]
    steps:
      - "Check overdue items"
      - "Notify assigned member"
      - "Escalate to lead after threshold"
    compensationRequired: true
    compensation:
      rollbackOrder: reverse-step-order
      rules:
        - { onFailureOfStep: "Notify assigned member", compensationAction: "Cancel queued notification" }
        - { onFailureOfStep: "Escalate to lead after threshold", compensationAction: "Revoke escalation and reset status" }
    notes: "Thresholds configurable per tenant"
```

### Event-Time Semantics (for ingest workflows)

For event-driven or telemetry-heavy domains, define:

- ordering expectations (global, per-entity, per-partition)
- allowed lateness/window
- out-of-order reconciliation behavior

### What scaffolding produces from workflows

- Orchestrator service shell + step method stubs
- Optional compensation stubs
- DI registration
- No API endpoints by default

### Content Composition / Publishing Guidance

For playlist-driven pages:

- model ordered blocks with explicit `Position`
- use discriminator-style block typing (text/image/video)
- define publish lifecycle (draft snapshot, published snapshot, rollback target)

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