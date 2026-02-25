# Resource Implementation Schema (Phase 2 Output)

Maps domain constructs from [domain-definition-schema.md](domain-definition-schema.md) to concrete Aspire/Azure resources, datatypes, and infrastructure.

**Prerequisite:** Complete Phase 1 domain definition first.

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
scaffoldMode: full              # full | lite
testingProfile: balanced        # minimal | balanced | comprehensive
functionProfile: starter        # starter | full
unoProfile: starter             # starter | full
customNugetFeeds: []
```

### Profile Inputs

| Input | Default | Values | Applies when |
|---|---|---|---|
| `scaffoldMode` | `full` | `full`, `lite` | always |
| `testingProfile` | `balanced` | `minimal`, `balanced`, `comprehensive` | always |
| `functionProfile` | `starter` | `starter`, `full` | `includeFunctionApp: true` |
| `unoProfile` | `starter` | `starter`, `full` | `includeUnoUI: true` |

## Entity-to-Store Mapping

Assign each entity a data store and define implementation-level property details.

```yaml
entities:
  - name: TodoItem
    dataStore: sql                          # sql | cosmosdb | table | blob
    partitionKeyProperty: TenantId          # cosmosdb/table only
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
- `notifications` — notification channels/triggers ([skills/notifications.md](skills/notifications.md))
- `scheduledJobs` — background job definitions
- `functionDefinitions` — Azure Function triggers/definitions
- `seedData` — initial data seeding
