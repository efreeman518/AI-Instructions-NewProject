# Golden Path Sample

Use this sample as the canonical small scaffold when validating instruction changes. It is intentionally boring: one parent entity, one child entity, SQL, Aspire, API, no optional UI/functions.

The goal is not demo breadth. The goal is repeatable proof that the instructions still produce a buildable, testable, convention-compliant .NET/Aspire/Azure scaffold.

---

## Domain Prompt

```text
Scaffold a WorkBoard app for managing work projects and work items.
Projects have a name, description, status, and due date.
Work items belong to one project and have title, details, priority, status, due date, and completion date.
Users need CRUD and search for both entities.
Use row-level multi-tenancy.
No external APIs, no notifications, no UI, no Functions, no scheduler, no AI services.
```

---

## Expected Phase 1 Output

```yaml
ProjectName: WorkBoard
ProjectDescription: Multi-tenant work tracking API
OrganizationName: Example
multiTenant: true
tenantIsolation: row-level
authProvider: EntraID
authScenario: enterprise
entities:
  - name: Project
    description: Work container owned by one tenant
    isTenantEntity: true
    properties:
      - name: Name
        kind: string
        required: true
      - name: Description
        kind: text
      - name: Status
        kind: enum
        required: true
        values: [Draft, Active, Completed, Archived]
      - name: DueDate
        kind: date
    children:
      - name: WorkItems
        entity: WorkItem
        relationship: one-to-many
        cascadeDelete: false
    rules:
      - name: ProjectNameRequired
        condition: Name is required and max 200 chars
  - name: WorkItem
    description: Trackable item of work under a project
    isTenantEntity: true
    properties:
      - name: ProjectId
        kind: identifier
        required: true
      - name: Title
        kind: string
        required: true
      - name: Details
        kind: text
      - name: Priority
        kind: enum
        required: true
        values: [Low, Normal, High, Critical]
      - name: Status
        kind: enum
        required: true
        values: [New, InProgress, Blocked, Done, Canceled]
      - name: DueDate
        kind: date
      - name: CompletedDate
        kind: date
    navigation:
      - name: Project
        entity: Project
        required: true
    rules:
      - name: CompletedDateRequiredWhenDone
        condition: CompletedDate is required when Status is Done
domainRules:
  - name: WorkItemBelongsToSameTenantAsProject
    appliesTo: [Project, WorkItem]
events: []
workflows: []
```

---

## Expected Phase 2 Output

```yaml
scaffoldMode: full
testingProfile: balanced
includeApi: true
includeGateway: false
includeFunctionApp: false
includeScheduler: false
includeUnoUI: false
includeBlazorUI: false
includeNotifications: false
includeIaC: true
includeGitHubActions: false
includeAzd: false
includeAiServices: false
useAspire: true
database: AzureSQL
caching: FusionCache+Redis
includeKeyVault: false
deployTarget: ContainerApps
tenantIdType: Guid
customNugetFeeds:
  - https://nuget.pkg.github.com/{owner}/index.json
entities:
  - name: Project
    dataStore: sql
    properties:
      - name: Name
        type: string
        maxLength: 200
        required: true
      - name: Description
        type: string
        maxLength: 2000
      - name: Status
        type: ProjectStatus
        required: true
      - name: DueDate
        type: DateTimeOffset?
    children:
      - name: WorkItems
        entity: WorkItem
        relationship: one-to-many
  - name: WorkItem
    dataStore: sql
    properties:
      - name: ProjectId
        type: Guid
        required: true
      - name: Title
        type: string
        maxLength: 200
        required: true
      - name: Details
        type: string
        maxLength: 4000
      - name: Priority
        type: WorkItemPriority
        required: true
      - name: Status
        type: WorkItemStatus
        required: true
      - name: DueDate
        type: DateTimeOffset?
      - name: CompletedDate
        type: DateTimeOffset?
    navigation:
      - name: Project
        entity: Project
        required: true
        deleteRestrict: true
externalDependencyModes:
  sql: emulator
  redis: emulator
  serviceBus: no-op stub
  eventGrid: no-op stub
  keyVault: deployment-only
  blobStorage: no-op stub
  cosmosDb: no-op stub
  aiServices: deployment-only
```

---

## Validation Commands

Run these from the generated app root:

```powershell
python .instructions/scripts/validate-domain-spec.py --file-path domain-specification.yaml
python .instructions/scripts/validate-resource-impl.py --file-path resource-implementation.yaml --domain-spec-path domain-specification.yaml
python .instructions/scripts/validate-ef-packages-feed.py --root . --config-only --require-auth-env
```

After Phase 4:

```powershell
dotnet restore
dotnet build
python .instructions/scripts/validate-ef-packages-feed.py --root .
python .instructions/scripts/validate-scaffold-output.py --root . --phase 4
```

After the final enabled Phase 5 sub-phase:

```powershell
python .instructions/scripts/run-final-scaffold-check.py --root . --require-auth-env
```

---

## Expected Final Shape

- `Project` and `WorkItem` have complete domain models, EF configurations, repositories, DTOs, mappers, services, endpoints, builders, service tests, and endpoint tests.
- EF.Packages types are consumed from NuGet, not reimplemented locally.
- `Directory.Packages.props` owns all versions.
- `nuget.config` maps `EF.*` to the private feed and `dotnet-ef` to `nuget.org`.
- App starts through Aspire and API health returns 200.
- No `NotImplementedException` remains in generated source.
