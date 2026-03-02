# TaskFlow — Resource Implementation

> Reverse-engineered from the sample-app implementation.  
> Schema: [resource-implementation-schema.md](../resource-implementation-schema.md)

## Canonical Defaults

```yaml
scaffoldMode: full
testingProfile: comprehensive
functionProfile: starter
unoProfile: starter
customNugetFeeds: []

includeApi: true
includeGateway: true
includeFunctionApp: true
includeScheduler: true
includeUnoUI: true

includeIaC: false
includeGitHubActions: false
includeAzd: false
```

## Entity-to-Store Mapping

```yaml
entities:
  - name: TodoItem
    dataStore: sql
    properties:
      - { name: Title, type: string, maxLength: 200, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
      - { name: Priority, type: int, required: false, default: 3 }
      - { name: Status, type: flags_enum, default: "None" }
      - { name: EstimatedHours, type: "decimal?", precision: 10, scale: 4 }
      - { name: ActualHours, type: "decimal?", precision: 10, scale: 4 }
      - { name: ParentId, type: "Guid?", description: "Self-referencing FK, max depth 5" }
      - { name: CategoryId, type: "Guid?", description: "FK → Category" }
      - { name: AssignedToId, type: "Guid?", description: "FK → TeamMember" }
      - { name: TeamId, type: "Guid?", description: "FK → Team" }
    embedded:
      - name: Schedule
        description: "DateRange value object mapped to columns on TodoItem table"
        properties:
          - { name: StartDate, type: "DateTimeOffset?" }
          - { name: DueDate, type: "DateTimeOffset?" }
    children:
      - { name: Comments, entity: Comment, relationship: one-to-many, cascadeDelete: true }
      - { name: Reminders, entity: Reminder, relationship: one-to-many, cascadeDelete: true }
      - { name: Attachments, entity: Attachment, relationship: one-to-many, cascadeDelete: true }
      - { name: History, entity: TodoItemHistory, relationship: one-to-many, cascadeDelete: true }
      - { name: Tags, entity: Tag, relationship: many-to-many, joinEntity: TodoItemTag }
      - { name: Children, entity: TodoItem, relationship: self-referencing, onDelete: Restrict }
    navigation:
      - { name: Category, entity: Category, required: false, deleteRestrict: false, onDelete: SetNull }
      - { name: AssignedTo, entity: TeamMember, required: false, onDelete: ClientSetNull }
      - { name: Team, entity: Team, required: false, onDelete: SetNull }
      - { name: Parent, entity: TodoItem, required: false, onDelete: Restrict }
    indexes:
      - { columns: [TenantId] }
      - { columns: [Status] }
      - { columns: [AssignedToId] }
      - { columns: [CategoryId] }

  - name: Team
    dataStore: sql
    properties:
      - { name: Name, type: string, maxLength: 100, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
      - { name: IsActive, type: bool, default: true }
    children:
      - { name: Members, entity: TeamMember, relationship: one-to-many, cascadeDelete: true }
    indexes:
      - { columns: [TenantId, Name], unique: true }

  - name: TeamMember
    dataStore: sql
    properties:
      - { name: TeamId, type: Guid, required: true, description: "FK → Team" }
      - { name: UserId, type: Guid, required: true }
      - { name: DisplayName, type: string, maxLength: 100, required: true }
      - { name: Role, type: enum, default: "Member" }
      - { name: HourlyRate, type: "decimal?", precision: 10, scale: 4 }
      - { name: JoinedAt, type: DateTimeOffset }
    navigation:
      - { name: Team, entity: Team, required: true }
    indexes:
      - { columns: [TeamId, UserId], unique: true }

  - name: Category
    dataStore: sql
    properties:
      - { name: Name, type: string, maxLength: 100, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
      - { name: ColorHex, type: string, maxLength: 7, required: false }
      - { name: DisplayOrder, type: int, default: 0 }
      - { name: IsActive, type: bool, default: true }
    indexes:
      - { columns: [TenantId, Name], unique: true }

  - name: Tag
    dataStore: sql
    isTenantEntity: false
    properties:
      - { name: Name, type: string, maxLength: 50, required: true }
      - { name: Description, type: string, maxLength: 2000, required: false }
    indexes:
      - { columns: [Name], unique: true }

  - name: Comment
    dataStore: sql
    properties:
      - { name: TodoItemId, type: Guid, required: true, description: "FK → TodoItem" }
      - { name: Text, type: string, maxLength: 1000, required: true }
      - { name: AuthorId, type: Guid, required: true }
      - { name: CreatedAt, type: DateTimeOffset }
    indexes:
      - { columns: [TodoItemId] }

  - name: Attachment
    dataStore: sql
    properties:
      - { name: EntityId, type: Guid, required: true, description: "Polymorphic FK" }
      - { name: EntityType, type: enum, required: true, description: "Polymorphic discriminator: TodoItem, Comment" }
      - { name: FileName, type: string, maxLength: 100, required: true }
      - { name: ContentType, type: string, maxLength: 100, required: true }
      - { name: FileSizeBytes, type: long, required: true }
      - { name: BlobUri, type: string, maxLength: 2048, required: true }
      - { name: UploadedAt, type: DateTimeOffset }
      - { name: UploadedBy, type: Guid, required: true }
    indexes:
      - { columns: [EntityType, EntityId] }

  - name: Reminder
    dataStore: sql
    properties:
      - { name: TodoItemId, type: Guid, required: true, description: "FK → TodoItem" }
      - { name: Type, type: enum, required: true }
      - { name: RemindAt, type: "DateTimeOffset?", required: false }
      - { name: CronExpression, type: string, maxLength: 100, required: false }
      - { name: Message, type: string, required: false }
      - { name: IsActive, type: bool, default: true }
      - { name: LastFiredAt, type: "DateTimeOffset?", required: false }
    indexes:
      - { columns: [TodoItemId] }
      - { columns: [RemindAt] }

  - name: TodoItemHistory
    dataStore: sql
    properties:
      - { name: TodoItemId, type: Guid, required: true, description: "FK → TodoItem" }
      - { name: Action, type: string, maxLength: 100, required: true }
      - { name: PreviousStatus, type: "flags_enum?", required: false }
      - { name: NewStatus, type: "flags_enum?", required: false }
      - { name: PreviousAssignedToId, type: "Guid?", required: false }
      - { name: NewAssignedToId, type: "Guid?", required: false }
      - { name: ChangeDescription, type: string, maxLength: 2000, required: false }
      - { name: ChangedBy, type: Guid, required: true }
      - { name: ChangedAt, type: DateTimeOffset, required: true }
    indexes:
      - { columns: [TodoItemId] }
      - { columns: [ChangedAt] }
```

