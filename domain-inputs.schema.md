# Domain Inputs Schema

> **Start with a conversation, not a form.** Before filling out these inputs, use the **Domain Discovery Protocol** (see [SKILL.md](SKILL.md)) to have a collaborative conversation with the AI about your business domain. The AI will help you think through entities, relationships, business rules, and data store choices — then generate these inputs for you based on the agreed model.

Before scaffolding a new project, gather these inputs from the user. Items marked **required** must be provided; others have sensible defaults.

## Project Identity

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `ProjectName` | Yes | — | PascalCase name (e.g., `Inventory`, `Billing`). Used as root namespace and solution name. |
| `ProjectDescription` | No | — | Short description for README |
| `OrganizationName` | No | — | Org/company name for namespaces if different from project name |
| `scaffoldMode` | No | `full` | `full` (all skills) or `lite` (minimal clean architecture — no gateway, multi-tenant, caching, UI, Aspire). See SKILL.md for details. |

## Domain Entities

Provide a list of entities. For each entity:

```yaml
entities:
  - name: TodoItem                   # PascalCase entity name
    dataStore: sql                   # sql (default) | cosmosdb | table | blob
    isTenantEntity: true             # implements ITenantEntity<Guid>?
    partitionKeyProperty: TenantId   # cosmosdb/table only — property used as partition key
    properties:                      # scalar properties (Id, RowVersion are automatic)
      - name: Title
        type: string
        maxLength: 200
        required: true
      - name: Description
        type: string
        maxLength: 2000
        required: false
      - name: Priority
        type: int
        required: true
      - name: Status
        type: flags_enum              # generates a [Flags] enum in Domain.Shared
        values: [None, IsStarted, IsCompleted, IsBlocked, IsCancelled]
    children:                         # child collections (generates join entities, updaters, etc.)
      - name: Tags
        entity: Tag
        relationship: many-to-many    # many-to-many | one-to-many | polymorphic-join
        joinEntity: TodoItemTag       # for many-to-many, the join entity name
      - name: Comments
        entity: Comment
        relationship: one-to-many
        cascadeDelete: true
    embedded:                          # cosmosdb only — nested objects stored inside the document
      - name: Schedule                 # becomes a C# class serialized as nested JSON
        properties:
          - { name: StartDate, type: DateTimeOffset?, maxLength: null }
          - { name: DueDate, type: DateTimeOffset?, maxLength: null }
      - name: Preferences              # can be a dictionary / dynamic bag
        type: dictionary               # dictionary | object | list
        keyType: string
        valueType: string
      - name: ChecklistItems           # embedded collection (array of objects in the document)
        type: list
        properties:
          - { name: TodoItemId, type: Guid, required: true }
          - { name: Description, type: string, maxLength: 500 }
          - { name: IsComplete, type: bool, required: true }
          - { name: SortOrder, type: int, required: true }
    navigation:                       # navigation properties (non-collection)
      - name: Category
        entity: Category
        required: false
        deleteRestrict: true          # OnDelete(DeleteBehavior.Restrict)
```

### Data Store Selection Guide

Use `dataStore` on each entity to pick the right storage engine. If omitted, `sql` is assumed. The table below maps data characteristics to the best-fit store — use it when discussing entity design with the user or when the AI needs to recommend a store.

| Signal / Characteristic | → `sql` | → `cosmosdb` | → `table` | → `blob` |
|------------------------|---------|-------------|----------|---------|
| **Structure** | Fixed schema, normalized, relational | Semi-structured / variable schema, document-oriented | Flat key-value rows, no nesting | Unstructured binary/text, large payloads |
| **Relationships** | FK constraints, joins, many-to-many via join tables | Denormalized aggregates, embedded child arrays, no cross-document joins | None — partition key + row key lookup only | None — objects addressed by container/name |
| **Query patterns** | Complex filters, aggregations, GROUP BY, window functions, cross-entity joins | Point reads by id + partition key, single-partition queries, change feed | Point reads by partition + row key, simple filters on partition | Get/put by name, list with prefix, no query |
| **Consistency** | ACID transactions across tables | Transactional batch within a single logical partition | Single-entity atomic operations | Optimistic concurrency via ETag / lease |
| **Typical size per record** | Rows < 8 KB, total DB up to TBs | Documents up to 2 MB, collections unbounded | Entities up to 1 MB per row | Objects up to ~200 GB (block blob) |
| **Scale model** | Vertical (scale-up) + read replicas | Horizontal (partition key), auto-scale RU/s | Horizontal (partition key), throughput-based | Horizontal, tiered (Hot/Cool/Archive) |

#### When to Choose Each Store

**SQL (`dataStore: sql`)** — the default. Use for:
- Core business entities with referential integrity (TodoItem → Comments → Tags)
- Data that participates in complex queries, reports, or cross-entity transactions
- Entities with many-to-many or polymorphic relationships
- Anything requiring ACID transactions spanning multiple entity types
- *Examples:* TodoItem, Category, Team, TeamMember, Reminder

**Cosmos DB (`dataStore: cosmosdb`)** — use for:
- Entities with variable/evolving schema (e.g., device telemetry, form submissions with dynamic fields)
- **Documents with deep/nested JSON structure** — embedded objects, arrays of child objects, dictionaries, and mixed-depth hierarchies that would require multiple tables and JOINs in SQL but are naturally represented as a single document
- Read-heavy workloads with known access patterns (always query by tenant + entity id)
- Data that naturally forms self-contained aggregates (a todo item with embedded checklist items, a schedule object, a preferences dictionary — all in one document)
- Global distribution requirements or extreme horizontal scale
- Event sourcing or change-feed-driven architectures
- *Examples:* TodoItem profiles with nested schedule + arbitrary preferences, IoT telemetry with variable sensor payloads, team dashboards with embedded member summaries, todo items with embedded checklist items, form submissions with dynamic field structures, configuration documents with nested sections
- *Key decision:* If you never need cross-entity JOINs and can identify a good partition key, Cosmos DB may outperform SQL at scale. If the data has deep nesting that would normalize into 3+ SQL tables, that's a strong Cosmos DB signal

**Table Storage (`dataStore: table`)** — use for:
- Simple key-value lookup data (partition key + row key → entity)
- Append-heavy, rarely updated data with no relational needs
- High-volume, low-cost storage where query flexibility is not required
- *Examples:* Audit logs, configuration lookup tables, feature flags, rate-limit counters, denormalized read projections
- *Key decision:* If you only ever read by known keys and never need JOINs, filtering, or sorting beyond partition/row key, Table Storage is the cheapest option

**Blob Storage (`dataStore: blob`)** — use for:
- Binary files, documents, images, videos, exports, backups
- Large text payloads (JSON/XML/CSV) that don't need per-field querying
- Entities that are really "file metadata + file content" (store metadata in SQL, content in Blob)
- *Examples:* File attachments, report exports, user-uploaded documents, email templates
- *Key decision:* Blob Storage is not a database — if you need to search *within* the content, store searchable metadata in SQL/Cosmos and the raw file in Blob

