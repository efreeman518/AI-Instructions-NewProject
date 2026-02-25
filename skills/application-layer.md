# Application Layer

## Purpose

The application layer owns DTOs, contracts, static mappers, orchestration services, validation helpers, and internal message handlers. Domain invariants stay in domain factories/methods.

## Non-Negotiables

1. Keep contracts separate from implementations.
2. DTOs live in `Application.Models`; services live in `Application.Services`.
3. Mappers are static and provide EF-safe projector expressions.
4. Services enforce validation + tenant boundary checks before writes.
5. Internal event DTOs and handlers stay in contracts/message-handler projects.

Reference implementation: `sampleapp/src/Application/`.

---

## Project Layout

```
Application/
├── Application.Contracts/
│   ├── Services/
│   ├── Repositories/
│   ├── Mappers/
│   ├── Events/
│   └── Constants/
├── Application.Models/
├── Application.Services/
│   └── Rules/
└── Application.MessageHandlers/
```

---

## DTO Pattern

```csharp
public record {Entity}Dto : EntityBaseDto, ITenantEntityDto
{
    [Required] public Guid TenantId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public List<{Child}Dto> Children { get; set; } = [];
}
```

Shared DTO infrastructure (do not duplicate per entity):

- `EntityBaseDto`
- `ITenantEntityDto`
- `DefaultRequest<T>`
- `DefaultResponse<T>`
- `DefaultSearchFilter`
- `SecurityRoleDto`

Entity-specific DTOs and filters go under `Application.Models/{Entity}/`.

---

## Static Mapper Pattern

```csharp
public static class {Entity}Mapper
{
    public static {Entity}Dto ToDto(this {Entity} entity) => new() { Id = entity.Id, Name = entity.Name };

    public static DomainResult<{Entity}> ToEntity(this {Entity}Dto dto, Guid tenantId)
        => {Entity}.Create(tenantId, dto.Name);

    public static readonly Expression<Func<{Entity}, {Entity}Dto>> ProjectorSearch =
        entity => new {Entity}Dto { Id = entity.Id, Name = entity.Name };
}
```

Mapper rules:

1. Keep mappers static (no DI registration).
2. `ToEntity()` delegates construction to domain factories.
3. Projectors must be EF-translatable (no non-translatable method calls).
4. Use multiple projectors when needed (`Search`, `Root`, `StaticItems`).

---

## Service Pattern

```csharp
public class {Entity}Service(
    ILogger<{Entity}Service> logger,
    IRequestContext<string, Guid?> requestContext,
    I{Entity}RepositoryTrxn repoTrxn,
    I{Entity}RepositoryQuery repoQuery,
    ITenantBoundaryValidator tenantBoundaryValidator) : I{Entity}Service
{
    public async Task<Result<DefaultResponse<{Entity}Dto>>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repoTrxn.Get{Entity}Async(id, includeChildren: true, ct);
        if (entity is null) return Result<DefaultResponse<{Entity}Dto>>.None();

        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
            logger, requestContext.TenantId, requestContext.Roles, entity.TenantId,
            "{Entity}:Get", nameof({Entity}), entity.Id);

        if (boundary.IsFailure)
            return Result<DefaultResponse<{Entity}Dto>>.Failure(boundary.ErrorMessage!);

        return Result<DefaultResponse<{Entity}Dto>>.Success(new() { Item = entity.ToDto() });
    }
}
```

Service rules:

1. Primary-constructor DI only.
2. Validate request structure before domain operations.
3. Enforce tenant boundary on each operation.
4. Use transactional repo for writes, query repo for read/projection.
5. Keep delete idempotent.

Flow pattern:

- Create: validate -> boundary -> map/domain create -> persist.
- Update: validate -> load -> boundary -> apply updater -> persist.
- Delete: load (optional) -> boundary -> delete/return success.

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

```csharp
public interface I{Entity}Service
{
    Task<PagedResponse<{Entity}Dto>> SearchAsync(SearchRequest<{Entity}SearchFilter> request, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> CreateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default);
    Task<Result<DefaultResponse<{Entity}Dto>>> UpdateAsync(DefaultRequest<{Entity}Dto> request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

---

## Internal Events and Handlers

```csharp
public record UserCreatedEvent(Guid UserId, Guid TenantId, string Email);

public class UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger)
    : IMessageHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent message, CancellationToken ct = default) => Task.CompletedTask;
}
```

Handlers are auto-registered through the internal message bus at startup.

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