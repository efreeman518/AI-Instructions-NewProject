# CQRS Handler Template

Use when `.scaffold/resource-implementation.yaml` sets `applicationStyle: cqrs` or `switch`.

```csharp
public sealed record Create{Entity}Command(DefaultRequest<{Entity}Dto> Request)
    : ICommand<Result<DefaultResponse<{Entity}Dto>>>;

internal sealed class Create{Entity}Handler(
    ILogger<Create{Entity}Handler> logger,
    IRequestContext<string, Guid?> requestContext,
    I{Entity}RepositoryTrxn repoTrxn,
    ITenantBoundaryValidator tenantBoundaryValidator)
    : IRequestHandler<Create{Entity}Command, Result<DefaultResponse<{Entity}Dto>>>
{
    public async Task<Result<DefaultResponse<{Entity}Dto>>> HandleAsync(
        Create{Entity}Command command,
        CancellationToken ct = default)
    {
        var dto = command.Request.Item;
        dto.TenantId = requestContext.TenantId ?? Guid.Empty;

        var validation = {Entity}StructureValidator.ValidateCreate(dto);
        if (validation.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(validation.Errors);

        var boundary = tenantBoundaryValidator.EnsureTenantBoundary(
            logger, requestContext.TenantId, requestContext.Roles, dto.TenantId,
            "{Entity}:Create", nameof({Entity}));
        if (boundary.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(boundary.ErrorMessage!);

        var entityResult = dto.ToEntity(dto.TenantId);
        if (entityResult.IsFailure) return Result<DefaultResponse<{Entity}Dto>>.Failure(entityResult.ErrorMessage!);

        var entity = entityResult.Value!;
        repoTrxn.Create(ref entity);
        await repoTrxn.SaveChangesAsync(OptimisticConcurrencyWinner.ClientWins, ct);

        return Result<DefaultResponse<{Entity}Dto>>.Success(new DefaultResponse<{Entity}Dto>
        {
            Item = entity.ToDto()
        });
    }
}
```

Rules:

- Avoid central request dispatchers, request buses, and generic `Send()` entrypoints.
- Reason: keep route -> request -> handler flow explicit and registered once.
- One command/query maps to one handler registration.
- Handler injects only repositories and collaborators it uses.
- Reuse existing repository contracts; do not create CQRS-specific repositories unless the domain needs a genuinely different abstraction.
