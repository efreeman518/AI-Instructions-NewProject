# TaskFlow — Domain Specification

> Reverse-engineered from the sample-app implementation.  
> Schema: [domain-specification-schema.md](../ai/domain-specification-schema.md)

## Project Identity

```yaml
ProjectName: "TaskFlow"
ProjectDescription: "A multi-tenant task management platform with team collaboration, scheduling, reminders, and workflow automation."
OrganizationName: "TaskFlow"
```

## Entities

```yaml
entities:
  - name: TodoItem
    description: "A unit of work assigned to a team member with lifecycle tracking, scheduling, and hierarchical nesting."
    isTenantEntity: true
    properties:
      - { name: Title, required: true, description: "Short summary of the task" }
      - { name: Description, required: false, description: "Detailed task description" }
      - { name: Priority, kind: number, required: false, description: "Priority level 1-5 (default 3)" }
      - { name: Status, kind: flags_enum, values: [None, IsStarted, IsCompleted, IsBlocked, IsArchived, IsCancelled] }
      - { name: EstimatedHours, kind: number, required: false, description: "Estimated hours to complete" }
      - { name: ActualHours, kind: number, required: false, description: "Actual hours spent" }
      - { name: Schedule, kind: date, required: false, description: "Date range with StartDate and DueDate (value object)" }
    children:
      - { name: Comments, entity: Comment, relationship: one-to-many, cascadeDelete: true }
      - { name: Reminders, entity: Reminder, relationship: one-to-many, cascadeDelete: true }
      - { name: Attachments, entity: Attachment, relationship: one-to-many, cascadeDelete: true }
      - { name: History, entity: TodoItemHistory, relationship: one-to-many, cascadeDelete: true }
      - { name: Tags, entity: Tag, relationship: many-to-many }
      - { name: Children, entity: TodoItem, relationship: self-referencing }
    navigation:
      - { name: Category, entity: Category, required: false }
      - { name: AssignedTo, entity: TeamMember, required: false }
      - { name: Team, entity: Team, required: false }
      - { name: Parent, entity: TodoItem, required: false }

  - name: Team
    description: "A group of members that collaborate on tasks."
    isTenantEntity: true
    properties:
      - { name: Name, required: true, description: "Team name (unique per tenant)" }
      - { name: Description, required: false }
      - { name: IsActive, kind: boolean, required: false, description: "Whether the team is active (default true)" }
    children:
      - { name: Members, entity: TeamMember, relationship: one-to-many, cascadeDelete: true }

  - name: TeamMember
    description: "A user belonging to a team with a specific role."
    isTenantEntity: true
    properties:
      - { name: UserId, kind: identifier, required: true, description: "External user identity" }
      - { name: DisplayName, required: true, description: "Member display name" }
      - { name: Role, kind: enum, values: [Member, Admin, Owner] }
      - { name: HourlyRate, kind: money, required: false, description: "Member billing rate" }
      - { name: JoinedAt, kind: date, required: true, description: "Date member joined the team" }
    navigation:
      - { name: Team, entity: Team, required: true }

  - name: Category
    description: "A classification label for organizing tasks, unique per tenant."
    isTenantEntity: true
    properties:
      - { name: Name, required: true, description: "Category name (unique per tenant)" }
      - { name: Description, required: false }
      - { name: ColorHex, required: false, description: "Display color in #RRGGBB format" }
      - { name: DisplayOrder, kind: number, required: false, description: "Sort order (default 0)" }
      - { name: IsActive, kind: boolean, required: false, description: "Whether the category is active (default true)" }

  - name: Tag
    description: "A global label that can be applied to multiple tasks (not tenant-scoped)."
    isTenantEntity: false
    properties:
      - { name: Name, required: true, description: "Tag name (globally unique)" }
      - { name: Description, required: false }
    navigation:
      - { name: TodoItems, entity: TodoItem, required: false }

  - name: Comment
    description: "A text comment on a task, recorded with author and timestamp."
    isTenantEntity: true
    properties:
      - { name: Text, required: true, description: "Comment content" }
      - { name: AuthorId, kind: identifier, required: true, description: "User who wrote the comment" }
      - { name: CreatedAt, kind: date, required: true, description: "When the comment was posted" }
    navigation:
      - { name: TodoItem, entity: TodoItem, required: true }

  - name: Attachment
    description: "A file attached to a task or comment via polymorphic association."
    isTenantEntity: true
    properties:
      - { name: EntityId, kind: identifier, required: true, description: "Polymorphic FK to owning entity" }
      - { name: EntityType, kind: enum, values: [TodoItem, Comment], description: "Type of the owning entity" }
      - { name: FileName, required: true, description: "Original file name" }
      - { name: ContentType, required: true, description: "MIME content type" }
      - { name: FileSizeBytes, kind: number, required: true, description: "File size in bytes" }
      - { name: BlobUri, required: true, description: "URI to the blob storage location" }
      - { name: UploadedAt, kind: date, required: true }
      - { name: UploadedBy, kind: identifier, required: true }

  - name: Reminder
    description: "A scheduled reminder associated with a task — one-time or recurring."
    isTenantEntity: true
    properties:
      - { name: Type, kind: enum, values: [OneTime, Recurring], required: true }
      - { name: RemindAt, kind: date, required: false, description: "Required if OneTime" }
      - { name: CronExpression, required: false, description: "Required if Recurring" }
      - { name: Message, required: false, description: "Optional reminder message" }
      - { name: IsActive, kind: boolean, required: false, description: "Whether the reminder is active (default true)" }
      - { name: LastFiredAt, kind: date, required: false }
    navigation:
      - { name: TodoItem, entity: TodoItem, required: true }

  - name: TodoItemHistory
    description: "An audit trail entry recording a change to a task."
    isTenantEntity: true
    properties:
      - { name: Action, required: true, description: "Description of the action (e.g. Created, Updated, AssignedTo)" }
      - { name: PreviousStatus, kind: flags_enum, values: [None, IsStarted, IsCompleted, IsBlocked, IsArchived, IsCancelled], required: false }
      - { name: NewStatus, kind: flags_enum, values: [None, IsStarted, IsCompleted, IsBlocked, IsArchived, IsCancelled], required: false }
      - { name: PreviousAssignedToId, kind: identifier, required: false }
      - { name: NewAssignedToId, kind: identifier, required: false }
      - { name: ChangeDescription, required: false }
      - { name: ChangedBy, kind: identifier, required: true }
      - { name: ChangedAt, kind: date, required: true }
    navigation:
      - { name: TodoItem, entity: TodoItem, required: true }
```

