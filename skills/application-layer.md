# Application Layer

> **When to read:** Phase 5b, when building application services, DTOs, static mappers, validation helpers, or internal message handlers — anything that orchestrates domain operations behind a `Result<T>` boundary.
> **Skip if:** Phase 5a (domain) or Phase 5c (optional hosts) without service work; pure API endpoint shaping (see `api.md`).

## Worked Example

This is `TaskItemService.CreateAsync` from TaskFlow (`../AI-Instructions-ReferenceApp/src/Application/TaskFlow.Application.Services/TaskItemService.cs`) — multi-tenant variant. It shows the full `Result<DefaultResponse<T>>` flow: validate, enforce tenant boundary, map DTO → entity, persist, log, publish integration event.

```csharp
public async Task<Result<DefaultResponse<TaskItemDto>>> CreateAsync(
    DefaultRequest<TaskItemDto> request, CancellationToken ct = default)
{
    var dto = request.Item;
    dto.TenantId = RequestTenantId ?? Guid.Empty;     // [MULTI-TENANT] stamp from IRequestContext

    var validation = TaskItemStructureValidator.ValidateCreate(dto);
    if (validation.IsFailure)
        return Result<DefaultResponse<TaskItemDto>>.Failure(validation.Errors);

    var boundary = tenantBoundaryValidator.EnsureTenantBoundary(    // [MULTI-TENANT]
        logger, RequestTenantId, RequestRoles, dto.TenantId,
        "TaskItem:Create", nameof(TaskItem));
    if (boundary.IsFailure)
        return Result<DefaultResponse<TaskItemDto>>.Failure(boundary.ErrorMessage!);

    var entityResult = dto.ToEntity(dto.TenantId)
        .Bind(e => repoTrxn.UpdateFromDto(e, dto));   // DomainResult chain
    if (entityResult.IsFailure)
        return Result<DefaultResponse<TaskItemDto>>.Failure(entityResult.ErrorMessage!);

    var entity = entityResult.Value!;
    repoTrxn.Create(ref entity);
    await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

    return Result<DefaultResponse<TaskItemDto>>.Success(BuildResponse(entity.ToDto()));
}
```

Things to notice:
- Service is `internal` — callers go through `ITaskItemService`.
- Primary constructor injection brings in repo split (`repoTrxn`/`repoQuery`), `IRequestContext` (audit + tenant), `ITenantBoundaryValidator`, cache, integration event publisher.
- `BuildResponse` is a private static helper — every Success path uses it. Never inline `new DefaultResponse<T>`.
- DTO → entity mapping uses static mappers (`dto.ToEntity()`, `entity.ToDto()`); no AutoMapper.
- Multi-tenant stamping happens *before* validation. For single-tenant scaffolds, drop the `// [MULTI-TENANT]` lines and `TenantInfo` from `BuildResponse`.

The principles below are commentary on this shape.

## Purpose

The application layer owns DTOs, contracts, static mappers, orchestration services, validation helpers, and internal message handlers. Domain invariants stay in domain factories/methods.

Base types (`IRequestContext`, `Result<T>`, `IStartupTask`): [../support/ef-packages-reference.md](../support/ef-packages-reference.md) — do not regenerate these.

## Non-Negotiables

1. Keep contracts separate from implementations.
2. DTOs live in `Application.Models`; services live in `Application.Services`.
3. Mappers are static and provide EF-safe projector expressions.
4. **[Multi-tenant only]** Services enforce validation + tenant boundary checks before writes. See [multi-tenant.md](multi-tenant.md) for `TenantBoundaryValidator` usage and `EnsureTenantBoundary(...)` patterns.
5. Internal event DTOs and handlers stay in contracts/message-handler projects.
6. **[Multi-tenant only]** Services MUST stamp `dto.TenantId = RequestTenantId ?? Guid.Empty` on the DTO immediately after `var dto = request.Item;` in both `CreateAsync` and `UpdateAsync`. DTOs arrive from the API layer without TenantId. Use `dto.TenantId` (not `RequestTenantId`) in subsequent boundary-validator and `ToEntity()` calls.
7. Use `nameof({Entity})` in service logging and validator calls — never hardcoded entity name strings.
8. Use `ErrorConstants` for shared error keys; use `ServiceErrorMessages` for formatted error messages.
9. Use `[LoggerMessage]` source-generated extensions for high-frequency structured logging in `Rules/`.

> **TaskFlow demonstrates multi-tenant patterns.** When scaffolding a single-tenant application, omit `ITenantBoundaryValidator`, tenant stamping, tenant filter enforcement, and `TenantInfoDto` from `DefaultResponse`. The service template marks these sections with `// [MULTI-TENANT]`.

Reference patterns: [../patterns/expected-output-index.md](../patterns/expected-output-index.md) (Application Layer).

