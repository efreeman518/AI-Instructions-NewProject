# Domain Definition Schema (Phase 1 Output)

Pure business domain model — no implementation details, no datatypes, no databases.

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
    notes: "Thresholds configurable per tenant"
```

Skip workflows when CRUD + state transitions suffice. Design guidance: [domain-design-guide.md](domain-design-guide.md#workflow-design).

## Tenancy & Auth Model

```yaml
multiTenant: true
tenantIsolation: "row-level"     # row-level | schema | database
globalAdminRole: GlobalAdmin
authProvider: EntraID             # EntraID | EntraExternal | None
```
