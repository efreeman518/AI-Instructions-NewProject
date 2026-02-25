# Domain Inputs Schema

Single input contract for scaffolding. Complete domain discovery first (see [SKILL.md](SKILL.md)), then submit a compact YAML payload.

## Required First

- `ProjectName`
- `customNugetFeeds` (use `[]` if no private feeds)

## Core Identity

| Input | Default | Notes |
|---|---|---|
| `ProjectDescription` | — | README summary |
| `OrganizationName` | — | optional namespace prefix |
| `scaffoldMode` | `full` | `full` or `lite` |

---

## Entity Contract

Canonical shape:

```yaml
entities:
  - name: TodoItem
    dataStore: sql                  # sql | cosmosdb | table | blob
    isTenantEntity: true
    partitionKeyProperty: TenantId  # cosmosdb/table only
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
      - { name: Status, type: flags_enum, values: [None, IsStarted, IsCompleted] }
    children:
      - { name: Tags, entity: Tag, relationship: many-to-many, joinEntity: TodoItemTag }
    navigation:
      - { name: Category, entity: Category, required: false, deleteRestrict: true }
    embedded:                        # cosmosdb only
      - name: Schedule
        properties:
          - { name: StartDate, type: DateTimeOffset? }
```

Supported relationships:

- `one-to-many`
- `many-to-many`
- `polymorphic-join`
- `self-referencing`

Detailed modeling: [domain-design-guide.md](domain-design-guide.md).

---

## Events, Rules, and Workflow Primitives

### Events

```yaml
events:
  - name: TodoItemCreated
    raisedBy: TodoItem
    trigger: afterCreate            # afterCreate | afterUpdate | afterDelete | afterStatusChange | custom
    payload:
      - { name: TenantId, type: Guid? }
```

Rules:

- event DTOs implement `IMessage`
- handlers implement `IMessageHandler<T>`

### Rules

```yaml
entities:
  - name: TodoItem
    rules:
      - { name: TitleRequired, condition: "!string.IsNullOrWhiteSpace(Title)", errorMessage: "Title is required." }

domainRules:
  - { name: TenantQuotaNotExceeded, appliesTo: [TodoItem], dependsOn: [ITenantService], errorMessage: "Tenant quota exceeded." }
```

### State machine + custom actions

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
      - { name: Reschedule, params: [{ name: NewRemindAt, type: DateTimeOffset }] }
```

---

## Security and Tenant Inputs

| Input | Default | Values |
|---|---|---|
| `multiTenant` | `true` | tenant isolation |
| `tenantIdType` | `Guid` | tenant key type |
| `globalAdminRole` | `GlobalAdmin` | bypass role |
| `authProvider` | `EntraID` | `EntraID`, `EntraExternal`, `None` |
| `gatewayAuth` | `EntraExternal` | user auth at gateway |
| `apiAuth` | `EntraID` | API auth |
| `tokenRelay` | `true` | relay user context through gateway |

---

## Infra, Delivery, and Host Flags

### Infrastructure

| Input | Default | Values |
|---|---|---|
| `database` | `AzureSQL` | `AzureSQL`, `SQLServer` |
| `caching` | `FusionCache+Redis` | `FusionCache+Redis`, `DistributedMemory`, `None` |
| `useAspire` | `true` | orchestration enabled |
| `deployTarget` | `ContainerApps` | `ContainerApps`, `AppService`, `AKS` |
| `includeKeyVault` | `false` | key vault scaffolding |
| `includeGrpc` | `false` | gRPC |
| `externalApis` | `[]` | external integrations |

### IaC / pipeline

| Input | Default |
|---|---|
| `includeIaC` | `true` |
| `azureRegion` | `eastus2` |
| `iacEnvironments` | `[dev, staging, prod]` |
| `includeGitHubActions` | `false` |
| `includeAzd` | `false` |
| `usePrivateEndpoints` | `false` |

### Host inclusion

| Input | Default |
|---|---|
| `includeApi` | `true` |
| `includeGateway` | `true` |
| `includeFunctionApp` | `false` |
| `includeScheduler` | `false` |
| `includeBlazorUI` | `false` |
| `includeUnoUI` | `false` |

---

## AI and Testing Inputs

### AI

| Input | Default | Values |
|---|---|---|
| `includeAiServices` | `false` | enable AI infrastructure |
| `aiSearchProvider` | `none` | `none`, `azureSql`, `cosmosdb`, `azureAiSearch` |
| `aiAgentFramework` | `none` | `none`, `agentFramework`, `microsoftFoundry` |
| `includeMultiAgent` | `false` | requires agent framework |

### Testing

| Input | Default | Values |
|---|---|---|
| `testingProfile` | `balanced` (`full`) / `minimal` (`lite`) | `minimal`, `balanced`, `comprehensive` |
| `includeArchitectureTests` | `false` | architecture tests |
| `includeE2ETests` | `false` | Playwright |
| `includeLoadTests` | `false` | NBomber |
| `includeBenchmarkTests` | `false` | BenchmarkDotNet |

Templates:

- [templates/test-template-unit.md](templates/test-template-unit.md)
- [templates/test-template-integration.md](templates/test-template-integration.md)
- [templates/test-template-e2e.md](templates/test-template-e2e.md)
- [templates/test-template-quality.md](templates/test-template-quality.md)

---

## Integration Blocks (Optional)

Use these sections only when the feature is enabled:

- `externalApis` (pattern: [skills/external-api.md](skills/external-api.md))
- `messagingProviders` / `messagingChannels` (pattern: [skills/messaging.md](skills/messaging.md))
- scheduler keys (`schedulerEngine`, `scheduledJobs`, etc.)
- function keys (`functionProfile`, `functionTriggers`, `functionDefinitions`)
- UI keys (`unoProfile`, `uiPages`, theme/localization)
- notification keys (`notifications`, `notificationChannels`, `notificationTriggers`; pattern: [skills/notifications.md](skills/notifications.md))
- `seedData`

---

## Minimal Example

```yaml
ProjectName: TaskFlow
scaffoldMode: full
multiTenant: true
includeApi: true
includeGateway: true
entities:
  - name: TodoItem
    dataStore: sql
    isTenantEntity: true
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
customNugetFeeds: []
```