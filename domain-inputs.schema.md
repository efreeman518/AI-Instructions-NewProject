# Domain Inputs Schema

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
  - name: Product                    # PascalCase entity name
    dataStore: sql                   # sql (default) | cosmosdb | table | blob
    isTenantEntity: true             # implements ITenantEntity<Guid>?
    partitionKeyProperty: TenantId   # cosmosdb/table only — property used as partition key
    properties:                      # scalar properties (Id, RowVersion are automatic)
      - name: Name
        type: string
        maxLength: 100
        required: true
      - name: Sku
        type: string
        maxLength: 50
        required: true
      - name: Price
        type: decimal
        required: true
      - name: Status
        type: flags_enum              # generates a [Flags] enum in Domain.Shared
        values: [None, IsInactive, IsDiscontinued, IsFeatured]
    children:                         # child collections (generates join entities, updaters, etc.)
      - name: Tags
        entity: Tag
        relationship: many-to-many    # many-to-many | one-to-many | polymorphic-join
        joinEntity: EntityTag         # for many-to-many, the join entity name
      - name: Variants
        entity: ProductVariant
        relationship: one-to-many
        cascadeDelete: true
    embedded:                          # cosmosdb only — nested objects stored inside the document
      - name: Address                  # becomes a C# class serialized as nested JSON
        properties:
          - { name: Street, type: string, maxLength: 200 }
          - { name: City, type: string, maxLength: 100 }
          - { name: State, type: string, maxLength: 2 }
          - { name: ZipCode, type: string, maxLength: 10 }
      - name: Preferences              # can be a dictionary / dynamic bag
        type: dictionary               # dictionary | object | list
        keyType: string
        valueType: string
      - name: LineItems                # embedded collection (array of objects in the document)
        type: list
        properties:
          - { name: ProductId, type: Guid, required: true }
          - { name: ProductName, type: string, maxLength: 200 }
          - { name: Quantity, type: int, required: true }
          - { name: UnitPrice, type: decimal, required: true }
    navigation:                       # navigation properties (non-collection)
      - name: Category
        entity: Category
        required: true
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
- Core business entities with referential integrity (Orders → LineItems → Products)
- Data that participates in complex queries, reports, or cross-entity transactions
- Entities with many-to-many or polymorphic relationships
- Anything requiring ACID transactions spanning multiple entity types
- *Examples:* Users, Orders, Products, Invoices, Appointments

**Cosmos DB (`dataStore: cosmosdb`)** — use for:
- Entities with variable/evolving schema (e.g., device telemetry, form submissions with dynamic fields)
- **Documents with deep/nested JSON structure** — embedded objects, arrays of child objects, dictionaries, and mixed-depth hierarchies that would require multiple tables and JOINs in SQL but are naturally represented as a single document
- Read-heavy workloads with known access patterns (always query by tenant + entity id)
- Data that naturally forms self-contained aggregates (an order with embedded line items, an address object, a preferences dictionary — all in one document)
- Global distribution requirements or extreme horizontal scale
- Event sourcing or change-feed-driven architectures
- *Examples:* UserProfiles with nested address + arbitrary preferences, IoT telemetry with variable sensor payloads, product catalog with variant schemas, orders with embedded line items, form submissions with dynamic field structures, configuration documents with nested sections
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
| **Structured + Attachments** | Entity metadata, FKs | — | — | File content | Order with uploaded PO document |
| **Relational + Audit Trail** | Core entity CRUD | — | Append-only change log | — | Patient record with immutable audit history |
| **Relational + Read Cache** | Source of truth | Denormalized read model (change feed from SQL via events) | — | — | Product catalog: SQL for writes, Cosmos for fast reads |
| **Relational + Telemetry** | Device/sensor registration | Time-series telemetry data | — | Raw data exports | IoT platform |
| **Search + Binary** | Searchable metadata | — | — | Raw documents | Document management system |

