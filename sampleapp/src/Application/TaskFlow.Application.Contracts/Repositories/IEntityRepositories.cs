// Pattern: Simple entity repository interfaces — Category, Tag, Team.
// Category and Team are tenant entities; Tag is a non-tenant shared entity.

using Package.Infrastructure.Domain;
using Domain.Model.Entities;
using Application.Models.Category;
using Application.Models.Tag;
using Application.Models.Team;

namespace Application.Contracts.Repositories;

// ═══════════════════════════════════════════════════════════════
// Category — tenant entity, cacheable
// ═══════════════════════════════════════════════════════════════

public interface ICategoryRepositoryQuery : IBaseRepositoryQuery<Category, CategoryDto>
{
    /// <summary>Pattern: Static-data query — returns all categories for a tenant (cached).</summary>
    Task<IReadOnlyList<CategoryDto>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface ICategoryRepositoryTrxn : IBaseRepositoryTrxn<Category> { }

// ═══════════════════════════════════════════════════════════════
// Tag — non-tenant shared entity (no tenant filter)
// ═══════════════════════════════════════════════════════════════

public interface ITagRepositoryQuery : IBaseRepositoryQuery<Tag, TagDto>
{
    /// <summary>Pattern: Global lookup — returns all tags (no tenant boundary).</summary>
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default);
}

public interface ITagRepositoryTrxn : IBaseRepositoryTrxn<Tag> { }

// ═══════════════════════════════════════════════════════════════
// Team — tenant entity with child members
// ═══════════════════════════════════════════════════════════════

public interface ITeamRepositoryQuery : IBaseRepositoryQuery<Team, TeamDto>
{
    /// <summary>Returns full team with members (ProjectorRoot).</summary>
    Task<TeamDto?> GetWithMembersAsync(Guid teamId, CancellationToken ct = default);
}

public interface ITeamRepositoryTrxn : IBaseRepositoryTrxn<Team> { }