#### Hybrid Patterns

Many real-world entities span multiple stores. Common combinations:

| Pattern | SQL | Cosmos DB | Table | Blob | Example |
|---------|-----|-----------|-------|------|---------|
| **Structured + Attachments** | Entity metadata, FKs | — | — | File content | TodoItem with uploaded Attachment document |
| **Relational + Audit Trail** | Core entity CRUD | — | Append-only change log | — | Patient record with immutable audit history |
| **Relational + Read Cache** | Source of truth | Denormalized read model (change feed from SQL via events) | — | — | Category catalog: SQL for writes, Cosmos for fast reads |
| **Relational + Telemetry** | Device/sensor registration | Time-series telemetry data | — | Raw data exports | IoT platform |
| **Search + Binary** | Searchable metadata | — | — | Raw documents | Document management system |

> For hybrid patterns, model the primary entity with `dataStore: sql` (or cosmosdb) and reference the secondary store through a service-layer abstraction. For example, a `TodoItem` entity (`dataStore: sql`) with file attachments uses `IBlobRepository` in the `TodoItemService` — the blob interaction is in the service, not on the entity itself.

#### Decision Flowchart (Simplified)

```
Is the data binary/file content?
  → Yes → blob
  → No ↓
Does the data have fixed schema + relational constraints + complex queries?
  → Yes → sql
  → No ↓
Is it simple key-value lookup (partition + row key)?
  → Yes → table
  → No ↓
Is the schema variable, or does it form self-contained aggregates?
  → Yes → cosmosdb
  → Otherwise → sql (safe default)

Does the data need semantic search / similarity matching?
  → If entity is already in sql or cosmosdb and search is secondary → use in-database vector indexing
  → If search is a primary feature or spans multiple data sources → azureAiSearch
```

## Relationship Types

| Type | Description | Example |
|------|-------------|---------|
| `one-to-many` | Parent owns collection of children | TodoItem → Comments |
| `many-to-many` | Via explicit join entity (no EF implicit) | TodoItem → TodoItemTag → Tag |
| `polymorphic-join` | Join entity with EntityId + EntityType for shared tables | TodoItem/Comment → Attachment |
| `self-referencing` | Entity relates to itself via ParentId or join entity | TodoItem → TodoItem (ParentId) |

### Relationship Modeling Guide

When defining relationships in domain inputs, include enough detail for the AI to generate correct EF configurations. The sections below show the EF configuration pattern for each relationship type, based on real examples from the sample solution.

#### One-to-Many (with back-navigation)

Parent owns a collection. Child has a FK back to parent. Use `cascadeDelete: true` for owned children.

```yaml
children:
  - name: Comments
    entity: Comment
    relationship: one-to-many
    cascadeDelete: true
```

**EF Configuration:**
```csharp
// In TodoItemConfiguration : EntityBaseConfiguration<TodoItem>
builder.HasMany(e => e.Comments)
    .WithOne()
    .HasForeignKey("TodoItemId")
    .OnDelete(DeleteBehavior.Cascade);
```

#### One-to-Many (reference, no back-navigation)

Entity references a lookup/type entity. No back-navigation collection on the referenced entity. Use `deleteRestrict: true` to prevent orphans.

```yaml
navigation:
  - name: Category
    entity: Category
    required: false
    deleteRestrict: true
```

**EF Configuration:**
```csharp
// In TodoItemConfiguration : EntityBaseConfiguration<TodoItem>
builder.HasOne<Category>()
    .WithMany(e => e.TodoItems)
    .HasForeignKey(e => e.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);
```

#### Many-to-Many (explicit join entity)

Always use an explicit join entity — never EF implicit many-to-many. The join entity can carry extra payload (e.g., `AppliedAt`).

```yaml
children:
  - name: Tags
    entity: Tag
    relationship: many-to-many
    joinEntity: TodoItemTag
    joinProperties:
      - { name: AppliedAt, type: DateTimeOffset }
```

**EF Configuration:**
```csharp
// In TodoItemTagConfiguration (composite key entity — no EntityBase)
builder.HasKey(e => new { e.TodoItemId, e.TagId });

builder.HasOne(e => e.TodoItem)
    .WithMany(e => e.TodoItemTags)
    .HasForeignKey(e => e.TodoItemId);

builder.HasOne(e => e.Tag)
    .WithMany(e => e.TodoItemTags)
    .HasForeignKey(e => e.TagId);

// Composite clustered index for fast lookups
builder.HasIndex(e => new { e.TodoItemId, e.TagId })
    .IsClustered();
```

#### Self-Referencing (direct FK)

Entity relates to itself through a nullable FK back to the same table. Use `Restrict` delete to prevent cascading cycles.

```yaml
children:
  - name: Children
    entity: TodoItem
    relationship: self-referencing
    selfReferenceKey: ParentId
```

**EF Configuration:**
```csharp
// In TodoItemConfiguration : EntityBaseConfiguration<TodoItem>
builder.HasOne(e => e.Parent)
    .WithMany(e => e.Children)
    .HasForeignKey(e => e.ParentId)
    .OnDelete(DeleteBehavior.Restrict);
```

#### Polymorphic Join (EntityType + EntityId discriminator)

A shared table (e.g., `EntityTag`, `EntityEmail`) stores data for multiple entity types, discriminated by an `EntityType` string column and `EntityId` GUID. Use a check constraint to enforce valid entity types.

```yaml
children:
  - name: Attachments
    entity: Attachment
    relationship: polymorphic-join
    joinEntity: Attachment
    polymorphicEntityTypes: [TodoItem, Comment]
```

**EF Configuration:**
```csharp
// In AttachmentConfiguration : EntityBaseConfiguration<Attachment>
builder.Property(e => e.EntityType)
    .HasConversion<string>()
    .HasMaxLength(50)
    .IsRequired();

builder.Property(e => e.EntityId)
    .IsRequired();

builder.HasIndex(e => new { e.EntityType, e.EntityId });

// Enforce valid discriminator values
builder.HasCheckConstraint(
    "CK_Attachment_EntityType",
    "[EntityType] IN ('TodoItem', 'Comment')");
```

> **Note:** Polymorphic joins do NOT use EF navigation properties to the parent entity. The service layer resolves the parent type at runtime based on `EntityType`.

## Domain Events

Define internal events that flow through `IInternalMessageBus`. Each event generates an event DTO in `Application.Contracts/Events/` and a handler in `Application.MessageHandlers/`. Handlers are auto-registered by `IInternalMessageBus.AutoRegisterHandlers()` in Bootstrapper.

