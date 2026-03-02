# Resource Implementation Schema (Phase 2 Output)

Maps domain constructs from [domain-specification-schema.md](domain-specification-schema.md) to concrete Aspire/Azure resources, datatypes, and infrastructure.

**Prerequisite:** Complete Phase 1 domain definition first.

## Output Contract

Write Phase 2 output to `.instructions/resource-implementation.yaml` by default. If another path is used, state it explicitly in handoff notes.

## Canonical Defaults (Single Source of Truth)

All defaults used across this instruction set must reference this section. If another file disagrees, this section wins.

```yaml
scaffoldMode: full
testingProfile: balanced
functionProfile: starter      # starter | full
unoProfile: starter           # starter | full
customNugetFeeds: []

includeApi: true
includeGateway: false
includeFunctionApp: false
includeScheduler: false
includeUnoUI: false

includeIaC: true
includeGitHubActions: false
includeAzd: false
```

## Scaffold Configuration

```yaml
scaffoldMode: full              # full | lite | api-only
testingProfile: balanced        # minimal | balanced | comprehensive
functionProfile: starter        # starter | full
unoProfile: starter             # starter | full
customNugetFeeds: []
```

## Policy Inputs (Optional)

```yaml
moneyCalculationPolicy:
  roundingMode: MidpointRounding.ToEven
  currencyScaleMap:
    USD: 2
  operationOrder: [proration, discount, tax]

timeBoundaryPolicy:
  canonicalTimeZone: UTC
  periodBoundaryMode: [start-inclusive, end-exclusive]
  daylightSavingHandling: normalize-to-utc

entitlementPolicy:
  sourcePriority: [Tier, Purchase, Promo]
  conflictResolution: highest-priority-wins
  revokeBehavior: source-scoped-revocation
```

### Profile Inputs

| Input | Default | Values | Applies when |
|---|---|---|---|
| `scaffoldMode` | `full` | `full`, `lite`, `api-only` | always |
| `testingProfile` | `balanced` | `minimal`, `balanced`, `comprehensive` | always |
| `functionProfile` | `starter` | `starter`, `full` | `includeFunctionApp: true` |
| `unoProfile` | `starter` | `starter`, `full` | `includeUnoUI: true` |

## Entity-to-Store Mapping

Assign each entity a data store and define implementation-level property details.

For `ReasonCode`-style fields, prefer enum for stable fixed sets and catalog entities for evolving/localized sets.

```yaml
entities:
  - name: TodoItem
    dataStore: sql                          # sql | cosmosdb | table | blob
    partitionKeyProperty: TenantId          # cosmosdb/table only
    throughputProfile: standard             # optional (e.g., low|standard|high|burst)
    retentionPolicy: keep-forever           # optional (e.g., 30d|180d|archive-after-30d)
    replayWindow: "PT24H"                  # optional for event-driven entities
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
      - { name: DueDate, type: DateTimeOffset?, required: false }
      - { name: Priority, type: enum }
      - { name: Status, type: flags_enum }
      - { name: Amount, type: decimal, precision: 10, scale: 4 }
    children:
      - { name: Tags, entity: Tag, relationship: many-to-many, joinEntity: TodoItemTag }
    navigation:
      - { name: Category, entity: Category, required: false, deleteRestrict: true }
    embedded:                               # cosmosdb only
      - name: Schedule
        properties:
          - { name: StartDate, type: DateTimeOffset? }
```

### Compliance Metadata (Optional)

```yaml
compliance:
  defaultClassification: Internal
  entities:
    - name: PatientProfile
      dataClassification: PHI
      retention: "P7Y"
      encryptionRequired: true
      auditRequired: true
      properties:
        - { name: DateOfBirth, dataClassification: PII }
```

### Data Store Selection

