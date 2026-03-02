# Sampleapp Pattern Catalog

Distilled cross-cutting patterns from `sample-app/` that span multiple files/projects. Use this catalog before opening raw reference source.

## How to Use This File

- Load this file first when building a new slice or optional host.
- Pick the matching pattern and then open only the referenced skill/template.
- Avoid duplicating scaffolding logic already defined in templates.

---

## Cross-Cutting Pattern Map

| Pattern | When | Use | Primary References |
|---|---|---|---|
| Self-referencing hierarchy | Parent/child trees (e.g., subtasks) | Align entity nav props + EF config + depth/cycle rule | [templates/entity-template.md](templates/entity-template.md), [templates/ef-configuration-template.md](templates/ef-configuration-template.md), [templates/domain-rules-template.md](templates/domain-rules-template.md) |
| Polymorphic join discriminator | One attachment/comment type used by many aggregates | `{EntityType, EntityId}` + indexed discriminator | [skills/data-access.md](skills/data-access.md), [templates/ef-configuration-template.md](templates/ef-configuration-template.md) |
| Explicit many-to-many join | Join metadata needed (`AppliedAt`, audit, flags) | Explicit join entity + composite key | [skills/data-access.md](skills/data-access.md), [templates/entity-template.md](templates/entity-template.md) |
| Value object / owned type | Value semantics under aggregate root | Record + `Create()` + `OwnsOne` mapping | [templates/domain-rules-template.md](templates/domain-rules-template.md), [templates/ef-configuration-template.md](templates/ef-configuration-template.md) |
| Domain rules/specification | Invariant validation across inputs | `RuleBase<T>` + composed rules + `DomainResult` | [templates/domain-rules-template.md](templates/domain-rules-template.md) |
| Split query/trxn DbContext | Read/write concerns differ | Shared base model + Trxn/Query contexts + query no-tracking | [skills/data-access.md](skills/data-access.md), [templates/repository-template.md](templates/repository-template.md) |
| Multi-tenant query filter | Tenant-safe reads by default | Global filter on `ITenantEntity<Guid>` in DbContext base | [skills/multi-tenant.md](skills/multi-tenant.md), [skills/data-access.md](skills/data-access.md) |
| Internal event pipeline | Domain/service events trigger side effects | Contracts + handler DI + startup auto-registration | [skills/messaging.md](skills/messaging.md), [templates/message-handler-template.md](templates/message-handler-template.md) |
| Gateway token relay | API gateway forwards identity to downstream services | Claims normalization + request transform + service token acquisition | [skills/gateway.md](skills/gateway.md), [skills/identity-management.md](skills/identity-management.md) |
| FusionCache named caches | Per-feature cache policies + Redis backplane | Configure caches from settings loop | [skills/caching.md](skills/caching.md), [skills/configuration.md](skills/configuration.md) |
| TickerQ scheduler host | Cron/time orchestration in separate host | Thin `[TickerFunction]` adapters + handler classes + single replica | [skills/background-services.md](skills/background-services.md), [skills/aspire.md](skills/aspire.md) |
| Channel background queue | In-process async work dispatch | Bounded channel + scoped consumer service | [skills/background-services.md](skills/background-services.md) |
| Mixed-store reconciliation | Feature spans SQL + document/stream/table stores | Authoritative boundary + reconciliation handler/job + replay-safe correction flow | [vertical-slice-checklist.md](vertical-slice-checklist.md), [skills/messaging.md](skills/messaging.md), [skills/background-services.md](skills/background-services.md) |
| Timeline projection | Support/dispute critical workflows | Append-only event log + query read model + timeline endpoint | [skills/messaging.md](skills/messaging.md), [vertical-slice-checklist.md](vertical-slice-checklist.md) |
| Aspire AppHost composition | Multi-host local orchestration | Add resources once, wire references per host, apply `WaitFor` | [skills/aspire.md](skills/aspire.md), [skills/solution-structure.md](skills/solution-structure.md) |
| Docker multi-stage chiseled | Smaller runtime images + cached restore layers | Copy project files first, then publish to chiseled runtime | [skills/cicd.md](skills/cicd.md), [skills/package-dependencies.md](skills/package-dependencies.md) |
| Result-through-layers | All CRUD flows — domain to endpoint | `DomainResult<T>` in entities/services, `Result.Match()` in endpoints; never throw for business errors | [skills/domain-model.md](skills/domain-model.md), [templates/service-template.md](templates/service-template.md), [templates/endpoint-template.md](templates/endpoint-template.md), [skills/api.md](skills/api.md) |
| DefaultExceptionHandler | Unexpected/infrastructure exceptions only | `IExceptionHandler` maps exception types to `ProblemDetails`; last-resort safety net, not a control-flow mechanism | [skills/api.md](skills/api.md) |
| Uno composition root | Client DI/auth/navigation bootstrap | Configure auth + HTTP/Kiota + route maps centrally | [skills/uno-ui.md](skills/uno-ui.md), [templates/mvux-model-template.md](templates/mvux-model-template.md) |

---

## Canonical Composite Snippets

### 1) Self-Referencing Hierarchy

```csharp
public Guid? ParentId { get; private set; }
public TodoItem? Parent { get; private set; }
public ICollection<TodoItem> SubTasks { get; private set; } = [];

builder.HasOne(e => e.Parent)
    .WithMany(e => e.SubTasks)
    .HasForeignKey(e => e.ParentId)
    .OnDelete(DeleteBehavior.Restrict);
```