## Infrastructure Resources

### Database & Storage

```yaml
database: AzureSQL
schema: taskflow
cqrs: true
cqrsDetails:
  readWriteContext: TaskFlowDbContextTrxn
  readOnlyContext: TaskFlowDbContextQuery
  readOnlyOptions: [NoTracking, "ApplicationIntent=ReadOnly"]
pooledDbContextFactory: true
azureSqlCompatLevel: 170
sqlServerCompatLevel: 160
retryPolicy:
  maxRetries: 5
  maxDelay: "PT30S"
decimalPrecision: "decimal(10,4)"
dateTimeType: datetime2
concurrency:
  strategy: optimistic
  token: RowVersion
  winner: ClientWins
tenantQueryFilters: true

caching: "FusionCache+Redis"
cacheName: Default
cacheSettings:
  memoryDuration: "PT30M"
  distributedDuration: "PT60M"
  failSafeMaxDuration: "PT120M"
  failSafeThrottleDuration: "PT1M"
  jitterMaxDuration: "PT10S"
  eagerRefreshThreshold: 0.9

includeKeyVault: false
```

### Messaging

```yaml
messagingProviders:
  - { name: InternalMessageBus, type: InProcess, pattern: topic }

messagingSemantics:
  - { channel: InternalEvents, deliveryMode: in-process, processMode: Topic, autoRegister: true }
```

### Hosting

```yaml
deployTarget: ContainerApps
useAspire: true

includeApi: true
includeGateway: true
includeFunctionApp: true
includeScheduler: true
includeUnoUI: true

aspireResources:
  - name: sql
    type: SqlServer
    method: "AddSqlServer(\"sql\", sqlPassword)"
    dataVolume: taskflow-sql-data
    database: TaskFlowDb
  - name: redis
    type: Redis
    method: "AddRedis(\"redis\")"
    dataVolume: taskflow-redis-data

aspireProjects:
  - name: taskflowapi
    project: TaskFlow.Api
    references: [sql, redis]
    ports: [5065, 7065]
  - name: taskflowscheduler
    project: TaskFlow.Scheduler
    references: [sql, redis]
    ports: [5100, 7100]
    replicas: 1
  - name: taskflowgateway
    project: TaskFlow.Gateway
    references: [taskflowapi]
    ports: [5028, 7028]
    description: "YARP reverse proxy"
  - name: functionapp
    project: FunctionApp
    waitFor: [sql]
    description: "Azure Functions isolated worker"
```