```yaml
events:
  - name: TodoItemCreated                   # PascalCase event name → generates TodoItemCreated.cs + TodoItemCreatedHandler.cs
    raisedBy: TodoItem                       # entity or service that publishes the event
    trigger: afterCreate                     # afterCreate | afterUpdate | afterDelete | afterStatusChange | custom
    payload:                                 # event-specific properties (Id is automatic)
      - { name: TenantId, type: Guid? }
      - { name: Title, type: string }
      - { name: Priority, type: int }
      - { name: CategoryId, type: Guid? }
      - { name: AssignedToId, type: Guid? }
    handlers:                                # one handler class per entry
      - name: RecordTodoItemHistory
        description: "Create audit trail entry for the new todo item"
        dependsOn: [ITodoItemHistoryRepository]
      - name: NotifyAssignee
        description: "Send notification to assigned team member"
        dependsOn: [INotificationService]

  - name: TodoItemCompleted
    raisedBy: TodoItem
    trigger: afterStatusChange               # triggered by state machine transition
    transitionFrom: IsStarted
    transitionTo: IsCompleted
    payload:
      - { name: TenantId, type: Guid }
      - { name: Title, type: string }
      - { name: CompletedBy, type: string }
    handlers:
      - name: NotifyTodoItemCompleted
        description: "Notify team that todo item is completed"
        dependsOn: [INotificationService]

  - name: ReminderRescheduled
    raisedBy: ReminderService                # raised from a custom action, not entity lifecycle
    trigger: custom
    payload:
      - { name: ReminderId, type: Guid }
      - { name: RescheduledDateTime, type: DateTimeOffset }
    handlers:
      - name: ProcessReschedule
        description: "Update the scheduled reminder job"
        dependsOn: [IReminderService]
```

### Event Trigger Types

| Trigger | When | Typical use |
|---------|------|-------------|
| `afterCreate` | After entity is persisted via `Create()` factory | Welcome emails, provisioning |
| `afterUpdate` | After entity update is saved | Sync downstream, audit |
| `afterDelete` | After entity is soft/hard deleted | Cleanup, notifications |
| `afterStatusChange` | After a state machine transition completes | Workflow progression, notifications |
| `custom` | Manually published by a service method | Ad-hoc business events, cross-service coordination |

### Event Generation Rules

- Event DTOs implement `IMessage` with a static `Create()` factory that validates required fields.
- Handlers implement `IMessageHandler<TEvent>` and are decorated with `[ScopedMessageHandler]`.
- If `dependsOn` lists services, they are injected via primary constructor.
- Events with `trigger: afterStatusChange` reference a state machine transition — the AI auto-wires the publish call inside the generated transition method.
- If no `handlers` are listed, the event DTO is still generated (handlers can be added later).

## Domain Rules

Domain rules encode business invariants using the **Specification pattern**. Each rule generates a `RuleBase<T>` class in `Domain.Rules/` that returns `DomainResult` for railway-oriented error handling. Rules are called by application services before state transitions or persistence.

Rules can be declared at the entity level (inline) or in a standalone section for cross-entity rules.

### Inline (per entity)

```yaml
entities:
  - name: TodoItem
    rules:
      - name: TitleRequired
        condition: "!string.IsNullOrWhiteSpace(Title)"
        errorMessage: "Todo item title is required."
      - name: PriorityInRange
        condition: "Priority >= 1 && Priority <= 5"
        errorMessage: "Priority must be between 1 and 5."
      - name: CannotBeCompletedAndBlocked
        condition: "!(Status.HasFlag(IsCompleted) && Status.HasFlag(IsBlocked))"
        errorMessage: "A todo item cannot be both Completed and Blocked."
```

### Standalone (cross-entity or complex)

```yaml
domainRules:
  - name: TenantQuotaNotExceeded
    appliesTo: [TodoItem, Team]              # multiple entities share this rule
    description: "Tenant cannot exceed maximum entity count per plan"
    dependsOn: [ITenantService]              # requires injected service context
    errorMessage: "Tenant has reached the maximum allowed {Entity} count."
  - name: CannotDeleteCategoryWithTodoItems
    appliesTo: [Category]
    condition: "entity.TodoItems.Count == 0"
    errorMessage: "Cannot delete a category that still has todo items."
```

### Rule Generation Rules

- Each rule generates a class inheriting `RuleBase<{Entity}>` with `IsSatisfiedBy()` override.
- `condition` is a human-readable expression — the AI translates it to C# in `IsSatisfiedBy()`.
- Rules with `dependsOn` generate a class that accepts dependencies via constructor (not static).
- Inline entity rules are called in the entity's service before `Create()` / `Update()` persistence.
- Standalone `domainRules` are called in relevant services and may span multiple entity types.

## Entity State Machines

For entities with lifecycle states, define a state machine. This generates:
- A `[Flags]` enum or plain enum in `Domain.Shared`
- Transition methods on the entity with guard validation
- Events raised on transitions (if `raisesEvent` is specified)
- Service methods per action
- API endpoints per action (e.g., `POST /api/v1/orders/{id}/approve`)

State machines are defined inline on the entity:

```yaml
entities:
  - name: TodoItem
    isTenantEntity: true
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
      - { name: Priority, type: int, required: true }
    stateMachine:
      field: Status                           # property that holds the state (generated as enum)
      initial: None                           # default state on Create()
      states: [None, InProgress, Blocked, Completed, Cancelled]
      transitions:
        - from: None
          to: InProgress
          action: Start                       # generates StartAsync() on service + POST endpoint
        - from: InProgress
          to: Completed
          action: Complete
          raisesEvent: TodoItemCompleted       # references event defined in events section
          guardRule: AllSubtasksComplete        # optional — must pass this domain rule first
        - from: InProgress
          to: Blocked
          action: Block
        - from: Blocked
          to: InProgress
          action: Unblock
        - from: InProgress
          to: Cancelled
          action: Cancel
        - from: [None, InProgress, Blocked]    # multiple source states
          to: Cancelled
          action: Cancel
```

### State Machine Generation Rules

- `field` generates a `{Entity}Status` enum in `Domain.Shared/Enums/`.
- `initial` sets the default value in the `Create()` factory.
- Each `transition` generates:
  - A method on the entity: `public DomainResult {Action}() { ... }` that checks current state and sets the new state.
  - A service method: `public async Task<Result<{Entity}Dto>> {Action}Async(Guid id, CancellationToken ct)` that loads the entity, calls the transition, publishes any event, and saves.
  - An API endpoint: `POST /api/v{version}/{entities}/{id}/{action}` (lowercased action).
- `guardRule` references a domain rule that must pass before the transition is allowed.
- `raisesEvent` references an event in the `events` section. The service calls `messageBus.PublishAsync()` after successful transition.
- Transitions with `from: [...]` (array) generate a single action method that checks against multiple valid source states.

## Custom Actions

