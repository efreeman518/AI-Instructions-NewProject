# Sampleapp Pattern Catalog

Distilled cross-cutting patterns from `sampleapp/` that span multiple files/projects. Use this catalog before opening raw reference source.

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
| Aspire AppHost composition | Multi-host local orchestration | Add resources once, wire references per host, apply `WaitFor` | [skills/aspire.md](skills/aspire.md), [skills/solution-structure.md](skills/solution-structure.md) |
| Docker multi-stage chiseled | Smaller runtime images + cached restore layers | Copy project files first, then publish to chiseled runtime | [skills/cicd.md](skills/cicd.md), [skills/package-dependencies.md](skills/package-dependencies.md) |
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

---

## Pattern Selection Rules

1. Prefer template-owned implementation details over catalog duplication.
2. Use this file for orchestration decisions across projects, not per-file boilerplate.
3. If a pattern touches 3+ projects, treat it as a composite and verify all references.
4. When uncertain, default to the simplest pattern that preserves clean architecture boundaries.

## Verification Checklist

- [ ] Selected patterns map to enabled workloads only
- [ ] Each chosen pattern has an explicit primary reference loaded
- [ ] No duplicate implementation guidance copied from templates
- [ ] Cross-project wiring (AppHost/Gateway/Scheduler/UI) is internally consistent