### UI Hosting

```yaml
uiPlatform: Uno
uiFramework: MVUX
uiTargets: [Android, iOS, WASM]
uiFeatures:
  - Material
  - Hosting
  - Toolkit
  - Logging
  - MVUX
  - Configuration
  - Http
  - Serialization
  - Localization
  - Navigation
  - Skia
  - ThemeService
  - Authentication
uiHosting:
  gatewayUrl: "https://localhost:7028"
  mockSupport: true
  mockDefine: USE_MOCKS
uiPages:
  - Shell
  - Main
  - Home
  - Login
  - Settings
  - TodoItemList
  - TodoItemDetail
  - TodoItemEdit
  - CategoryList
  - CategoryEdit
  - TagList
  - TagEdit
  - TeamList
  - TeamEdit
```

### Security

```yaml
apiAuth: EntraID
apiAuthConfig: TaskFlowApi_EntraID
gatewayAuth: EntraID
gatewayAuthConfig: TaskFlowGateway_EntraID
externalAuth: EntraExternal
tokenRelay: true
tenantIdType: Guid

authPolicies:
  - { name: GlobalAdmin, role: GlobalAdmin }
  - { name: User, role: User }
fallbackPolicy: RequireAuthenticatedUser

rateLimiting:
  type: FixedWindow
  requestsPerMinute: 100
  partitionBy: IP

correlationTracking:
  header: X-Correlation-ID
  propagate: true
```

### Gateway (YARP)

```yaml
gateway:
  type: YARP
  routes:
    - { match: "api/{**catch-all}", cluster: taskflow-api, destination: "https+http://taskflowapi" }
  features:
    - ExceptionHandler
    - HttpsRedirect
    - CORS
    - Auth
    - ReverseProxy
  healthEndpoints: ["/health", "/alive"]
  cors: configurable
```

### Scheduled Jobs

```yaml
scheduledJobs:
  - name: MaintenanceFunction
    type: AzureFunction
    trigger: timerTrigger
    schedule: "0 0 2 * * *"
    description: "Daily maintenance at 2 AM UTC"
    targetService: MaintenanceService

schedulerFramework: TickerQ
```

### Notifications

```yaml
notifications:
  email:
    provider: MailKit
    protocol: SMTP
    configSection: "NotificationService:EmailSettings"
  sms:
    provider: Twilio
    configSection: "NotificationService:SmsSettings"
```

### External Integrations

```yaml
externalIntegrations:
  - name: EntraExt
    type: MicrosoftGraph
    configSection: EntraExt
    operations:
      - GetUserDisplayNameAsync
      - GetUserEmailAsync
  - name: TimezoneService
    type: NodaTime
    description: "TZDB timezone conversion"
```

### IaC / Pipeline

```yaml
includeIaC: false
includeGitHubActions: false
includeAzd: false
```

### Testing

```yaml
testingProfile: comprehensive
includeArchitectureTests: true
includeE2ETests: true
includeLoadTests: true
includeBenchmarkTests: true

testProjects:
  - { name: Test.Unit, framework: MSTest, libraries: [Moq] }
  - { name: Test.Integration, framework: MSTest, libraries: [Testcontainers.MsSql, Respawn] }
  - { name: Test.Endpoints, framework: MSTest, libraries: [WebApplicationFactory] }
  - { name: Test.Architecture, framework: MSTest, libraries: [NetArchTest] }
  - { name: Test.Benchmarks, framework: BenchmarkDotNet }
  - { name: Test.Load, framework: NBomber }
  - { name: Test.PlaywrightUI, framework: "MSTest + Playwright" }
  - { name: Test.Support, description: "Shared test utilities" }
```

### Application Services