For entity-specific operations that go beyond CRUD and state transitions (e.g., Reschedule, Clone, Merge), define custom actions on the entity:

```yaml
entities:
  - name: Reminder
    customActions:
      - name: Reschedule
        params:
          - { name: NewRemindAt, type: DateTimeOffset }
        raisesEvent: ReminderRescheduled
        description: "Move the reminder to a new date/time"
      - name: Deactivate
        description: "Deactivate this reminder so it no longer fires"
  - name: TodoItem
    customActions:
      - name: Reassign
        params:
          - { name: AssignedToId, type: Guid }
          - { name: Notes, type: string? }
        description: "Reassign this todo item to a different team member"
      - name: Clone
        returns: TodoItemDto                   # non-void return
        description: "Create a copy of this todo item with a new ID"
```

### Custom Action Generation Rules

- Each action generates:
  - A domain method (if stateless): `public DomainResult {Action}({Params}) { ... }` on the entity.
  - A service method: `public async Task<Result<{Entity}Dto>> {Action}Async(Guid id, {Params}, CancellationToken ct)`.
  - An API endpoint: `POST /api/v{version}/{entities}/{id}/{action}` (lowercased action).
  - A request DTO (if `params` has entries): `{Action}{Entity}Request` record.
- If `raisesEvent` is set, the service publishes the event after successful execution.
- If `returns` is specified, the service returns that type; otherwise returns the entity DTO.
- Custom actions are not state transitions — they don't change the state machine field. Use `stateMachine.transitions` for state changes.

## Workflows (Guidance — Not Code-Generated)

> **Complex workflows are specific to each use case and are not code-generated from schema inputs.** The sections above (events, state machines, custom actions, scheduled jobs, domain rules) provide the building blocks — workflows compose them in application code.

Entity state machines handle **single-entity lifecycle** well (e.g., `TodoItem: None → InProgress → Completed`). However, real business workflows often span **multiple entities, external service calls, decision branches, retries, compensation, async waits, and human intervention** — e.g., "validate team capacity across TeamMembers, send Reminder notifications, escalate blocked TodoItems, handle failures with audit history recording." This level of orchestration is inherently custom and should not be captured declaratively.

### How to describe workflows to the AI

Provide an optional `workflows` section as **descriptive hints** for the AI. These are not code-generating — they give the AI enough context to scaffold an orchestrator service class with the right dependencies, method stubs, and TODO placeholders. The developer fills in the actual business logic.

```yaml
workflows:
  - name: TodoItemEscalation
    description: |
      1. TodoItem blocked for >48h → check if assigned team member is active
      2. If assignee inactive → reassign to team lead via Team.Members
      3. Send Reminder notification to new assignee
      4. If still blocked after 72h → escalate via notification to all team admins
      5. Record all actions in TodoItemHistory
      6. If manually unblocked → clear escalation reminders, resume normal flow
    involvedEntities: [TodoItem, Team, TeamMember, Reminder, TodoItemHistory]
    pattern: orchestrator                    # orchestrator | choreography
    compensationRequired: false
    notes: "Escalation thresholds are configurable per tenant"
```

### What the AI generates from workflow hints

- An `{WorkflowName}Orchestrator` service class (e.g., `OrderFulfillmentOrchestrator`) with:
  - Injected dependencies for all `involvedEntities` services
  - Method stubs for each numbered step with `// TODO:` comments
  - Compensation methods if `compensationRequired: true`
- Registration in Bootstrapper DI
- **No** API endpoints — orchestrators are invoked from event handlers, custom actions, or scheduled jobs

### When workflows are NOT needed

If your domain is primarily CRUD with simple lifecycle transitions, you don't need this section. The entity state machines, events, and handlers are sufficient. Use workflows only when:

- Multiple entities must be coordinated in a specific sequence
- External service calls introduce failure/retry concerns
- Decision branches determine which entities to create or update
- Compensation (rollback) logic is required on failure
- Steps may be asynchronous (waiting for webhooks, human approval, etc.)

## Multi-Tenancy

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `multiTenant` | No | `true` | Enable tenant isolation |
| `tenantIdType` | No | `Guid` | Type of TenantId |
| `globalAdminRole` | No | `GlobalAdmin` | Role name that bypasses tenant filters |

## Authentication

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `authProvider` | No | `EntraID` | `EntraID`, `EntraExternal` (B2C replacement), or `None` |
| `gatewayAuth` | No | `EntraExternal` | Auth at gateway level (user-facing) |
| `apiAuth` | No | `EntraID` | Auth at API level (service-to-service) |
| `tokenRelay` | No | `true` | Gateway acquires service token for downstream calls |

## Package Feeds

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `customNugetFeeds` | **Yes — if private packages are used** | `[]` | List of custom/private NuGet package feed URLs. These are added to `nuget.config` alongside `nuget.org` so the solution can restore and compile. Specify all feeds up front — the implementation cannot restore packages from feeds it doesn't know about. |
| `updateToLatestNugets` | No | `true` | After adding new package references, always update to the latest stable versions from `nuget.org` and any custom feeds. Ensures the project starts with current dependencies. |

### Example

```yaml
customNugetFeeds:
  - name: "CompanyFeed"
    url: "https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json"
  - name: "InternalPackages"
    url: "https://nuget.mycompany.com/v3/index.json"
updateToLatestNugets: true
```

### Feed Rules

- `nuget.config` **must** include `nuget.org` plus all feeds listed in `customNugetFeeds`.
- After adding any new `<PackageReference>`, run `dotnet restore` to verify all packages resolve.
- After restore succeeds, update to the latest stable versions: `dotnet outdated --upgrade` or manually update `Directory.Packages.props`.
- If a feed requires authentication (e.g., Azure DevOps), the `nuget.config` should include a `<packageSourceCredentials>` section or the user must configure a personal access token via `dotnet nuget update source`.

## Infrastructure

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `database` | No | `AzureSQL` | `AzureSQL`, `SQLServer` |
| `caching` | No | `FusionCache+Redis` | `FusionCache+Redis`, `DistributedMemory`, `None` |
| `useAspire` | No | `true` | .NET Aspire orchestration |
| `deployTarget` | No | `ContainerApps` | `ContainerApps`, `AppService`, `AKS` |
| `includeKeyVault` | No | `false` | Runtime secrets/keys via `IKeyVaultManager`; also enables field-level encryption via `IKeyVaultCryptoUtility` |
| `includeGrpc` | No | `false` | gRPC service-to-service communication with error interceptors |
| `externalApis` | No | `[]` | List of external API integrations — each generates an `Infrastructure.{ServiceName}` project with Refit + resilience. See External APIs section below. |

