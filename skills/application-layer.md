# Application Layer

## Purpose

The application layer owns DTOs, contracts, static mappers, orchestration services, validation helpers, and internal message handlers. Domain invariants stay in domain factories/methods.

## Non-Negotiables

1. Keep contracts separate from implementations.
2. DTOs live in `Application.Models`; services live in `Application.Services`.
3. Mappers are static and provide EF-safe projector expressions.
4. Services enforce validation + tenant boundary checks before writes.
5. Internal event DTOs and handlers stay in contracts/message-handler projects.

Reference implementation: `sample-app/src/Application/`.

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

Entity-specific DTOs and filters go under `Application.Models/{Entity}/`. See [dto-template.md](../templates/dto-template.md) for record patterns.

Shared DTO infrastructure (do not duplicate per entity):

- `EntityBaseDto`
- `ITenantEntityDto`
- `DefaultRequest<T>`
- `DefaultResponse<T>`
- `DefaultSearchFilter`
- `SecurityRoleDto`

---

## Static Mapper Pattern

See [mapper-template.md](../templates/mapper-template.md) for full implementation.

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
3. Enforce tenant boundary on each operation.
4. Use transactional repo for writes, query repo for read/projection.
5. Keep delete idempotent — for **hard delete**, call `repoTrxn.Delete(entity)` before `SaveChangesAsync`. For **soft delete** (main entities), flip flags: `entity.Update(flags: entity.Flags | {Entity}Flags.IsInactive)` then save. Both patterns wrap `SaveChangesAsync` in try/catch returning `Result.Failure(ex.GetBaseException().Message)`.
6. **CreateAsync must apply ALL DTO properties** — `Entity.Create()` only takes factory args. Call `entity.Update(...)` afterward to apply remaining DTO fields (e.g., EstimatedHours, ActualHours). If `Update()` triggers `Valid()`, propagate failures.
7. **SaveChangesAsync overload** — Always use `SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct)`. The parameterless `SaveChangesAsync(ct)` throws `NotImplementedException`.

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

- `StructureValidators`
- `ValidationHelper`
- `ServiceErrorMessages`
- tenant-boundary logging extensions

Example:

```csharp
public static class StructureValidators
{
    internal static Result ValidateCreate<T>(T? dto) where T : class, ITenantEntityDto
    {
        if (dto is null) return Result.Failure("Payload is required.");
        return Require(dto.TenantId != Guid.Empty, "TenantId is required.");
    }
}
```

---

## Service Interface Pattern

See [service-template.md](../templates/service-template.md) for interface and implementation.

---

## Internal Events and Handlers

See [message-handler-template.md](../templates/message-handler-template.md) for full implementation.

> **Note:** `[ScopedMessageHandler]` attribute (from `EF.BackgroundServices.Attributes`) is required on handlers that inject scoped services (repositories, DbContext). Handlers are auto-registered through the internal message bus at startup.

---

## Verification

- [ ] DTO records exist in `Application.Models/{Entity}/`
- [ ] shared DTO infrastructure is centralized and not duplicated
- [ ] static mapper includes `ToDto`, `ToEntity`, and projector expressions
- [ ] projectors are EF-safe expressions
- [ ] service implements `I{Entity}Service` and uses repo split correctly
- [ ] service executes validation + tenant boundary checks on write/read flows
- [ ] event DTOs are in contracts and handlers are in message-handlers project
- [ ] service signatures align with [endpoint-template.md](../templates/endpoint-template.md)