| Signal | `sql` | `cosmosdb` | `table` | `blob` |
|---|---|---|---|---|
| Data shape | fixed relational | document/variable | flat key-value | unstructured file |
| Relationships | joins/FKs | aggregate nesting | none | none |
| Query style | complex filters/joins | partition-aligned reads | partition+row key | get/put/list |
| Transactions | cross-entity ACID | partition-local | single row | object-level |
| Typical use | business source of truth | read models/events | audit/config/counters | attachments/media |

**Quick rules:** Binary content → `blob`. Relational + complex queries → `sql`. Simple key lookups → `table`. Document aggregates → `cosmosdb`. Uncertain → `sql`.

**Common hybrids:** SQL metadata + Blob content. SQL source of truth + Cosmos read model. SQL core + Table audit trail.

### SQL Type Defaults

| Domain kind | SQL type | Notes |
|---|---|---|
| `string` | `nvarchar(N)` | always specify maxLength |
| `text` | `nvarchar(max)` | large text |
| `number` | `int` or `long` | |
| `money` | `decimal(P,S)` | always specify precision+scale |
| `date` | `datetime2` or `DateTimeOffset` | |
| `boolean` | `bit` | |
| `identifier` | `Guid` or `int` | |
| `enum` | `int` | stored as int, C# enum |
| `flags_enum` | `int` | bitwise flags |

## Relationship Configuration

Phase 1 defines the business relationship. Phase 2 adds EF configuration details.

### One-to-many
```csharp
builder.HasMany(e => e.Comments)
    .WithOne()
    .HasForeignKey("TodoItemId")
    .OnDelete(DeleteBehavior.Cascade);
```

### Many-to-many (explicit join)
```csharp
builder.HasKey(e => new { e.TodoItemId, e.TagId });
```

### Self-referencing
```csharp
builder.HasOne(e => e.Parent)
    .WithMany(e => e.Children)
    .HasForeignKey(e => e.ParentId)
    .OnDelete(DeleteBehavior.Restrict);
```

### Polymorphic join
```csharp
builder.Property(e => e.EntityType).HasConversion<string>().HasMaxLength(50).IsRequired();
builder.Property(e => e.EntityId).IsRequired();
builder.HasIndex(e => new { e.EntityType, e.EntityId });
```

## Infrastructure Resources

### Database & Storage

| Input | Default | Values |
|---|---|---|
| `database` | `AzureSQL` | `AzureSQL`, `SQLServer` |
| `caching` | `FusionCache+Redis` | `FusionCache+Redis`, `DistributedMemory`, `None` |
| `includeKeyVault` | `false` | |

### Messaging

```yaml
messagingProviders:
  - { name: ServiceBus, type: AzureServiceBus }
messagingChannels:
  - { name: DomainEvents, provider: ServiceBus, pattern: topic }
messagingSemantics:
  - { channel: DomainEvents, deliveryMode: at-least-once, outboxEnabled: true, idempotencyKey: MessageId, deduplicationWindow: "PT1H" }
```

Options: Azure Service Bus, Event Grid, Event Hubs. See [skills/messaging.md](skills/messaging.md).

### Hosting

| Input | Default | Values |
|---|---|---|
| `deployTarget` | `ContainerApps` | `ContainerApps`, `AppService`, `AKS` |
| `useAspire` | `true` | local orchestration |
| `includeApi` | `true` | |
| `includeGateway` | `false` | |
| `includeFunctionApp` | `false` | |
| `includeScheduler` | `false` | |
| `includeUnoUI` | `false` | |

### UI Hosting (if applicable)

| Platform | Hosting |
|---|---|
| Mobile (iOS/Android) | App store distribution |
| Web (WASM) | Azure Static Web Apps / Container Apps |
| Desktop (Windows) | MSIX / direct distribution |

### Security

| Input | Default |
|---|---|
| `gatewayAuth` | `EntraExternal` |
| `apiAuth` | `EntraID` |
| `tokenRelay` | `true` |
| `tenantIdType` | `Guid` |

### IaC / Pipeline