## Infrastructure as Code (IaC)

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `includeIaC` | No | `true` | Generate Azure Bicep templates in `infra/` |
| `azureRegion` | No | `eastus2` | Primary Azure region for resource deployment |
| `iacEnvironments` | No | `[dev, staging, prod]` | List of environments to generate `.bicepparam` files for |
| `includeGitHubActions` | No | `false` | Generate GitHub Actions workflow for CI/CD deployment |
| `includeAzd` | No | `false` | Generate `azure.yaml` for Azure Developer CLI (`azd up`) |
| `usePrivateEndpoints` | No | `false` | Enable private endpoints for SQL, Redis, Key Vault in prod |

## AI Services

Use when the domain conversation identifies opportunities for semantic search, AI-powered decision-making, or agent-based workflows.

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `includeAiServices` | No | `false` | Enable AI service infrastructure (model clients, vector indexing support) |
| `aiSearchProvider` | No | `none` | Vector search backend: `none`, `azureSql`, `cosmosdb`, `azureAiSearch`. Use `azureSql` or `cosmosdb` for in-database vector indexing; use `azureAiSearch` for a dedicated search index with advanced ranking, hybrid search, and large-scale vector workloads. |
| `aiAgentFramework` | No | `none` | Agent framework: `none`, `agentFramework`, `microsoftFoundry`. Use `agentFramework` for in-code agent orchestration via **Microsoft Agent Framework** (successor to Semantic Kernel + AutoGen) with tools, plugins, and multi-agent handoff; use `microsoftFoundry` for managed model endpoints, prompt management, and Foundry Agent Service. Both can be combined. |
| `includeMultiAgent` | No | `false` | Enable multi-agent orchestration patterns (requires `aiAgentFramework` to be set). Scaffolds an agent orchestrator with handoff, planning, and tool-use patterns. |

### Vector Search Configuration

When `aiSearchProvider` is set, define which entities participate in vector indexing:

```yaml
aiServices:
  aiSearchProvider: azureAiSearch
  vectorEntities:
    - entity: TodoItem
      searchableFields: [Title, Description]          # text fields to embed
      embeddingModel: text-embedding-3-small          # OpenAI / Azure OpenAI embedding model
      dimensions: 1536                                 # embedding vector dimensions
      indexName: todoitems-index                       # Azure AI Search index name (azureAiSearch only)
      hybridSearch: true                               # combine vector + keyword search (azureAiSearch only)
    - entity: Comment
      searchableFields: [Text]
      embeddingModel: text-embedding-3-small
      dimensions: 1536
      indexName: comments-index
```

#### When to Choose Each Vector Search Backend

| Signal | → `azureSql` | → `cosmosdb` | → `azureAiSearch` |
|--------|-------------|-------------|-------------------|
| **Entity already in SQL** | Entity is `dataStore: sql` and search is a secondary need | — | Complex ranking, facets, or large corpus |
| **Entity already in Cosmos DB** | — | Entity is `dataStore: cosmosdb`, search queries align with partition key patterns | Cross-partition semantic search, hybrid ranking |
| **Dedicated search experience** | — | — | Search is a primary feature; need advanced relevance, filters, hybrid (vector + BM25), semantic ranker |
| **Scale** | < 1M vectors per table | < 1M vectors per container | Millions of vectors with low latency |
| **Operational simplicity** | Built-in; no extra resource | Built-in; no extra resource | Separate Azure resource; more knobs, more power |

> **Decision shortcut:** If the entity already lives in Azure SQL or Cosmos DB and search is secondary, use in-database vector indexing to avoid provisioning another resource. If search is a core feature or spans multiple entities/data sources, use Azure AI Search.

### Agent Workflow Configuration

When `aiAgentFramework` is set, define agent capabilities:

```yaml
aiServices:
  aiAgentFramework: agentFramework
  includeMultiAgent: true
  agents:
    - name: TriageAgent
      description: "Classifies incoming todo items by priority and routes to the appropriate team"
      modelEndpoint: gpt-4o                           # Azure OpenAI deployment name
      tools: [TodoItemClassifier, PriorityScorer, SearchTodoItems, GetTeamHistory]
    - name: AssignmentAgent
      description: "Suggests optimal team member assignment based on workload and expertise"
      modelEndpoint: gpt-4o
      tools: [TeamMemberMatcher, WorkloadAnalyzer]
    - name: OrchestratorAgent
      description: "Coordinates multi-agent handoff for complex task management workflows"
      modelEndpoint: gpt-4o
      delegates: [TriageAgent, AssignmentAgent]        # agents this orchestrator can delegate to
```

### AI Services Generation Rules

- When `aiSearchProvider` is set:
  - Generates an `Infrastructure.AiSearch` project (or adds vector support to existing data access projects for in-database providers).
  - For `azureAiSearch`: scaffolds `I{Entity}SearchService`, index schema, indexer pipeline, and Aspire/IaC wiring for the Azure AI Search resource.
  - For `azureSql` / `cosmosdb`: adds vector columns/properties to existing entity configurations and generates embedding-aware repository extensions.
  - Generates an `IEmbeddingService` abstraction with an Azure OpenAI implementation for computing embeddings.
- When `aiAgentFramework` is set:
  - Generates an `Infrastructure.AiAgents` project with agent definitions, tool stubs, and DI registration.
  - For `agentFramework`: scaffolds `AgentBuilder` configuration, tool classes, and multi-agent handoff patterns using Microsoft Agent Framework (`Microsoft.Extensions.Agents` packages).
  - For `microsoftFoundry`: scaffolds Foundry Agent Service client, managed endpoint configuration, and prompt template management.
  - For `includeMultiAgent: true`: generates an orchestrator pattern with agent handoff, shared context, and conversation management.
- AI service configuration (model endpoints, API keys) follows the same pattern as other infrastructure: `appsettings.json` for non-secret config, Key Vault for secrets, Aspire resource wiring.
- All AI service dependencies are stubbed for local development (mock embedding service, in-memory vector store) so the project compiles and runs without live Azure AI resources.

## Deployable Projects

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `includeApi` | No | `true` | REST API project |
| `includeGateway` | No | `true` | YARP gateway project |
| `includeFunctionApp` | No | `false` | Azure Functions project |
| `includeScheduler` | No | `false` | Background scheduler project |
| `includeBlazorUI` | No | `false` | Blazor WASM UI project |
| `includeUnoUI` | No | `false` | Uno Platform cross-platform UI (Web, Android, iOS, macOS, Windows, Linux) |

## Testing

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `testingProfile` | No | `balanced` (`full`) / `minimal` (`lite`) | `minimal`, `balanced`, `comprehensive` — controls initial test project footprint |
| `includeArchitectureTests` | No | `false` | Include `Test.Architecture` (NetArchTest) in initial scaffold |
| `includeE2ETests` | No | `false` | Include browser UI tests (`Test.PlaywrightUI`) |
| `includeLoadTests` | No | `false` | Include `Test.Load` (NBomber) |
| `includeBenchmarkTests` | No | `false` | Include `Test.Benchmarks` (BenchmarkDotNet) |