## Business Rules

```yaml
entities:
  - name: TodoItem
    rules:
      - { name: TitleRequired, condition: "Title must not be empty", errorMessage: "Title is required." }
      - { name: TitleMaxLength, condition: "Title must be 200 characters or fewer", errorMessage: "Title exceeds maximum length." }
      - { name: DescriptionMaxLength, condition: "Description must be 2000 characters or fewer", errorMessage: "Description exceeds maximum length." }
      - { name: PriorityRange, condition: "Priority must be between 1 and 5", errorMessage: "Priority must be between 1 and 5." }
      - { name: HierarchyMaxDepth, condition: "Self-referencing hierarchy must not exceed depth 5", errorMessage: "Maximum nesting depth of 5 exceeded." }
      - { name: ScheduleValid, condition: "If Schedule is set, DueDate must be after StartDate", errorMessage: "Due date must be after start date." }

  - name: Team
    rules:
      - { name: NameRequired, condition: "Name must not be empty", errorMessage: "Team name is required." }
      - { name: NameMaxLength, condition: "Name must be 100 characters or fewer", errorMessage: "Team name exceeds maximum length." }
      - { name: NameUnique, condition: "Name must be unique within the tenant", errorMessage: "A team with this name already exists." }

  - name: TeamMember
    rules:
      - { name: UserIdRequired, condition: "UserId must not be empty", errorMessage: "UserId is required." }
      - { name: DisplayNameRequired, condition: "DisplayName must not be empty", errorMessage: "Display name is required." }
      - { name: UniquePerTeam, condition: "A user may only be added to a team once", errorMessage: "User is already a member of this team." }

  - name: Category
    rules:
      - { name: NameRequired, condition: "Name must not be empty", errorMessage: "Category name is required." }
      - { name: NameUnique, condition: "Name must be unique within the tenant", errorMessage: "A category with this name already exists." }
      - { name: ColorHexFormat, condition: "ColorHex must match #RRGGBB format", errorMessage: "Color must be a valid hex color code." }

  - name: Tag
    rules:
      - { name: NameRequired, condition: "Name must not be empty", errorMessage: "Tag name is required." }
      - { name: NameUnique, condition: "Name must be globally unique", errorMessage: "A tag with this name already exists." }

  - name: Comment
    rules:
      - { name: TextRequired, condition: "Text must not be empty", errorMessage: "Comment text is required." }
      - { name: TextMaxLength, condition: "Text must be 1000 characters or fewer", errorMessage: "Comment exceeds maximum length." }
      - { name: AuthorRequired, condition: "AuthorId must not be empty", errorMessage: "Author is required." }

  - name: Attachment
    rules:
      - { name: FileNameRequired, condition: "FileName must not be empty", errorMessage: "File name is required." }
      - { name: ContentTypeRequired, condition: "ContentType must not be empty", errorMessage: "Content type is required." }
      - { name: FileSizePositive, condition: "FileSizeBytes must be greater than zero", errorMessage: "File size must be positive." }
      - { name: BlobUriRequired, condition: "BlobUri must not be empty", errorMessage: "Blob URI is required." }

  - name: Reminder
    rules:
      - { name: OneTimeRequiresRemindAt, condition: "RemindAt is required when Type is OneTime", errorMessage: "Remind-at date is required for one-time reminders." }
      - { name: RecurringRequiresCron, condition: "CronExpression is required when Type is Recurring", errorMessage: "Cron expression is required for recurring reminders." }
```

