// ═══════════════════════════════════════════════════════════════
// Pattern: Service unit tests — CategoryService (cache-on-write for static data).
// Demonstrates: IFusionCacheProvider mock for cache-on-write verification,
// CategoryDeletionRule cross-entity check via ITodoItemRepositoryQuery mock,
// IRequestContext mocking for tenant boundary enforcement.
//
// Service constructor:
//   CategoryService(ILogger, IRequestContext<string,Guid?>, ICategoryRepositoryQuery,
//                   ICategoryUpdater, ITodoItemRepositoryQuery, IFusionCacheProvider)
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.Category;
using Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Package.Infrastructure.Common;
using Test.Support;
using ZiggyCreatures.Caching.Fusion;

namespace Test.Unit.Services;

[TestClass]
public class CategoryServiceTests : UnitTestBase
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: Mock setup — one mock per dependency.
    // ═══════════════════════════════════════════════════════════════

    private readonly Mock<ICategoryRepositoryQuery> _repoQueryMock;
    private readonly Mock<ICategoryUpdater> _updaterMock;
    private readonly Mock<ITodoItemRepositoryQuery> _todoItemRepoMock;
    private readonly Mock<IFusionCacheProvider> _fusionCacheProviderMock;
    private readonly Mock<IFusionCache> _fusionCacheMock;
    private readonly Mock<IRequestContext<string, Guid?>> _requestContextMock;

    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public CategoryServiceTests() : base()
    {
        _repoQueryMock = _mockFactory.Create<ICategoryRepositoryQuery>();
        _updaterMock = _mockFactory.Create<ICategoryUpdater>();
        _todoItemRepoMock = _mockFactory.Create<ITodoItemRepositoryQuery>();
        _fusionCacheProviderMock = _mockFactory.Create<IFusionCacheProvider>();
        _fusionCacheMock = _mockFactory.Create<IFusionCache>();
        _requestContextMock = _mockFactory.Create<IRequestContext<string, Guid?>>();

        // Pattern: Wire FusionCacheProvider → returns a mock IFusionCache for StaticData cache.
        _fusionCacheProviderMock.Setup(p => p.GetCache(It.IsAny<string>()))
            .Returns(_fusionCacheMock.Object);

        // Pattern: Default request context — tenant-scoped user.
        _requestContextMock.Setup(r => r.TenantId).Returns(TestTenantId);
        _requestContextMock.Setup(r => r.UserId).Returns("test-user-001");
        _requestContextMock.Setup(r => r.Roles).Returns(new List<string> { "User" });
    }

    /// <summary>Helper — creates a CategoryService with current mock instances.</summary>
    private CategoryService CreateService() => new(
        new NullLogger<CategoryService>(),
        _requestContextMock.Object,
        _repoQueryMock.Object,
        _updaterMock.Object,
        _todoItemRepoMock.Object,
        _fusionCacheProviderMock.Object);

    // ═══════════════════════════════════════════════════════════════
    // Create — Cache-on-write verification
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateAsync_ValidDto_InvalidatesTenantCache()
    {
        // Arrange
        var dto = new CategoryDto
        {
            TenantId = TestTenantId,
            Name = "Work",
            DisplayOrder = 1,
            IsActive = true
        };

        var createdDto = new CategoryDto
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = "Work",
            DisplayOrder = 1,
            IsActive = true
        };

        _updaterMock.Setup(u => u.CreateAsync(It.IsAny<CategoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CategoryDto>.Success(createdDto));

        var svc = CreateService();

        // Act
        var result = await svc.CreateAsync(dto);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Work", result.Value!.Name);

        // Pattern: Verify cache-on-write — cache is invalidated after create.
        _fusionCacheMock.Verify(c => c.RemoveAsync(
            $"Categories:{TestTenantId}",
            It.IsAny<FusionCacheEntryOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateAsync_NonAdmin_ForcesTenantFromContext()
    {
        // Arrange — DTO has no TenantId, non-admin caller.
        var dto = new CategoryDto { Name = "Personal", DisplayOrder = 2 };
        var createdDto = new CategoryDto { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Personal" };

        _updaterMock.Setup(u => u.CreateAsync(It.IsAny<CategoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CategoryDto>.Success(createdDto));

        var svc = CreateService();

        // Act
        var result = await svc.CreateAsync(dto);

        // Assert — Pattern: Non-admin forces TenantId from caller context.
        Assert.IsTrue(result.IsSuccess);
        _updaterMock.Verify(u => u.CreateAsync(
            It.Is<CategoryDto>(d => d.TenantId == TestTenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════
    // Update — Cache invalidation after mutation
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdateAsync_Success_InvalidatesTenantCache()
    {
        // Arrange
        var dto = new CategoryDto
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = "Updated Work",
            DisplayOrder = 1
        };

        _updaterMock.Setup(u => u.UpdateAsync(It.IsAny<CategoryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CategoryDto>.Success(dto));

        var svc = CreateService();

        // Act
        var result = await svc.UpdateAsync(dto);

        // Assert
        Assert.IsTrue(result.IsSuccess);

        // Pattern: Cache-on-write — cache invalidated after successful update.
        _fusionCacheMock.Verify(c => c.RemoveAsync(
            $"Categories:{TestTenantId}",
            It.IsAny<FusionCacheEntryOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════
    // Delete — CategoryDeletionRule cross-entity check
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_NoActiveItems_CallsUpdaterAndInvalidatesCache()
    {
        // Arrange — no active todo items in this category.
        var categoryId = Guid.NewGuid();
        var existing = new CategoryDto { Id = categoryId, TenantId = TestTenantId, Name = "Old Category" };

        _todoItemRepoMock.Setup(r => r.HasActiveItemsInCategoryAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _updaterMock.Setup(u => u.DeleteAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var svc = CreateService();

        // Act
        var result = await svc.DeleteAsync(categoryId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        _updaterMock.Verify(u => u.DeleteAsync(categoryId, It.IsAny<CancellationToken>()), Times.Once);

        // Pattern: Cache invalidated after successful delete.
        _fusionCacheMock.Verify(c => c.RemoveAsync(
            $"Categories:{TestTenantId}",
            It.IsAny<FusionCacheEntryOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_HasActiveItems_ReturnsFailure()
    {
        // Arrange — category has active (non-archived, non-cancelled) todo items.
        var categoryId = Guid.NewGuid();

        _todoItemRepoMock.Setup(r => r.HasActiveItemsInCategoryAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = CreateService();

        // Act
        var result = await svc.DeleteAsync(categoryId);

        // Assert — Pattern: CategoryDeletionRule blocks deletion when active items exist.
        Assert.IsTrue(result.IsFailure);

        // Pattern: Updater should NOT be called when rule fails.
        _updaterMock.Verify(u => u.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_NotFound_ReturnsSuccessIdempotent()
    {
        // Arrange — category doesn't exist (deletion rule passes, lookup returns null).
        var categoryId = Guid.NewGuid();

        _todoItemRepoMock.Setup(r => r.HasActiveItemsInCategoryAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategoryDto?)null);

        var svc = CreateService();

        // Act
        var result = await svc.DeleteAsync(categoryId);

        // Assert — Pattern: Idempotent delete — not found = success.
        Assert.IsTrue(result.IsSuccess);
        _updaterMock.Verify(u => u.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════
    // GetById — Tenant boundary
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetByIdAsync_ExistingItem_ReturnsDto()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var dto = new CategoryDto { Id = categoryId, TenantId = TestTenantId, Name = "Work" };

        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var svc = CreateService();

        // Act
        var result = await svc.GetByIdAsync(categoryId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Work", result.Value!.Name);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetByIdAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategoryDto?)null);

        var svc = CreateService();

        // Act
        var result = await svc.GetByIdAsync(Guid.NewGuid());

        // Assert — Pattern: NotFound result for missing entity.
        Assert.IsTrue(result.IsFailure || result.Value is null);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetByIdAsync_CrossTenant_ReturnsForbidden()
    {
        // Arrange — category belongs to a different tenant.
        var categoryId = Guid.NewGuid();
        var dto = new CategoryDto { Id = categoryId, TenantId = Guid.NewGuid(), Name = "Other Tenant" };

        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var svc = CreateService();

        // Act
        var result = await svc.GetByIdAsync(categoryId);

        // Assert — Pattern: Tenant boundary violation → Forbidden.
        Assert.IsTrue(result.IsFailure);
    }

    // ═══════════════════════════════════════════════════════════════
    // Search — Tenant filter enforcement
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_NonAdmin_ForcesTenantFilter()
    {
        // Arrange
        var filter = new CategorySearchFilter { TenantId = null };
        var response = new PagedResponse<CategoryDto> { Data = [], TotalCount = 0 };

        _repoQueryMock.Setup(r => r.QueryPageProjectionAsync(It.IsAny<CategorySearchFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var svc = CreateService();

        // Act
        var result = await svc.SearchAsync(filter);

        // Assert — Pattern: Non-admin search forces TenantId from requestContext.
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(TestTenantId, filter.TenantId,
            "Non-admin search should force TenantId from caller context.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_GlobalAdmin_SearchesAcrossTenants()
    {
        // Arrange — GlobalAdmin caller.
        _requestContextMock.Setup(r => r.Roles).Returns(new List<string> { "GlobalAdmin" });

        var filter = new CategorySearchFilter { TenantId = null };
        var response = new PagedResponse<CategoryDto> { Data = [], TotalCount = 0 };

        _repoQueryMock.Setup(r => r.QueryPageProjectionAsync(It.IsAny<CategorySearchFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var svc = CreateService();

        // Act
        var result = await svc.SearchAsync(filter);

        // Assert — Pattern: GlobalAdmin can search across all tenants.
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(filter.TenantId, "GlobalAdmin search should NOT force TenantId.");
    }

    // ═══════════════════════════════════════════════════════════════
    // GetAllForTenantAsync — Cache-aside (read-through) pattern
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetAllForTenantAsync_ReturnsCachedCategories()
    {
        // Arrange — FusionCache GetOrSetAsync returns cached categories.
        var categories = new List<CategoryDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Work" },
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, Name = "Personal" }
        };

        _fusionCacheMock.Setup(c => c.GetOrSetAsync(
                $"Categories:{TestTenantId}",
                It.IsAny<Func<FusionCacheFactoryExecutionContext<IReadOnlyList<CategoryDto>?>, CancellationToken, Task<IReadOnlyList<CategoryDto>?>>>(),
                It.IsAny<MaybeValue<IReadOnlyList<CategoryDto>?>>(),
                It.IsAny<FusionCacheEntryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories.AsReadOnly());

        var svc = CreateService();

        // Act
        var result = await svc.GetAllForTenantAsync(TestTenantId);

        // Assert — Pattern: Cache-aside returns the cached list without hitting repo.
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, result.Value!.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetAllForTenantAsync_CacheMiss_ReturnsEmptyList()
    {
        // Arrange — cache returns null (cache miss, factory returns null).
        _fusionCacheMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<FusionCacheFactoryExecutionContext<IReadOnlyList<CategoryDto>?>, CancellationToken, Task<IReadOnlyList<CategoryDto>?>>>(),
                It.IsAny<MaybeValue<IReadOnlyList<CategoryDto>?>>(),
                It.IsAny<FusionCacheEntryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CategoryDto>?)null);

        var svc = CreateService();

        // Act
        var result = await svc.GetAllForTenantAsync(TestTenantId);

        // Assert — Pattern: Null coalesced to empty list.
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Value!.Count);
    }
}