> For new solutions, start with `minimal` or `balanced` and add the rest as quality gates when vertical slices stabilize.

## External APIs

Use when the solution needs to call third-party or partner REST APIs. Each external API generates a dedicated `Infrastructure.{ServiceName}` project with Refit interface, request/response models, auth handler, and resilience configuration.

```yaml
externalApis:
  - name: Stripe                          # PascalCase — becomes Infrastructure.Stripe
    baseUrl: "https://api.stripe.com"     # provider's base URL
    authScheme: apiKey                     # apiKey | oauth | bearer | custom
    description: "Payment processing"     # brief purpose
    operations:                            # optional — scaffolds Refit methods + DTOs
      - name: CreateCharge
        method: POST
        path: "/v1/charges"
        requestType: CreateChargeRequest
        responseType: ChargeResponse
      - name: GetCharge
        method: GET
        path: "/v1/charges/{chargeId}"
        responseType: ChargeResponse
  - name: ShipStation
    baseUrl: "https://ssapi.shipstation.com"
    authScheme: apiKey
    description: "Shipping label generation and tracking"
```

### External API Generation Rules

- Each entry generates an `Infrastructure.{name}` project containing: `I{Name}Api` (Refit interface), `{Name}Service` (wrapper implementing application-layer interface), `{Name}Settings`, `{Name}AuthHandler`, and `Models/` folder.
- `authScheme` determines the `DelegatingHandler` pattern: `apiKey` → header injection, `oauth` → client-credentials token relay, `bearer` → static token, `custom` → placeholder handler.
- `operations` are optional; if omitted, a placeholder Refit interface is scaffolded for the developer to fill in.
- Each `operation` generates a Refit method on `I{Name}Api` and request/response DTOs in `Models/`.
- Resilience (retry + circuit breaker + timeout) is configured in `ServiceCollectionExtensions.cs` using `Microsoft.Extensions.Http.Resilience`.
- See [skills/external-api.md](skills/external-api.md) for implementation patterns.

## Messaging

Use when the project needs asynchronous messaging between services. Each messaging channel generates a sender/publisher interface, concrete implementation, DI registration, and optionally a background-service processor.

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `messagingProviders` | No | `[]` | List of providers: `serviceBus`, `eventGrid`, `eventHub` |

### Messaging Channel Definitions

Optionally provide detailed channel definitions to scaffold concrete senders/receivers:

```yaml
messagingChannels:
  - name: TodoItemEvents
    provider: serviceBus               # serviceBus | eventGrid | eventHub
    direction: send                     # send | receive | both
    topicOrQueue: todoitem-events       # queue/topic name (Service Bus) or topic name (Event Grid/Hub)
    subscriptionName: api-handler       # Service Bus subscription (receive/both only)
    messageType: TodoItemEventMessage   # DTO type published/consumed
    description: "Publish todo item lifecycle events to downstream services"
  - name: ReminderNotifications
    provider: serviceBus
    direction: receive
    topicOrQueue: reminder-notifications
    subscriptionName: reminder-processor
    messageType: ReminderNotificationMessage
    description: "Consume reminder-triggered notification messages"
  - name: AuditLog
    provider: eventHub
    direction: send
    topicOrQueue: audit-stream
    messageType: AuditEntry
    description: "Stream audit entries to Event Hub for analytics"
```

### Messaging Generation Rules

- Each channel generates:
  - `I{Name}Sender` / `I{Name}Publisher` interface in `Application.Contracts/Services/`
  - Concrete implementation in `Infrastructure.Repositories/` inheriting from the appropriate `EF.Messaging` base class
  - DI registration in `Bootstrapper`
  - For `direction: receive` or `both`: a `BackgroundService` processor in `TaskFlow.BackgroundServices`
- See [skills/messaging.md](skills/messaging.md) for implementation patterns and base class APIs
- `messageType` generates a DTO record in `Application.Models/`

## Scheduler (TickerQ)

Use when `includeScheduler: true`.

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `schedulerEngine` | No | `TickerQ` | Scheduler engine. Current supported value: `TickerQ` |
| `schedulerUsePersistence` | No | `true` | Persist scheduled jobs to SQL via `SchedulerDbContext` |
| `schedulerGenerateDeploymentScript` | No | `false` | Generate `TickerQ_Deployment.sql` on startup for schema deployment |
| `schedulerReplicaCount` | No | `1` | Replica count for scheduler host (keep `1` unless Redis coordination is explicitly enabled) |

### Scheduled Jobs

Define the jobs the scheduler should run. Each job generates a `{JobName}Handler` (testable business logic) and a TickerQ adapter in `Jobs/`. If no jobs are listed, the scheduler host is scaffolded with a placeholder sample job.

```yaml
scheduledJobs:
  - name: SyncOverdueTodoItems
    cron: "0 */2 * * *"                     # every 2 hours
    description: "Flag todo items past their due date as overdue"
    dependsOn: [ITodoItemService]            # services injected into handler
  - name: NightlyTaskReport
    cron: "0 3 * * *"                        # daily at 3 AM
    description: "Generate daily task summary and email to team admins"
    dependsOn: [ITodoItemService, INotificationService]
  - name: CleanupCompletedTodoItems
    cron: "0 0 * * 0"                        # weekly on Sunday midnight
    description: "Archive completed todo items older than 90 days"
    dependsOn: [ITodoItemService]
  - name: RetryFailedNotifications
    cron: "*/15 * * * *"                     # every 15 minutes
    description: "Retry notifications that failed to send"
    dependsOn: [INotificationService]
```

### Scheduled Job Generation Rules

- Each job generates:
  - `Handlers/{JobName}Handler.cs` — implements `IScheduledJobHandler`, receives injected services.
  - A `[TickerFunction]` method in the relevant `Jobs/{Feature}Jobs.cs` class that delegates to the handler via `BaseTickerQJob`.
  - Registration in `RegisterSchedulerServices.cs`.
- `cron` uses standard 5-field cron syntax (minute, hour, day-of-month, month, day-of-week).
- `dependsOn` lists service interfaces injected via primary constructor.
- If `schedulerUsePersistence: true`, jobs are tracked in the `[Scheduler]` schema.

## Function App

Use when `includeFunctionApp: true`.

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `functionProfile` | No | `starter` | `starter` (HTTP + Timer baseline) or `full` (HTTP/Timer/Blob/Queue/ServiceBus/EventGrid pattern) |
| `functionTriggers` | No | `[http, timer]` | Trigger set to scaffold first; add others incrementally as dependencies are ready |
| `functionUseMiddleware` | No | `true` | Register global middleware (`GlobalExceptionHandler`, `GlobalLogger`) |
| `functionUseAzureAppConfiguration` | No | `false` | Enable Azure App Configuration integration for Functions host |

### Function Trigger Definitions