```yaml
services:
  - { name: TodoItemService, interface: ITodoItemService, operations: [Search, CRUD, "State transitions", Assign, AddComment] }
  - { name: CategoryService, interface: ICategoryService, operations: [Search, CRUD] }
  - { name: TagService, interface: ITagService, operations: [Search, CRUD] }
  - { name: TeamService, interface: ITeamService, operations: [Search, CRUD, AddMember, RemoveMember] }
  - { name: MaintenanceService, interface: IMaintenanceService, operations: [PurgeHistory] }

repositories:
  pattern: CQRS
  entities:
    - name: TodoItem
      queryRepo: ITodoItemRepositoryQuery
      trxnRepo: ITodoItemRepositoryTrxn
    - name: Category
      queryRepo: ICategoryRepositoryQuery
      trxnRepo: ICategoryRepositoryTrxn
    - name: Tag
      queryRepo: ITagRepositoryQuery
      trxnRepo: ITagRepositoryTrxn
    - name: Team
      queryRepo: ITeamRepositoryQuery
      trxnRepo: ITeamRepositoryTrxn
    - name: Maintenance
      trxnRepo: IMaintenanceRepository
```

### API Endpoints

```yaml
apiEndpoints:
  basePath: /api
  groups:
    - name: TodoItems
      route: /api/todoitems
      operations:
        - { method: POST, path: /search, description: "Search todo items" }
        - { method: GET, path: "/{id}", description: "Get by ID" }
        - { method: POST, path: /, description: "Create" }
        - { method: PUT, path: /, description: "Update" }
        - { method: DELETE, path: "/{id}", description: "Delete" }
        - { method: POST, path: "/{id}/start", description: "Start" }
        - { method: POST, path: "/{id}/complete", description: "Complete" }
        - { method: POST, path: "/{id}/block", description: "Block" }
        - { method: POST, path: "/{id}/unblock", description: "Unblock" }
        - { method: POST, path: "/{id}/cancel", description: "Cancel" }
        - { method: POST, path: "/{id}/archive", description: "Archive" }
        - { method: POST, path: "/{id}/restore", description: "Restore" }
        - { method: POST, path: "/{id}/reopen", description: "Reopen" }
        - { method: POST, path: "/{id}/assign", description: "Assign member" }
        - { method: POST, path: "/{id}/comments", description: "Add comment" }
    - name: Categories
      route: /api/categories
      operations:
        - { method: POST, path: /search }
        - { method: GET, path: "/{id}" }
        - { method: POST, path: / }
        - { method: PUT, path: / }
        - { method: DELETE, path: "/{id}" }
    - name: Tags
      route: /api/tags
      operations:
        - { method: POST, path: /search }
        - { method: GET, path: "/{id}" }
        - { method: POST, path: / }
        - { method: PUT, path: / }
        - { method: DELETE, path: "/{id}" }
    - name: Teams
      route: /api/teams
      operations:
        - { method: POST, path: /search }
        - { method: GET, path: "/{id}" }
        - { method: POST, path: / }
        - { method: PUT, path: / }
        - { method: DELETE, path: "/{id}" }
        - { method: POST, path: "/{teamId}/members" }
        - { method: DELETE, path: "/{teamId}/members/{memberId}" }
    - name: Maintenance
      route: /api/maintenance
      operations:
        - { method: POST, path: /purge-history }
  healthEndpoints:
    - { path: /health }
    - { path: /alive }
  features:
    - OpenAPI
    - ScalarUI
    - ProblemDetails
    - CorrelationTracking
    - RateLimiting
```

### Bootstrapper Registration Order

```yaml
bootstrapperPipeline:
  - step: RegisterInfrastructureServices
    registers:
      - TimeProvider
      - AzureAppConfiguration
      - InternalMessageBus
      - "FusionCache + Redis"
      - RequestContext
      - AzureClients
      - "Database (CQRS DbContexts + repositories)"
      - "Notifications (email/SMS)"
      - "EntraExt (Graph)"
      - StartupTasks
  - step: RegisterDomainServices
    registers: []
  - step: RegisterApplicationServices
    registers:
      - "MessageHandlers (auto-register)"
      - TodoItemService
      - CategoryService
      - TagService
      - TeamService
      - MaintenanceService
  - step: RegisterBackgroundServices
    registers:
      - ChannelBackgroundTaskQueue

startupTasks:
  - { name: ApplyEFMigrationsStartup, description: "Auto-applies EF migrations in Aspire dev environment" }
  - { name: WarmupDependencies, description: "Warms up both DbContext connections" }

requestContext:
  source: JWT
  claims:
    - { claim: "oid|sub", maps: AuditId }
    - { claim: userTenantId, maps: TenantId }
    - { claim: Role, maps: Roles }
  correlationId:
    header: X-Correlation-ID
    fallback: generated
```