| Input | Default |
|---|---|
| `includeIaC` | `true` |
| `azureRegion` | `eastus2` |
| `iacEnvironments` | `[dev, staging, prod]` |
| `includeGitHubActions` | `false` |
| `includeAzd` | `false` |
| `usePrivateEndpoints` | `false` |

### AI Services (if applicable)

| Input | Default |
|---|---|
| `includeAiServices` | `false` |
| `aiSearchProvider` | `none` |
| `aiAgentFramework` | `none` |
| `includeMultiAgent` | `false` |

### Testing

| Input | Default |
|---|---|
| `testingProfile` | `balanced` |
| `includeArchitectureTests` | `false` |
| `includeE2ETests` | `false` |
| `includeLoadTests` | `false` |
| `includeBenchmarkTests` | `false` |

### Optional Integrations

- `externalApis` — external API integrations ([skills/external-api.md](skills/external-api.md))
- `includeGrpc` — gRPC services
- `seedData` — initial data seeding

#### Notifications

```yaml
notifications:
  - name: TaskOverdueNotification
    trigger: TodoItemOverdueSuspected          # domain event name
    channel: email                              # email | push | sms | in-app
    template: "task-overdue"                    # template identifier
    recipients: [AssignedMember, TeamLead]
  - name: TaskCompletedNotification
    trigger: TodoItemCompleted
    channel: in-app
    template: "task-completed"
    recipients: [Creator]
```

See [skills/notifications.md](skills/notifications.md).

#### Scheduled Jobs

```yaml
scheduledJobs:
  - name: OverdueTaskCheck
    schedule: "0 */6 * * *"                     # cron expression
    description: "Check for overdue tasks and raise TodoItemOverdueSuspected events"
    targetService: TodoItemService
    method: CheckOverdueItemsAsync
  - name: DailyDigest
    schedule: "0 8 * * 1-5"
    description: "Send daily task summary to team leads"
    targetService: NotificationService
    method: SendDailyDigestAsync
```

#### Function Definitions

```yaml
functionDefinitions:
  - name: ProcessTaskEvent
    trigger: serviceBusTopic                    # serviceBusTopic | httpTrigger | timerTrigger | blobTrigger | queueTrigger
    channel: DomainEvents                       # messaging channel name (if topic/queue trigger)
    subscription: task-events                   # subscription name (if topic trigger)
    description: "Process domain events for task lifecycle changes"
  - name: GenerateReport
    trigger: timerTrigger
    schedule: "0 0 1 * *"
    description: "Generate monthly task completion report"
```

### High-Ingest Operational Controls (Optional)

- `throughputProfile` — expected RU/TU profile and autoscale behavior per entity/channel
- `retentionPolicy` — TTL/archival expectations for time-series or audit data
- `replayWindow` — expected replay/backfill window for event-driven processing

### Ingestion Semantics (Optional)

```yaml
ingestionSemantics:
  eventTimePolicy: event-time
  orderingExpectation: per-partition-ordered
  allowedLateness: "PT10M"
  watermarkStrategy: fixed-lag
  outOfOrderHandling: reconcile-window
```

---

## Phase 2 → 3 Transition Gate

Before moving to Phase 3 (Implementation Plan), verify all of the following:

- [ ] Every Phase 1 entity has a `dataStore` assignment (`sql`, `cosmosdb`, `table`, `blob`)
- [ ] Every `string` property has `maxLength` defined
- [ ] Every `decimal`/`money` property has `precision` and `scale`
- [ ] `scaffoldMode` is set (`full`, `lite`, or `api-only`)
- [ ] At least one host is enabled (`includeApi`, `includeGateway`, etc.)
- [ ] If `many-to-many` relationship exists, `joinEntity` is specified
- [ ] `testingProfile` is set (`minimal`, `balanced`, or `comprehensive`)
- [ ] `customNugetFeeds` is defined (empty array if none)