Pair this with a domain rule to prevent self-parenting/cycles and enforce max depth.

### 2) TickerQ Job Adapter + Handler Split

```csharp
[TickerFunction("ProcessDueReminders", "10 */5 * * * *", TickerTaskPriority.High)]
public async Task ProcessDueRemindersAsync(TickerFunctionContext context, CancellationToken ct)
{
    await ExecuteJobAsync<ProcessDueRemindersHandler>("ProcessDueReminders", context, ct);
}
```

Job method is an adapter only; business logic remains in `IScheduledJobHandler` implementation.

### 3) Gateway Claim Relay

```csharp
context.AddRequestTransform(async transformContext =>
{
    AddOriginalUserClaimsHeader(transformContext);
    var token = await tokenService.GetAccessTokenAsync(clusterId);
    transformContext.ProxyRequest!.Headers.Authorization = new("Bearer", token);
});
```

Always normalize inbound claims before forwarding.

### 4) Result-Through-Layers Error Strategy

Two complementary error paths — never mix them:

```
[Domain]  DomainResult<T>.Success / .Failure   — business validation, rules, state transitions
              ↓
[Service] Result<T>.Success / .Failure / .None  — orchestration, tenant boundary, structure validation
              ↓
[Endpoint] result.Match(ok => ..., errors => ProblemDetails, notFound => NotFound)
              ↓
[DefaultExceptionHandler] IExceptionHandler     — catches ONLY unexpected exceptions (infra, null ref, timeout)
                          maps to ProblemDetails with appropriate HTTP status
```

**Rule:** Use `Result`/`DomainResult` for all expected outcomes. Throw exceptions only for truly unexpected failures. `DefaultExceptionHandler` is a safety net, not a control-flow mechanism.

Reference: `sample-app/src/TaskFlow/TaskFlow.Api/ExceptionHandlers/DefaultExceptionHandler.cs`.

---

## Pattern Selection Rules

1. Prefer template-owned implementation details over catalog duplication.
2. Use this file for orchestration decisions across projects, not per-file boilerplate.
3. If a pattern touches 3+ projects, treat it as a composite and verify all references.
4. When uncertain, default to the simplest pattern that preserves clean architecture boundaries.

---

## Sample App File Index

Quick lookup for exact reference files when scaffolding. All paths relative to `sample-app/src/`.

### Domain Layer
| Artifact | Path |
|---|---|
| Entity (root) | `Domain/Domain.Model/TodoItem.cs` |
| Entity (child) | `Domain/Domain.Model/Comment.cs` |
| Value object | `Domain/Domain.Model/DateRange.cs` |

### Data Access
| Artifact | Path |
|---|---|
| EF config (entity) | `Infrastructure/Infrastructure.Data/Configurations/TodoItemConfiguration.cs` |
| Write repository | `Infrastructure/Infrastructure.Repositories/TodoItemRepositoryTrxn.cs` |
| Read repository | `Infrastructure/Infrastructure.Repositories/TodoItemRepositoryQuery.cs` |
| Trxn DbContext | `Infrastructure/Infrastructure.Data/TaskFlowDbContextTrxn.cs` |
| Query DbContext | `Infrastructure/Infrastructure.Data/TaskFlowDbContextQuery.cs` |
| Updater | `Infrastructure/Infrastructure.Repositories/TodoItemUpdater.cs` |

### Application Layer
| Artifact | Path |
|---|---|
| Service | `Application/Application.Services/TodoItemService.cs` |
| DTO | `Application/Application.Models/TodoItemDto.cs` |
| Search filter | `Application/Application.Models/TodoItemSearchFilter.cs` |
| Mapper | `Application/Application.Mappers/TodoItemMapper.cs` |
| Contracts | `Application/Application.Contracts/` |
| Message handler | `Application/Application.MessageHandlers/TodoItemCreatedEventHandler.cs` |

### API Host
| Artifact | Path |
|---|---|
| Program.cs | `TaskFlow/TaskFlow.Api/Program.cs` |
| Endpoints | `TaskFlow/TaskFlow.Api/Endpoints/TodoItemEndpoints.cs` |
| RegisterApiServices | `TaskFlow/TaskFlow.Api/RegisterApiServices.cs` |
| Bootstrapper | `TaskFlow/TaskFlow.Bootstrapper/RegisterServices.cs` |

### Testing
| Artifact | Path |
|---|---|
| Unit (domain) | `Test/Test.Unit/Domain/TodoItemTests.cs` |
| Unit (mapper) | `Test/Test.Unit/Application/TodoItemMapperTests.cs` |
| Integration | `Test/Test.Integration/EndpointContractTests.cs` |
| Architecture | `Test/Test.Architecture/LayerDependencyTests.cs` |
| Test support | `Test/Test.Support/UnitTestBase.cs`, `InMemoryDbBuilder.cs`, `DbSupport.cs` |
| Endpoint tests | `Test/Test.Endpoints/Endpoints/CategoryEndpointsTests.cs` |
| Custom factory | `Test/Test.Integration/CustomApiFactory.cs` |

### Aspire
| Artifact | Path |
|---|---|
| AppHost | `Aspire/AppHost/AppHost.cs` |
| Service defaults | `Aspire/ServiceDefaults/` |

## Verification Checklist

- [ ] Selected patterns map to enabled workloads only
- [ ] Each chosen pattern has an explicit primary reference loaded
- [ ] No duplicate implementation guidance copied from templates
- [ ] Cross-project wiring (AppHost/Gateway/Scheduler/UI) is internally consistent