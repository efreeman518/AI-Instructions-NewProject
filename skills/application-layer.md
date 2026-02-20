# Application Layer

## Overview

The application layer contains service implementations, DTOs, static mappers with EF-safe projectors, validation rules, and internal event handling. It bridges the domain model with infrastructure, using contracts (interfaces) defined in a separate project.

## Project Layout

> **Reference implementation:** See `sampleapp/src/Application/` for all four projects demonstrating this layout.

```
Application/
├── Application.Contracts/    # Interfaces + mappers (no implementations)
│   ├── Services/             # IClientService, ITenantBoundaryValidator, etc.
│   ├── Repositories/         # IClientRepositoryTrxn, IClientRepositoryQuery, etc.
│   ├── Mappers/              # Static mapper classes (entity ↔ DTO)
│   ├── Events/               # Internal event/message DTOs
│   └── Constants/            # AppConstants, ErrorConstants
├── Application.Models/       # DTOs, request/response wrappers
├── Application.Services/     # Service implementations
│   └── Rules/                # ValidationHelper, StructureValidators, ServiceErrorMessages
└── Application.MessageHandlers/  # IMessageHandler<T> implementations
```

## DTO Pattern

DTOs are records in `Application.Models`:

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.Models/` — `TodoItem/TodoItemDto.cs`, `Category/CategoryDto.cs`, etc.

```csharp
// Compact pattern — see sampleapp for full implementations
public record {Entity}Dto : EntityBaseDto, ITenantEntityDto
{
    [Required] public Guid TenantId { get; set; }
    [Required] public string Name { get; set; } = null!;
    public List<{Child}Dto> Children { get; set; } = [];
}
```

### Base DTO Types

```csharp
public record EntityBaseDto : IEntityBaseDto { public Guid? Id { get; set; } }
public interface ITenantEntityDto { Guid TenantId { get; set; } }
```

### Request/Response Wrappers

```csharp
public record DefaultRequest<T> { public T Item { get; set; } = default!; }
public record DefaultResponse<T> { public T Item { get; set; } = default!; }
```

### Shared Types (Application.Models)

The following types live in `Application.Models/` (root or `Shared/` subfolder) and are used across all entities. **Do not duplicate them per entity.**

| Type | Location | Purpose |
|------|----------|---------|
| `DefaultRequest<T>` | `Application.Models/DefaultRequest.cs` | Wraps inbound payload for Create/Update |
| `DefaultResponse<T>` | `Application.Models/DefaultResponse.cs` | Wraps outbound payload with optional `TenantInfo` |
| `DefaultSearchFilter` | `Application.Models/DefaultSearchFilter.cs` | Base search filter with paging, sorting, search text |
| `SecurityRoleDto` | `Application.Models/SecurityRoleDto.cs` | Role info for authorization checks |
| `EntityBaseDto` | `Application.Models/Shared/EntityBaseDto.cs` | Base record with nullable `Guid? Id` |
| `ITenantEntityDto` | `Application.Models/Shared/ITenantEntityDto.cs` | Interface requiring `TenantId` property |

**Rules:**
- Entity-specific DTOs go in `Application.Models/{Entity}/` — e.g., `ClientDto`, `ClientSearchFilter`
- Shared types stay in `Application.Models/` root or `Shared/` — never copy them into entity subfolders
- `DefaultSearchFilter` can be extended per entity (e.g., `ClientSearchFilter : DefaultSearchFilter`) for entity-specific filter properties

## Static Mapper Pattern

Mappers are **static classes** in `Application.Contracts/Mappers/`. No AutoMapper — explicit, debuggable, and supports EF-safe projections.

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.Contracts/Mappers/` — `TodoItemMapper.cs` (full CRUD mapper with 3 projectors), `CategoryMapper.cs` (simple mapper with static items projector).

```csharp
// Compact pattern — see sampleapp for full implementations
public static class {Entity}Mapper
{
    public static {Entity}Dto ToDto(this {Entity} entity) => new() { Id = entity.Id, Name = entity.Name };
    public static DomainResult<{Entity}> ToEntity(this {Entity}Dto dto, Guid tenantId)
        => {Entity}.Create(tenantId, dto.Name);

    // EF-Safe Projectors
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> ProjectorSearch = 
        entity => new {Entity}Dto { Id = entity.Id, Name = entity.Name };
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> ProjectorRoot = 
        entity => new {Entity}Dto { Id = entity.Id, Name = entity.Name, Children = entity.Children.Select(c => new {Child}Dto { Id = c.Id }).ToList() };
}
```

### Mapper Rules