> For hybrid patterns, model the primary entity with `dataStore: sql` (or cosmosdb) and reference the secondary store through a service-layer abstraction. For example, an `Order` entity (`dataStore: sql`) with file attachments uses `IBlobRepository` in the `OrderService` — the blob interaction is in the service, not on the entity itself.

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
```

## Relationship Types

| Type | Description | Example |
|------|-------------|---------|
| `one-to-many` | Parent owns collection of children | Product → Variants |
| `many-to-many` | Via explicit join entity (no EF implicit) | Product → EntityTag → Tag |
| `polymorphic-join` | Join entity with EntityId + EntityType for shared tables | Person → EntityPhone → Phone |
| `self-referencing` | Entity relates to itself via join entity | QGroup → QGroupRelationship → QGroup |

### Relationship Modeling Guide

When defining relationships in domain inputs, include enough detail for the AI to generate correct EF configurations. The sections below show the EF configuration pattern for each relationship type, based on real examples from the sample solution.

#### One-to-Many (with back-navigation)

Parent owns a collection. Child has a FK back to parent. Use `cascadeDelete: true` for owned children.

```yaml
children:
  - name: ScheduledCallClients
    entity: ScheduledCallClient
    relationship: one-to-many
    cascadeDelete: true
```

**EF Configuration:**
```csharp
// In ClientConfiguration : EntityBaseConfiguration<Client>
builder.HasMany(e => e.ScheduledCallClients)
    .WithOne()
    .HasForeignKey("ClientId")
    .OnDelete(DeleteBehavior.Cascade);
```

#### One-to-Many (reference, no back-navigation)

Entity references a lookup/type entity. No back-navigation collection on the referenced entity. Use `deleteRestrict: true` to prevent orphans.

```yaml
navigation:
  - name: ClientType
    entity: ClientType
    required: true
    deleteRestrict: true
```

**EF Configuration:**
```csharp
// In ClientConfiguration : EntityBaseConfiguration<Client>
builder.HasOne(e => e.ClientType)
    .WithMany()
    .OnDelete(DeleteBehavior.Restrict);
```

#### Many-to-Many (explicit join entity)

Always use an explicit join entity — never EF implicit many-to-many. The join entity can carry extra payload (e.g., `Context`, `Sequence`).

```yaml
children:
  - name: QGroups
    entity: QGroup
    relationship: many-to-many
    joinEntity: ClientTypeQGroup
    joinProperties:
      - { name: Context, type: string, maxLength: 50 }
      - { name: Sequence, type: int }
```

**EF Configuration:**
```csharp
// In ClientTypeQGroupConfiguration : EntityBaseConfiguration<ClientTypeQGroup>
builder.HasOne<ClientType>()
    .WithMany()
    .HasForeignKey(e => e.ClientTypeId);

builder.HasOne<QGroup>()
    .WithMany()
    .HasForeignKey(e => e.QGroupId);

// Composite clustered index for fast lookups
builder.HasIndex(e => new { e.ClientTypeId, e.QGroupId, e.Context, e.Sequence })
    .IsClustered();
```

#### Self-Referencing (via join entity)

Entity relates to itself through a join entity with two FKs pointing to the same table. Both sides use `Restrict` delete to prevent cascading cycles.

```yaml
children:
  - name: ChildRelationships
    entity: QGroupRelationship
    relationship: self-referencing
    selfReferenceKeys: [MainQGroupId, ChildQGroupId]
