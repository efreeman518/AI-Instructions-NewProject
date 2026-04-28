# TaskFlow Ubiquitous Language Sample

Use this sample only as a reference for the shape and specificity expected from `UBIQUITOUS-LANGUAGE.md`. Do not copy TaskFlow terms into another domain unless the target business uses the same language.

Reference app local path: `C:\Users\EbenFreeman\source\repos\AI-Instructions-ReferenceApp`

## Accepted Terms

| Term | Type | Meaning | Code/Naming Guidance |
|---|---|---|---|
| `TaskItem` | aggregate | Core work item managed by a tenant. | Use `TaskItem`; avoid `Task` because it collides with .NET task type. |
| `Category` | entity | Tenant-scoped hierarchy for grouping task items. | Use `Category`, `CategoryTree`, `ParentCategoryId`. |
| `Tag` | entity | Lightweight tenant-scoped label assignable to many task items. | Use `Tag`; many-to-many bridge is `TaskItemTag`. |
| `Comment` | child entity | Discussion entry owned by a task item. | Use `Comment`; belongs to one `TaskItem`. |
| `ChecklistItem` | child entity | Ordered completion step owned by a task item. | Use `ChecklistItem`; not `Todo`. |
| `Attachment` | entity | File/link metadata owned polymorphically by `TaskItem` or `Comment`. | Use `OwnerType` + `OwnerId`; no parent navigation collection. |
| `TaskItemTag` | join entity | Explicit many-to-many bridge between task item and tag. | Use as a real entity when metadata is needed. |
| `DateRange` | value-object | Start and due date pair for a task item. | EF owned value object on `TaskItem`. |
| `RecurrencePattern` | value-object | Recurrence interval, frequency, and end date. | EF owned value object on `TaskItem`. |

## Rejected Synonyms

| Rejected Term | Use Instead | Reason |
|---|---|---|
| `Task` | `TaskItem` | Avoid `System.Threading.Tasks.Task` collision. |
| `Todo` | `TaskItem` or `ChecklistItem` | Too vague for aggregate vs child step. |
| `Label` | `Tag` | Reference app domain uses tag vocabulary. |

## States And Actions

| Entity | State/Action | Meaning |
|---|---|---|
| `TaskItem` | `Open` | Created and not yet started. |
| `TaskItem` | `InProgress` | Work has started. |
| `TaskItem` | `Blocked` | Work cannot proceed until an obstacle is removed. |
| `TaskItem` | `Completed` | Work finished. |
| `TaskItem` | `Cancelled` | Work intentionally stopped. |
| `TaskItem` | `Start`, `Block`, `Unblock`, `Complete`, `Cancel`, `Reopen` | Allowed lifecycle actions. |

## Events

| Event | Raised By | Meaning |
|---|---|---|
| `TaskItemCreated` | `TaskItem` | A new task item exists. |
| `TaskItemStatusChanged` | `TaskItem` | Task lifecycle state changed. |
| `TaskItemCompleted` | `TaskItem` | Task reached completed state. |
| `TaskItemRescheduled` | `TaskItem` | Task date range changed. |
| `TaskItemOverdueSuspected` | Scheduler | A scheduled check found a likely overdue task. |
| `CommentAdded` | `Comment` | A discussion entry was added to a task. |
| `AttachmentUploaded` | `Attachment` | Attachment metadata now points to uploaded content. |

## Decision Examples

| ID | Decision | Selected Option | Depends On |
|---|---|---|---|
| D-001 | Tenant isolation model | row-level tenant isolation | none |
| D-002 | Task storage | SQL for authoritative entities | D-001 |
| D-003 | Read projection | Cosmos DB `TaskView` projection | D-002 |
| D-004 | Attachment ownership | polymorphic `OwnerType` + `OwnerId`, no FK navigation collections | D-002 |