1. **ToDto()** — Extension method on entity. Used after loading full entity with includes.
2. **ToEntity()** — Extension method on DTO. Returns `DomainResult<T>` (delegates to domain factory).
3. **Projectors** — `Expression<Func<T, TDto>>` for EF query projection. MUST be EF-safe (no method calls, no `ToString(format)`, no complex ternaries).
4. **Multiple projectors per entity** — `ProjectorSearch` (minimal), `ProjectorRoot` (full), `ProjectorStaticItems` (lookup).
5. **No mapper registration** — Static classes, no DI needed.

## Service Pattern

Services are the main application logic layer:

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.Services/TodoItemService.cs` (full CRUD with tenant boundary, validation, caching) and `sampleapp/src/Application/TaskFlow.Application.Services/CategoryService.cs` (cacheable service with cache-on-write).

```csharp
// Compact pattern — see sampleapp for full implementations
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
        if (entity == null) return Result<DefaultResponse<{Entity}Dto>>.None();
        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(logger, requestContext.TenantId, requestContext.Roles, entity.TenantId, "{Entity}:Get", nameof({Entity}), entity.Id);
        if (boundary.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(boundary.ErrorMessage!);
        return Result<DefaultResponse<{Entity}Dto>>.Success(new() { Item = entity.ToDto() });
    }
    // Create / Update / Delete follow same pattern: Validate → Boundary → Domain → Save
}
```

### Service Rules

1. **Primary constructor DI** — All dependencies via primary constructor
2. **Tenant boundary check** on every operation
3. **Structural validation** before domain operations (Create/Update)
4. **Result pattern** — Return `Result<T>` for operations that can fail, `PagedResponse<T>` for searches
5. **Create flow:** Validate → Boundary check → ToEntity → UpdateFromDto (set children) → Create → SaveChanges
6. **Update flow:** Validate → Load entity → Boundary check → UpdateFromDto → SaveChanges
7. **Delete is idempotent** — Return success if entity not found

## Validation Rules

Centralized structural validators in `Application.Services/Rules/`:

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.Services/Rules/` — `ValidationHelper.cs` (tenant boundary, payload validation), `StructureValidators.cs` (generic Create/Update validation), `ServiceErrorMessages.cs` (parameterized error strings), `TenantBoundaryLoggingExtensions.cs` (source-generated LoggerMessage).

```csharp
// Compact pattern — see sampleapp for full implementation
public static class StructureValidators
{
    internal static Result ValidateCreate<T>(T? dto) where T : class, ITenantEntityDto
    {
        if (dto is null) return Result.Failure("Payload is required.");
        return Require(dto.TenantId != Guid.Empty, "TenantId is required.");
    }
}
```

## Service Interface Pattern

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.Contracts/Services/ITodoItemService.cs`

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

## Internal Events / Message Handlers

For cross-cutting concerns (audit logging, cascading side effects):

> **Reference implementation:** See `sampleapp/src/Application/TaskFlow.Application.MessageHandlers/` for handler implementations and `sampleapp/src/Application/TaskFlow.Application.Contracts/Events/` for event DTOs.

```csharp
// Event DTO (Application.Contracts/Events/)
public record UserCreatedEvent(Guid UserId, Guid TenantId, string Email);

// Handler (Application.MessageHandlers/)
public class UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger) 
    : IMessageHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent message, CancellationToken ct = default) => Task.CompletedTask;
}
```

Handlers are auto-registered via `IInternalMessageBus.AutoRegisterHandlers()` at startup.

---

## Verification

After generating application layer code, confirm:

- [ ] DTOs are records in `Application.Models/{Entity}/` — not in Contracts or Services
- [ ] Shared types (`DefaultRequest`, `DefaultResponse`, `DefaultSearchFilter`, `EntityBaseDto`) are NOT duplicated per entity
- [ ] Mapper is a static class in `Application.Contracts/Mappers/` with `ToDto()`, `ToEntity()`, and projector expressions
- [ ] Projectors use `Expression<Func<T, TDto>>` — no method calls, only property assignments (EF-safe)
- [ ] Service implements `I{Entity}Service` from Contracts using constructor injection
- [ ] Service validates with `StructureValidators` before calling domain methods
- [ ] Service uses `_repoTrxn` for writes and `_repoQuery` for reads (split repository pattern)
- [ ] Event DTOs are records in `Application.Contracts/Events/`
- [ ] Message handlers are in `Application.MessageHandlers/` and implement `IMessageHandler<T>`
- [ ] Cross-references: Mapper projectors match DTO properties, service interface matches [endpoint-template.md](../templates/endpoint-template.md)