## State Machines

```yaml
entities:
  - name: TodoItem
    stateMachine:
      field: Status
      kind: flags_enum
      initial: None
      states: [None, IsStarted, IsCompleted, IsBlocked, IsArchived, IsCancelled]
      transitions:
        - { from: None, to: IsStarted, action: Start }
        - { from: IsStarted, to: IsCompleted, action: Complete }
        - { from: [None, IsStarted], to: IsBlocked, action: Block }
        - { from: IsBlocked, to: IsStarted, action: Unblock }
        - { from: [None, IsStarted, IsBlocked], to: IsCancelled, action: Cancel, guard: "Must not be Completed or Archived" }
        - { from: [None, IsCompleted], to: IsArchived, action: Archive }
        - { from: IsArchived, to: None, action: Restore }
        - { from: IsCompleted, to: IsStarted, action: Reopen }
        - { from: IsCancelled, to: None, action: Reopen }
        - { from: [IsStarted, IsBlocked], to: None, action: Reset }
    customActions:
      - { name: Assign, params: [{ name: AssignedToId }] }
      - { name: SetSchedule, params: [{ name: StartDate }, { name: DueDate }] }
      - { name: SetParent, params: [{ name: ParentId }] }
      - { name: AddComment, params: [{ name: Text }, { name: AuthorId }] }
      - { name: AddReminder, params: [{ name: Type }, { name: RemindAt }, { name: CronExpression }, { name: Message }] }
      - { name: RemoveReminder, params: [{ name: ReminderId }] }
      - { name: AddTag, params: [{ name: Tag }] }
      - { name: RemoveTag, params: [{ name: Tag }] }

  - name: Reminder
    customActions:
      - { name: MarkFired, params: [], description: "Deactivates one-time reminders; updates LastFiredAt for recurring" }
```

## Events

```yaml
events:
  - name: TodoItemCreatedEvent
    raisedBy: TodoItem
    trigger: afterCreate
    payload: [TodoItemId, TenantId, Title, AssignedToId, CreatedBy]

  - name: TodoItemUpdatedEvent
    raisedBy: TodoItem
    trigger: afterUpdate
    payload: [TodoItemId, TenantId, Title, PreviousStatus, NewStatus, PreviousAssignedToId, NewAssignedToId, UpdatedBy]

  - name: TodoItemAssignedEvent
    raisedBy: TodoItem
    trigger: afterAction(Assign)
    payload: [TodoItemId, TenantId, Title, PreviousAssignedToId, NewAssignedToId, AssignedBy]
```

### Event Handlers

All event handlers create `TodoItemHistory` audit records:

| Handler | Event | Action Recorded |
|---------|-------|-----------------|
| `TodoItemCreatedEventHandler` | `TodoItemCreatedEvent` | "Created" |
| `TodoItemUpdatedEventHandler` | `TodoItemUpdatedEvent` | "Updated" |
| `TodoItemAssignedEventHandler` | `TodoItemAssignedEvent` | "AssignedTo" |

## Tenancy & Auth Model

```yaml
multiTenant: true
tenantIsolation: "row-level"
globalAdminRole: GlobalAdmin
authProvider: EntraID
authScenario: hybrid

roles:
  - GlobalAdmin
  - TenantAdmin
  - User
  - ReadOnly

externalAuth:
  provider: EntraExternal
  purpose: "Consumer-facing UI authentication (CIAM)"
```

## Domain Constants

```yaml
constants:
  TITLE_MAX_LENGTH: 200
  DESCRIPTION_MAX_LENGTH: 2000
  NAME_MAX_LENGTH: 100
  TAG_NAME_MAX_LENGTH: 50
  COMMENT_MAX_LENGTH: 1000
  URL_MAX_LENGTH: 2048
  COLOR_HEX_LENGTH: 7
  PRIORITY_MIN: 1
  PRIORITY_MAX: 5
  PRIORITY_DEFAULT: 3
  HIERARCHY_MAX_DEPTH: 5
  HISTORY_RETENTION_DAYS: 90
```
