// Pattern: Non-tenant (global) entity service — Tag.
// No tenant boundary checks since tags are shared across all tenants.

using Microsoft.Extensions.Logging;
using EF.Common;
using EF.Domain;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.Tag;

namespace Application.Services;

internal class TagService(
    ILogger<TagService> logger,
    ITagRepositoryQuery repoQuery,
    ITagUpdater updater) : ITagService
{
    public async Task<Result<PagedResponse<TagDto>>> SearchAsync(
        TagSearchFilter filter, CancellationToken ct = default)
    {
        // Pattern: No tenant filter for global entities.
        var result = await repoQuery.QueryPageProjectionAsync(filter, ct);
        return Result<PagedResponse<TagDto>>.Success(result);
    }

    public async Task<Result<TagDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repoQuery.QueryByIdProjectionAsync(id, ct);
        return dto is null ? Result<TagDto>.NotFound() : Result<TagDto>.Success(dto);
    }

    public async Task<Result<TagDto>> CreateAsync(TagDto dto, CancellationToken ct = default)
        => await updater.CreateAsync(dto, ct);

    public async Task<Result<TagDto>> UpdateAsync(TagDto dto, CancellationToken ct = default)
        => await updater.UpdateAsync(dto, ct);

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => await updater.DeleteAsync(id, ct);

    public async Task<Result<IReadOnlyList<TagDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var tags = await repoQuery.GetAllAsync(ct);
        return Result<IReadOnlyList<TagDto>>.Success(tags);
    }
}