Optionally provide detailed trigger definitions to scaffold functions with specific behavior instead of generic placeholders. Each entry generates a `Function{Name}.cs` file.

```yaml
functionDefinitions:
  - name: ProcessAttachmentBlob
    trigger: blob
    blobPath: "attachments/{name}"           # container/path pattern
    description: "Process uploaded attachment and update Attachment entity metadata"
    dependsOn: [IAttachmentService]
  - name: HandleReminderMessage
    trigger: serviceBusQueue
    queueName: "reminder-events"             # Service Bus queue name
    description: "Process reminder trigger from scheduler"
    dependsOn: [IReminderService, INotificationService]
  - name: OnBlobCreatedEvent
    trigger: eventGrid
    eventType: "Microsoft.Storage.BlobCreated"
    description: "React to blob creation events for attachment processing"
    dependsOn: [IAttachmentService]
  - name: DailyCleanup
    trigger: timer
    schedule: "0 0 2 * * *"                  # NCRONTAB 6-field (sec min hr day month dow)
    description: "Clean up expired reminders and archived todo items"
    dependsOn: [ITodoItemService, IReminderService]
  - name: HealthCheck
    trigger: http
    route: "health"
    authLevel: anonymous
    description: "Health check endpoint for monitoring"
```

### Function Definition Generation Rules

- Each definition generates a `Function{Name}.cs` in the Functions project root.
- `trigger` maps to the appropriate Azure Functions binding attribute.
- `dependsOn` lists services injected via primary constructor.
- If `functionDefinitions` is not provided, the AI scaffolds generic placeholder triggers based on `functionTriggers` list.
- For `starter` profile, only `http` and `timer` triggers are scaffolded; other trigger types are deferred to `full` profile.

## Uno Platform UI

When `includeUnoUI` is `true`, these additional inputs configure the cross-platform UI project. The Uno UI authenticates to and consumes the **YARP Gateway** — it never calls the backend API directly.

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `uiThemeColor` | No | `#6750A4` | Material Design primary color (hex) |
| `uiThemeDark` | No | `true` | Include dark mode support |
| `uiLocales` | No | `[en]` | Supported locales for localization |
| `uiPages` | No | *auto from entities* | List of pages to scaffold (e.g., `[Home, ProductList, ProductDetail, Settings]`) |
| `uiAuthProvider` | No | `EntraExternal` | Auth provider used at the UI level (`EntraExternal`, `Custom`, `None`) |
| `uiUseMocks` | No | `true` | Generate mock HTTP handler for offline/dev use |
| `unoProfile` | No | `starter` | Uno UI scaffolding profile: `starter` (core pages + nav + service wiring) or `full` (richer shell, dialogs/flyouts, expanded UI flows) |

### UI Pages

If `uiPages` is not specified, the AI generates pages based on the entities:
- A **Home** page (dashboard/landing)
- A **List** page per top-level entity
- A **Detail** page per top-level entity
- A **Settings** page
- A **Login** / **Registration** page (if auth is enabled)

When `unoProfile: starter`, keep the initial set focused on core list/detail/settings workflows.
When `unoProfile: full`, include richer navigation shell/flyout/dialog routes where appropriate.

```yaml
uiPages:
  - name: Home                        # Landing page
  - name: TodoItemList                 # List page for TodoItem entity
    entity: TodoItem
    type: list
  - name: TodoItemDetail               # Detail page for TodoItem entity
    entity: TodoItem
    type: detail
  - name: Settings                     # Settings page
```

## Notifications

Define notification channels and event-driven notification triggers. This generates provider registrations in `Infrastructure.Notification/` and wires handlers to publish messages.

See [skills/notifications.md](skills/notifications.md) for the full notification architecture.

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `notifications` | No | `false` | Enable the notification subsystem |
| `notificationMode` | No | `integrated` | `integrated` (in-process with API) or `standalone` (separate microservice host) |
| `notificationChannels` | No | `[email]` | Channels to scaffold: `email`, `sms`, `webPush`, `appPush` |

### Notification Triggers

Map domain events to notification deliveries. Each trigger generates wiring in the event's handler to call `INotificationService`.

```yaml
notificationTriggers:
  - event: TodoItemCreated                   # references an event from the events section
    channel: email
    template: NewTodoItemAssigned             # template name (generates a placeholder template)
    description: "Notify assigned team member about new todo item"
  - event: TodoItemCompleted
    channel: [email, appPush]                # multiple channels
    template: TodoItemCompletedNotification
    description: "Notify team that todo item is completed"
  - event: ReminderRescheduled
    channel: [email, appPush]
    template: ReminderRescheduledAlert
    description: "Alert assignee about rescheduled reminder"
```

### Notification Generation Rules

- The notification infrastructure is scaffolded based on `notificationChannels` (provider classes per channel).
- Each `notificationTriggers` entry wires the event handler to call `INotificationService.Send{Channel}Async()`.
- If the referenced `event` has a handler with `dependsOn: [INotificationService]`, the trigger wiring is added to that handler.
- If no matching handler exists, a new handler is generated for the event with the notification logic.
- `template` generates a placeholder notification template constant that can be customized later.

## Seed Data

Define initial reference data that should be seeded during database creation or migration. Generates EF `HasData()` calls in entity configurations or a dedicated `SeedData` startup task.

```yaml
seedData:
  - entity: Category
    rows:
      - { Name: "Work", Description: "Work-related tasks", ColorHex: "#4A90D9", DisplayOrder: 1 }
      - { Name: "Personal", Description: "Personal tasks", ColorHex: "#50C878", DisplayOrder: 2 }
      - { Name: "Urgent", Description: "Urgent and time-sensitive tasks", ColorHex: "#FF5733", DisplayOrder: 3 }
  - entity: Tag
    description: "Global tags for labeling todo items"
    rows:
      - { Name: "bug", Description: "Bug or defect" }
      - { Name: "feature", Description: "New feature request" }
      - { Name: "high-priority", Description: "Requires immediate attention" }
```

### Seed Data Generation Rules

- `Id` is auto-generated (deterministic GUID from entity name + row index) unless explicitly provided.
- If the entity has `isTenantEntity: true`, seed rows must include a `TenantId` or the AI uses a well-known dev tenant ID.
- Seed data is applied via `HasData()` in the entity's EF configuration for small static datasets.
- For larger or environment-specific datasets, the AI generates a `SeedDataStartupTask` in the Bootstrapper that runs on app startup.
- Rows must match the entity's property names and types.

---

## Example Minimal Input