```

**EF Configuration:**
```csharp
// In QGroupRelationshipConfiguration : EntityBaseConfiguration<QGroupRelationship>
builder.HasOne<QGroup>()
    .WithMany()
    .HasForeignKey(e => e.MainQGroupId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne<QGroup>()
    .WithMany()
    .HasForeignKey(e => e.ChildQGroupId)
    .OnDelete(DeleteBehavior.Restrict);

// Prevent duplicate relationships
builder.HasIndex(e => new { e.MainQGroupId, e.ChildQGroupId })
    .IsUnique();
```

#### Polymorphic Join (EntityType + EntityId discriminator)

A shared table (e.g., `EntityTag`, `EntityEmail`) stores data for multiple entity types, discriminated by an `EntityType` string column and `EntityId` GUID. Use a check constraint to enforce valid entity types.

```yaml
children:
  - name: Tags
    entity: Tag
    relationship: polymorphic-join
    joinEntity: EntityTag
    polymorphicEntityTypes: [Client, Person, Facility]
```

**EF Configuration:**
```csharp
// In EntityTagConfiguration : EntityBaseConfiguration<EntityTag>
builder.Property(e => e.EntityType)
    .HasMaxLength(50)
    .IsRequired();

builder.Property(e => e.EntityId)
    .IsRequired();

builder.HasIndex(e => new { e.EntityType, e.EntityId });

// Enforce valid discriminator values
builder.HasCheckConstraint(
    "CK_EntityTag_EntityType",
    "[EntityType] IN ('Client', 'Person', 'Facility')");
```

> **Note:** Polymorphic joins do NOT use EF navigation properties to the parent entity. The service layer resolves the parent type at runtime based on `EntityType`.

## Domain Events

Define internal events that flow through `IInternalMessageBus`. Each event generates an event DTO in `Application.Contracts/Events/` and a handler in `Application.MessageHandlers/`. Handlers are auto-registered by `IInternalMessageBus.AutoRegisterHandlers()` in Bootstrapper.

```yaml
events:
  - name: UserCreated                       # PascalCase event name → generates UserCreated.cs + UserCreatedHandler.cs
    raisedBy: User                           # entity or service that publishes the event
    trigger: afterCreate                     # afterCreate | afterUpdate | afterDelete | afterStatusChange | custom
    payload:                                 # event-specific properties (Id is automatic)
      - { name: TenantId, type: Guid? }
      - { name: Username, type: string }
      - { name: Email, type: string }
      - { name: FirstName, type: string }
      - { name: LastName, type: string }
    handlers:                                # one handler class per entry
      - name: SendWelcomeEmail
        description: "Send onboarding email to new user"
        dependsOn: [INotificationService]    # services injected into handler
      - name: AuditUserCreation
        description: "Write audit log entry"

  - name: OrderApproved
    raisedBy: Order
    trigger: afterStatusChange               # triggered by state machine transition
    transitionFrom: Submitted
    transitionTo: Approved
    payload:
      - { name: TenantId, type: Guid }
      - { name: OrderNumber, type: string }
      - { name: ApprovedBy, type: string }
    handlers:
      - name: NotifyOrderApproved
        description: "Email customer that order is approved"
        dependsOn: [INotificationService]

  - name: RescheduleCallRequest
    raisedBy: ScheduledCallService           # raised from a custom action, not entity lifecycle
    trigger: custom
    payload:
      - { name: ScheduledCallClientId, type: Guid }
      - { name: RescheduledDateTime, type: DateTime }
    handlers:
      - name: ProcessReschedule
        description: "Invoke rescheduling logic"
        dependsOn: [IScheduledCallService]
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
  - name: Product
    rules:
      - name: PriceMustBePositive
        condition: "Price > 0"
        errorMessage: "Product price must be greater than zero."
      - name: SkuFormat
        condition: "Sku matches ^[A-Z]{2,4}-\\d{4,8}$"
        errorMessage: "SKU must follow the format XX-0000 (2-4 uppercase letters, dash, 4-8 digits)."
      - name: NameRequired
        condition: "!string.IsNullOrWhiteSpace(Name)"
        errorMessage: "Product name is required."
```

### Standalone (cross-entity or complex)

```yaml
domainRules:
  - name: TenantQuotaNotExceeded
    appliesTo: [Product, Warehouse]          # multiple entities share this rule
    description: "Tenant cannot exceed maximum entity count per plan"
    dependsOn: [ITenantService]              # requires injected service context
    errorMessage: "Tenant has reached the maximum allowed {Entity} count."
  - name: CannotDeleteActiveParent
    appliesTo: [Category]
    condition: "entity.Children.Count == 0"
    errorMessage: "Cannot delete a category that still has child products."
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
  - name: Order
    isTenantEntity: true
    properties:
      - { name: OrderNumber, type: string, maxLength: 50, required: true }
      - { name: Total, type: decimal, required: true }
    stateMachine:
      field: Status                           # property that holds the state (generated as enum)
      initial: Draft                          # default state on Create()
      states: [Draft, Submitted, Approved, Shipped, Delivered, Cancelled]
      transitions:
        - from: Draft
          to: Submitted
          action: Submit                      # generates SubmitAsync() on service + POST endpoint
        - from: Submitted
          to: Approved
          action: Approve
          raisesEvent: OrderApproved          # references event defined in events section
          guardRule: ApproverRequired         # optional — must pass this domain rule first
        - from: Submitted
          to: Cancelled
          action: Cancel
        - from: Approved
          to: Shipped
          action: Ship
          raisesEvent: OrderShipped
        - from: Shipped
          to: Delivered
          action: Deliver
        - from: [Draft, Submitted]            # multiple source states
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
  - name: ScheduledCall
    customActions:
      - name: Reschedule
        params:
          - { name: NewDateTime, type: DateTime }
        raisesEvent: RescheduleCallRequest
        description: "Move the scheduled call to a new date/time"
      - name: AssignAgent
        params:
          - { name: AgentId, type: Guid }
          - { name: Notes, type: string? }
        description: "Assign a call agent to this scheduled call"
      - name: Clone
        returns: ScheduledCallDto              # non-void return
        description: "Create a copy of this scheduled call with a new ID"
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

Entity state machines handle **single-entity lifecycle** well (e.g., `Order: Draft → Submitted → Approved → Shipped`). However, real business workflows often span **multiple entities, external service calls, decision branches, retries, compensation, async waits, and human intervention** — e.g., "validate inventory across Products, process Payment, create Shipment, handle failures with SupportTicket escalation." This level of orchestration is inherently custom and should not be captured declaratively.

### How to describe workflows to the AI

Provide an optional `workflows` section as **descriptive hints** for the AI. These are not code-generating — they give the AI enough context to scaffold an orchestrator service class with the right dependencies, method stubs, and TODO placeholders. The developer fills in the actual business logic.

```yaml
workflows:
  - name: OrderFulfillment
    description: |
      1. Order submitted → validate inventory across Product entities
      2. If inventory insufficient → create BackorderRequest, notify purchasing, wait
      3. If inventory OK → process Payment via external gateway
      4. If payment fails → retry 3x, then escalate to SupportTicket
      5. If payment succeeds → approve Order, create Shipment, deduct inventory
      6. Shipment delivered → complete Order, notify customer
    involvedEntities: [Order, Product, Payment, Shipment, BackorderRequest, SupportTicket]
    pattern: orchestrator                    # orchestrator | choreography
    compensationRequired: true               # indicates rollback/compensation steps exist
    notes: "Payment gateway is external — needs idempotency keys and retry policy"
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
  - name: OrderEvents
    provider: serviceBus               # serviceBus | eventGrid | eventHub
    direction: send                     # send | receive | both
    topicOrQueue: order-events          # queue/topic name (Service Bus) or topic name (Event Grid/Hub)
    subscriptionName: api-handler       # Service Bus subscription (receive/both only)
    messageType: OrderEventMessage      # DTO type published/consumed
    description: "Publish order lifecycle events to downstream services"
  - name: PaymentConfirmations
    provider: serviceBus
    direction: receive
    topicOrQueue: payment-confirmations
    subscriptionName: order-processor
    messageType: PaymentConfirmedMessage
    description: "Consume payment confirmations from billing service"
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
  - Concrete implementation in `Infrastructure.Repositories/` inheriting from the appropriate `Package.Infrastructure.Messaging` base class
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
  - name: SyncStaleOrders
    cron: "0 */2 * * *"                     # every 2 hours
    description: "Re-process orders stuck in Pending for >24h"
    dependsOn: [IOrderService]               # services injected into handler
  - name: NightlyReportGeneration
    cron: "0 3 * * *"                        # daily at 3 AM
    description: "Generate daily summary report and email to admins"
    dependsOn: [IReportService, INotificationService]
  - name: CleanupExpiredSessions
    cron: "0 0 * * 0"                        # weekly on Sunday midnight
    description: "Remove expired user sessions older than 30 days"
    dependsOn: [ISessionRepository]
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
  - name: ProcessOrderBlob
    trigger: blob
    blobPath: "orders/{name}"                # container/path pattern
    description: "Parse uploaded order CSV and create Order entities"
    dependsOn: [IOrderService]
  - name: HandlePaymentMessage
    trigger: serviceBusQueue
    queueName: "payment-events"              # Service Bus queue name
    description: "Process payment confirmation from external system"
    dependsOn: [IPaymentService, IOrderService]
  - name: OnBlobCreatedEvent
    trigger: eventGrid
    eventType: "Microsoft.Storage.BlobCreated"
    description: "React to blob creation events for document processing"
    dependsOn: [IDocumentService]
  - name: DailyCleanup
    trigger: timer
    schedule: "0 0 2 * * *"                  # NCRONTAB 6-field (sec min hr day month dow)
    description: "Clean up temporary files and expired tokens"
    dependsOn: [ICleanupService]
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
  - name: ProductList                  # List page for Product entity
    entity: Product
    type: list
  - name: ProductDetail                # Detail page for Product entity
    entity: Product
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
  - event: UserCreated                       # references an event from the events section
    channel: email
    template: WelcomeEmail                   # template name (generates a placeholder template)
    description: "Send welcome email on user registration"
  - event: OrderShipped
    channel: [email, sms]                    # multiple channels
    template: ShipmentNotification
    description: "Notify customer that order has shipped"
  - event: PaymentFailed
    channel: [email, appPush]
    template: PaymentFailedAlert
    description: "Alert customer and ops about failed payment"
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
  - entity: ClientType
    rows:
      - { Name: "Enterprise", Description: "Enterprise-level client" }
      - { Name: "SMB", Description: "Small/medium business" }
      - { Name: "Individual", Description: "Individual consumer" }
  - entity: OrderStatus
    description: "Lookup table for order status display names"
    rows:
      - { Name: "Draft", DisplayName: "Draft", SortOrder: 1 }
      - { Name: "Submitted", DisplayName: "Pending Review", SortOrder: 2 }
      - { Name: "Approved", DisplayName: "Approved", SortOrder: 3 }
  - entity: AppSetting
    rows:
      - { Key: "MaxUploadSizeMB", Value: "25", Description: "Maximum file upload size" }
      - { Key: "SessionTimeoutMinutes", Value: "30", Description: "User session timeout" }
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
ProjectName: Inventory
multiTenant: true
includeUnoUI: true
entities:
  - name: Product
    isTenantEntity: true
    properties:
      - { name: Name, type: string, maxLength: 100, required: true }
      - { name: Sku, type: string, maxLength: 50, required: true }
      - { name: Price, type: decimal, required: true }
      - { name: Status, type: flags_enum, values: [None, IsInactive, IsDiscontinued] }
    children:
      - { name: Categories, entity: Category, relationship: many-to-many, joinEntity: ProductCategory }
    rules:
      - { name: PriceMustBePositive, condition: "Price > 0", errorMessage: "Price must be greater than zero." }
      - { name: SkuRequired, condition: "!string.IsNullOrWhiteSpace(Sku)", errorMessage: "SKU is required." }
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
ProjectName: OrderManagement
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
notificationChannels: [email, sms]

entities:
  - name: Order
    isTenantEntity: true
    properties:
      - { name: OrderNumber, type: string, maxLength: 50, required: true }
      - { name: Total, type: decimal, required: true }
      - { name: Notes, type: string, maxLength: 1000, required: false }
    children:
      - { name: LineItems, entity: OrderLineItem, relationship: one-to-many, cascadeDelete: true }
    navigation:
      - { name: Customer, entity: Customer, required: true, deleteRestrict: true }
    rules:
      - { name: TotalMustBePositive, condition: "Total > 0", errorMessage: "Order total must be positive." }
      - { name: MustHaveLineItems, condition: "LineItems.Count > 0", errorMessage: "Order must have at least one line item." }
    stateMachine:
      field: Status
      initial: Draft
      states: [Draft, Submitted, Approved, Shipped, Delivered, Cancelled]
      transitions:
        - { from: Draft, to: Submitted, action: Submit }
        - { from: Submitted, to: Approved, action: Approve, raisesEvent: OrderApproved }
        - { from: Submitted, to: Cancelled, action: Cancel }
        - { from: Approved, to: Shipped, action: Ship, raisesEvent: OrderShipped }
        - { from: Shipped, to: Delivered, action: Deliver }
    customActions:
      - name: Clone
        returns: OrderDto
        description: "Create a copy of this order as a new Draft"

  - name: OrderLineItem
    isTenantEntity: true
    properties:
      - { name: ProductName, type: string, maxLength: 200, required: true }
      - { name: Quantity, type: int, required: true }
      - { name: UnitPrice, type: decimal, required: true }

  - name: Customer
    isTenantEntity: true
    properties:
      - { name: Name, type: string, maxLength: 200, required: true }
      - { name: Email, type: string, maxLength: 254, required: true }
      - { name: Phone, type: string, maxLength: 20, required: false }

events:
  - name: OrderApproved
    raisedBy: Order
    trigger: afterStatusChange
    transitionFrom: Submitted
    transitionTo: Approved
    payload:
      - { name: TenantId, type: Guid }
      - { name: OrderNumber, type: string }
      - { name: ApprovedBy, type: string }
    handlers:
      - { name: NotifyCustomerOrderApproved, description: "Email customer", dependsOn: [INotificationService] }
  - name: OrderShipped
    raisedBy: Order
    trigger: afterStatusChange
    transitionFrom: Approved
    transitionTo: Shipped
    payload:
      - { name: TenantId, type: Guid }
      - { name: OrderNumber, type: string }
      - { name: TrackingNumber, type: string? }
    handlers:
      - { name: NotifyCustomerOrderShipped, description: "Email + SMS customer", dependsOn: [INotificationService] }

scheduledJobs:
  - name: SyncStaleOrders
    cron: "0 */2 * * *"
    description: "Re-process orders stuck in Submitted for >24h"
    dependsOn: [IOrderService]
  - name: NightlyOrderReport
    cron: "0 3 * * *"
    description: "Generate daily order summary and email to admins"
    dependsOn: [IOrderService, INotificationService]

messagingProviders: [serviceBus]
messagingChannels:
  - name: OrderEvents
    provider: serviceBus
    direction: send
    topicOrQueue: order-events
    messageType: OrderEventMessage
    description: "Publish order lifecycle events"
  - name: PaymentConfirmations
    provider: serviceBus
    direction: receive
    topicOrQueue: payment-confirmations
    subscriptionName: order-processor
    messageType: PaymentConfirmedMessage
    description: "Consume payment confirmations from billing service"

externalApis:
  - name: Stripe
    baseUrl: "https://api.stripe.com"
    authScheme: apiKey
    description: "Payment processing"
    operations:
      - { name: CreateCharge, method: POST, path: "/v1/charges", requestType: CreateChargeRequest, responseType: ChargeResponse }
      - { name: GetCharge, method: GET, path: "/v1/charges/{chargeId}", responseType: ChargeResponse }

notificationTriggers:
  - { event: OrderApproved, channel: email, template: OrderApprovedEmail }
  - { event: OrderShipped, channel: [email, sms], template: OrderShippedNotification }

seedData:
  - entity: Customer
    rows:
      - { Name: "Contoso Ltd", Email: "orders@contoso.com", Phone: "555-0100" }
      - { Name: "Fabrikam Inc", Email: "purchasing@fabrikam.com", Phone: "555-0200" }
```

From these inputs, the AI scaffolds entities with full CRUD, state machine transitions, domain events with handlers, scheduled background jobs, messaging channels, external API integrations, notification wiring, and seed data — all in one pass.