---

## Project Layout

```
Application/
├── Application.Contracts/
│   ├── Services/
│   ├── Repositories/
│   ├── Events/
│   └── Constants/
├── Application.Mappers/         # Separate project — NOT inside Contracts
├── Application.Models/
├── Application.Services/
│   └── Rules/
└── Application.MessageHandlers/
```

---

## DTO Pattern

Entity-specific DTOs and filters go under `Application.Models/{Entity}/`. See [data-mapping-template.md](../templates/data-mapping-template.md) for record patterns.

Shared DTO infrastructure (do not duplicate per entity):

- `EntityBaseDto`
- `ITenantEntityDto` — **[multi-tenant only]**
- `DefaultRequest<T>` — `record`, not `class`
- `DefaultResponse<T>` — `record`, not `class`; includes `TenantInfoDto?` **[multi-tenant only]**
- `TenantInfoDto` — `record` with `Id` (Guid) and `Name` (string) — **[multi-tenant only]**
- `DefaultSearchFilter`
- `SecurityRoleDto`

---

## Static Mapper Pattern

See [data-mapping-template.md](../templates/data-mapping-template.md) for full implementation.

Mapper rules:

1. Keep mappers static (no DI registration).
2. `ToEntity()` delegates construction to domain factories.
3. Projectors must be EF-translatable (no non-translatable method calls).
4. Use multiple projectors when needed (`Basic`, `Search`, `Full`, `StaticItems`).

---

## Service Pattern

See [service-template.md](../templates/service-template.md) for full implementation.

Service rules:

1. Primary-constructor DI only.
2. Validate request structure before domain operations.
3. **[Multi-tenant only]** Enforce tenant boundary on each operation.
4. Use transactional repo for writes, query repo for read/projection.
5. Keep delete idempotent — for **hard delete**, call `repoTrxn.Delete(entity)` before `SaveChangesAsync`. For **soft delete** (main entities), flip flags: `entity.Update(flags: entity.Flags | {Entity}Flags.IsInactive)` then save. Both patterns wrap `SaveChangesAsync` in try/catch returning `Result.Failure(ex.GetBaseException().Message)`.
6. **CreateAsync must apply ALL DTO properties** — `Entity.Create()` only takes factory args. Call `entity.Update(...)` afterward to apply remaining DTO fields (e.g., EstimatedHours, ActualHours). If `Update()` triggers `Valid()`, propagate failures.
7. **SaveChangesAsync overload** — Always use `SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct)`. The parameterless `SaveChangesAsync(ct)` throws `NotImplementedException`.
8. **`BuildResponse` helper** — Each service should have a private static `BuildResponse({Entity}Dto dto)` that returns `new DefaultResponse<{Entity}Dto> { Item = dto }` (add `TenantInfo` when multi-tenant). Centralizes response construction.
9. **`ErrorConstants`** — Use `ErrorConstants.ERROR_ITEM_NOTFOUND` in Update not-found paths (not inline strings).
10. **`nameof({Entity})`** — Use in all boundary-validator calls and error messages.

Flow pattern:

- Create: validate -> boundary -> map/domain create -> persist.
- Update: validate -> load -> boundary -> apply updater -> persist.
- Delete: load (optional) -> boundary -> delete/return success.

## Policy-Driven Orchestration

For policy-sensitive domains, keep logic centralized behind explicit policy services:

- `IMoneyCalculationPolicy` for rounding/currency-scale/proration order
- `ITimeBoundaryPolicy` for period boundaries/timezone handling
- `IEntitlementResolutionPolicy` for tier vs purchase grant precedence
- reason-code resolver (enum or catalog-backed)
- UGC lifecycle policy (moderation/visibility/soft-delete transitions)

Do not scatter these rules across endpoints/handlers.

---

## Validation Helpers

Keep structural validation centralized under `Application.Services/Rules/`:

- `StructureValidators` — generic `ValidateCreate<T>`, `ValidateUpdate<T>`, `ValidateUpdateId<T>` constrained on `ITenantEntityDto` / `IEntityBaseDto`
- Per-entity `{Entity}StructureValidator` — delegates common checks to `StructureValidators`, then adds entity-specific field validation using `DomainConstants`
- `ValidationHelper` — **[multi-tenant only]** static class with `EnsureGlobalAdmin`, `EnsureTenantBoundary`, `PreventTenantChange`
- `ServiceErrorMessages` — static factory methods for formatted error strings (`PayloadRequired`, `ItemNotFound`, `TenantMismatch`, etc.)
- `ErrorConstants` — shared string constants in `Application.Contracts` (`ERROR_ITEM_NOTFOUND`, `ERROR_NAME_EXISTS`, etc.)
- `TenantBoundaryLoggingExtensions` — **[multi-tenant only]** `[LoggerMessage]` source-generated extensions for structured tenant-violation logging
- `TenantRules` — **[multi-tenant only]** simple static rule methods (e.g., `PreventTenantChange`)