```yaml
ProjectName: TaskFlow
multiTenant: true
includeUnoUI: true
entities:
  - name: TodoItem
    isTenantEntity: true
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
      - { name: Priority, type: int, required: true }
      - { name: Status, type: flags_enum, values: [None, IsStarted, IsCompleted, IsBlocked, IsCancelled] }
    children:
      - { name: Tags, entity: Tag, relationship: many-to-many, joinEntity: TodoItemTag }
    rules:
      - { name: TitleRequired, condition: "!string.IsNullOrWhiteSpace(Title)", errorMessage: "Title is required." }
      - { name: PriorityInRange, condition: "Priority >= 1 && Priority <= 5", errorMessage: "Priority must be between 1 and 5." }
  - name: Category
    isTenantEntity: true
    properties:
      - { name: Name, type: string, maxLength: 100, required: true }
      - { name: Description, type: string, maxLength: 500, required: false }
```

From this input, the AI assistant generates the full vertical slice for each entity using the skill files in order.

## Example Full Input

A richer example demonstrating events, state machines, custom actions, jobs, and notifications:

```yaml
ProjectName: TaskFlow
OrganizationName: Contoso
multiTenant: true
authProvider: EntraID
database: AzureSQL
caching: FusionCache+Redis
useAspire: true
deployTarget: ContainerApps
includeApi: true
includeGateway: true
testingProfile: balanced
includeFunctionApp: true
functionProfile: starter
includeScheduler: true
notifications: true
notificationChannels: [email, appPush]

entities:
  - name: TodoItem
    isTenantEntity: true
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
      - { name: Priority, type: int, required: true }
      - { name: EstimatedHours, type: decimal?, required: false }
      - { name: ActualHours, type: decimal?, required: false }
    children:
      - { name: Comments, entity: Comment, relationship: one-to-many, cascadeDelete: true }
      - { name: Tags, entity: Tag, relationship: many-to-many, joinEntity: TodoItemTag }
      - { name: Reminders, entity: Reminder, relationship: one-to-many, cascadeDelete: true }
    navigation:
      - { name: Category, entity: Category, required: false, deleteRestrict: true }
    rules:
      - { name: TitleRequired, condition: "!string.IsNullOrWhiteSpace(Title)", errorMessage: "Title is required." }
      - { name: PriorityInRange, condition: "Priority >= 1 && Priority <= 5", errorMessage: "Priority must be between 1 and 5." }
    stateMachine:
      field: Status
      initial: None
      states: [None, InProgress, Blocked, Completed, Cancelled]
      transitions:
        - { from: None, to: InProgress, action: Start }
        - { from: InProgress, to: Completed, action: Complete, raisesEvent: TodoItemCompleted }
        - { from: InProgress, to: Blocked, action: Block }
        - { from: Blocked, to: InProgress, action: Unblock }
        - { from: [None, InProgress, Blocked], to: Cancelled, action: Cancel }
    customActions:
      - name: Clone
        returns: TodoItemDto
        description: "Create a copy of this todo item as a new item"
      - name: Reassign
        params:
          - { name: AssignedToId, type: Guid }
        description: "Reassign to a different team member"

  - name: Comment
    isTenantEntity: true
    properties:
      - { name: Text, type: string, maxLength: 1000, required: true }
      - { name: AuthorId, type: string, maxLength: 200, required: true }
      - { name: CreatedAt, type: DateTimeOffset, required: true }

  - name: Category
    isTenantEntity: true
    properties:
      - { name: Name, type: string, maxLength: 100, required: true }
      - { name: Description, type: string, maxLength: 500, required: false }
      - { name: ColorHex, type: string, maxLength: 7, required: false }
      - { name: DisplayOrder, type: int, required: true }

  - name: Tag
    isTenantEntity: false
    properties:
      - { name: Name, type: string, maxLength: 50, required: true }
      - { name: Description, type: string, maxLength: 200, required: false }

  - name: Team
    isTenantEntity: true
    properties:
      - { name: Name, type: string, maxLength: 100, required: true }
      - { name: Description, type: string, maxLength: 500, required: false }
    children:
      - { name: Members, entity: TeamMember, relationship: one-to-many, cascadeDelete: true }

  - name: TeamMember
    isTenantEntity: true
    properties:
      - { name: UserId, type: string, maxLength: 200, required: true }
      - { name: DisplayName, type: string, maxLength: 200, required: true }
      - { name: Role, type: enum, values: [Owner, Admin, Member] }
      - { name: HourlyRate, type: decimal?, required: false }

events:
  - name: TodoItemCreated
    raisedBy: TodoItem
    trigger: afterCreate
    payload:
      - { name: TenantId, type: Guid }
      - { name: Title, type: string }
      - { name: Priority, type: int }
    handlers:
      - { name: RecordTodoItemHistory, description: "Write audit trail", dependsOn: [ITodoItemHistoryRepository] }
  - name: TodoItemCompleted
    raisedBy: TodoItem
    trigger: afterStatusChange
    transitionFrom: InProgress
    transitionTo: Completed
    payload:
      - { name: TenantId, type: Guid }
      - { name: Title, type: string }
      - { name: CompletedBy, type: string }
    handlers:
      - { name: NotifyTeamTodoCompleted, description: "Notify team members", dependsOn: [INotificationService] }

scheduledJobs:
  - name: SyncOverdueTodoItems
    cron: "0 */2 * * *"
    description: "Flag todo items past their due date as overdue"
    dependsOn: [ITodoItemService]
  - name: NightlyTaskReport
    cron: "0 3 * * *"
    description: "Generate daily task summary and email to team admins"
    dependsOn: [ITodoItemService, INotificationService]

messagingProviders: [serviceBus]
messagingChannels:
  - name: TodoItemEvents
    provider: serviceBus
    direction: send
    topicOrQueue: todoitem-events
    messageType: TodoItemEventMessage
    description: "Publish todo item lifecycle events"
  - name: ReminderNotifications
    provider: serviceBus
    direction: receive
    topicOrQueue: reminder-notifications
    subscriptionName: reminder-processor
    messageType: ReminderNotificationMessage
    description: "Consume reminder-triggered notification messages"

notificationTriggers:
  - { event: TodoItemCreated, channel: email, template: NewTodoItemAssigned }
  - { event: TodoItemCompleted, channel: [email, appPush], template: TodoItemCompletedNotification }

seedData:
  - entity: Category
    rows:
      - { Name: "Work", Description: "Work-related tasks", ColorHex: "#4A90D9", DisplayOrder: 1 }
      - { Name: "Personal", Description: "Personal tasks", ColorHex: "#50C878", DisplayOrder: 2 }
      - { Name: "Urgent", Description: "Urgent and time-sensitive tasks", ColorHex: "#FF5733", DisplayOrder: 3 }
  - entity: Tag
    rows:
      - { Name: "bug", Description: "Bug or defect" }
      - { Name: "feature", Description: "New feature request" }
      - { Name: "high-priority", Description: "Requires immediate attention" }
```

From these inputs, the AI scaffolds entities with full CRUD, state machine transitions, domain events with handlers, scheduled background jobs, messaging channels, notification wiring, and seed data — all in one pass.