Example (generic base):

```csharp
public static class StructureValidators
{
    internal static Result ValidateCreate<T>(T? dto) where T : class, ITenantEntityDto
    {
        if (dto is null) return Result.Failure("Payload is required.");
        return Require(dto.TenantId != Guid.Empty, "TenantId is required.");
    }

    internal static Result ValidateUpdate<T>(T? dto) where T : class, IEntityBaseDto, ITenantEntityDto
    {
        if (dto is null) return Result.Failure("Payload is required.");
        if (dto.Id is null || dto.Id == Guid.Empty) return Result.Failure("Id is required for updates.");
        return Require(dto.TenantId != Guid.Empty, "TenantId is required.");
    }
}
```

Per-entity validators delegate then add field checks — see [structure-validator-template.md](../templates/structure-validator-template.md).

---

## Service Interface Pattern

See [service-template.md](../templates/service-template.md) for interface and implementation.

---

## Internal Events and Handlers

See [message-handler-template.md](../templates/message-handler-template.md) for full implementation.

> **Note:** `[ScopedMessageHandler]` attribute (from `EF.BackgroundServices.Attributes`) is required on handlers that inject scoped services (repositories, DbContext). Handlers are auto-registered through the internal message bus at startup.

### Domain Events vs Integration Events

- **Domain events** are in-process signals within a bounded context (e.g., entity state-change side-effects). Handled synchronously or via `IInternalMessageBus` (in-memory `System.Threading.Channels`).
- **Integration events** cross service boundaries — published to Azure Service Bus topics for async downstream processing by Functions or other consumers. Define integration event DTOs in `Application.Contracts/Events/`, publish via `IIntegrationEventPublisher`.

Integration event publishing is **fire-and-forget after a successful save**. Wrap in try/catch so a messaging failure does not roll back or fail an already-persisted entity:

```csharp
try
{
    await eventPublisher.PublishAsync(
        new {Entity}CreatedEvent(entity.Id, entity.TenantId),
        requestContext.CorrelationId, ct);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Failed to publish {Entity}CreatedEvent for {Id}; entity was saved successfully", entity.Id);
}
```

---

## Verification

- [ ] DTO records exist in `Application.Models/{Entity}/`
- [ ] shared DTO infrastructure is centralized and not duplicated (`EntityBaseDto`, `IEntityBaseDto`, `DefaultSearchFilter`)
- [ ] `DefaultRequest<T>` and `DefaultResponse<T>` are `record` types
- [ ] search filters are `record` types inheriting `DefaultSearchFilter`
- [ ] static mapper includes `ToDto`, `ToEntity`, and projector expressions
- [ ] projectors are EF-safe expressions
- [ ] `StructureValidators.cs` exists with generic `ValidateCreate<T>` / `ValidateUpdate<T>`
- [ ] per-entity validators delegate to `StructureValidators` then add field checks using `DomainConstants`
- [ ] `ErrorConstants` exists in `Application.Contracts` with shared error keys
- [ ] `ServiceErrorMessages` exists in `Application.Services/Rules`
- [ ] service implements `I{Entity}Service` and uses repo split correctly
- [ ] service has `BuildResponse` helper method
- [ ] service uses `nameof({Entity})` in boundary-validator and error messages
- [ ] **[Multi-tenant only]** `ITenantEntityDto` defined, `DefaultResponse` includes `TenantInfoDto?`
- [ ] **[Multi-tenant only]** service stamps `dto.TenantId = RequestTenantId ?? Guid.Empty` before validation in Create/Update
- [ ] **[Multi-tenant only]** service executes tenant boundary checks on write/read flows
- [ ] **[Multi-tenant only]** Search enforces tenant filter for non-admin; logs tenant filter manipulation via `[LoggerMessage]`
- [ ] **[Multi-tenant only]** Update calls `PreventTenantChange` after boundary check
- [ ] event DTOs are in contracts and handlers are in message-handlers project
- [ ] service signatures align with [endpoint-template.md](../templates/endpoint-template.md)

---

**TaskFlow proof (local):** `../AI-Instructions-ReferenceApp/src/Application/TaskFlow.Application.Services/TaskItemService.cs` + `Rules/ServiceErrorMessages.cs`, plus mappers at `../AI-Instructions-ReferenceApp/src/Application/TaskFlow.Application.Mappers/TaskItemMapper.cs`
**TaskFlow proof (remote fallback):** <https://github.com/efreeman518/AI-Instructions-ReferenceApp/blob/main/src/Application/TaskFlow.Application.Services/TaskItemService.cs>
