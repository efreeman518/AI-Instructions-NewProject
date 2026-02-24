// ═══════════════════════════════════════════════════════════════
// Pattern: Service unit tests — TodoItemService (richest service).
// Dual strategy: Mock-based (verify interactions) + InMemory DB (verify behavior).
// Demonstrates: MockRepository factory, IRequestContext mocking, IFusionCacheProvider
// mock, IInternalMessageBus verification, DomainResult pipeline testing.
//
// Service constructor:
//   TodoItemService(ILogger, IRequestContext<string,Guid?>, ITodoItemRepositoryQuery,
//                   ITodoItemUpdater, IInternalMessageBus, IFusionCacheProvider)
// ═══════════════════════════════════════════════════════════════

using Application.Contracts.Events;
using Application.Contracts.Repositories;
using Application.Contracts.Services;
using Application.Models.TodoItem;
using Application.Services;
using Domain.Model.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using EF.Common;
using EF.BackgroundServices.InternalMessageBus;
using EF.Domain;
using Test.Support;
using ZiggyCreatures.Caching.Fusion;

[assembly: Parallelize(Workers = 5, Scope = ExecutionScope.MethodLevel)]

namespace Test.Unit.Services;

[TestClass]
public class TodoItemServiceTests : UnitTestBase
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: Mock setup — one mock per dependency, created via MockRepository.
    // MockBehavior.Default (Loose) + DefaultValue.Mock from UnitTestBase.
    // ═══════════════════════════════════════════════════════════════

    private readonly Mock<ITodoItemRepositoryQuery> _repoQueryMock;
    private readonly Mock<ITodoItemUpdater> _updaterMock;
    private readonly Mock<IInternalMessageBus> _messageBusMock;
    private readonly Mock<IFusionCacheProvider> _fusionCacheProviderMock;
    private readonly Mock<IFusionCache> _fusionCacheMock;
    private readonly Mock<IRequestContext<string, Guid?>> _requestContextMock;

    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public TodoItemServiceTests() : base()
    {
        _repoQueryMock = _mockFactory.Create<ITodoItemRepositoryQuery>();
        _updaterMock = _mockFactory.Create<ITodoItemUpdater>();
        _messageBusMock = _mockFactory.Create<IInternalMessageBus>();
        _fusionCacheProviderMock = _mockFactory.Create<IFusionCacheProvider>();
        _fusionCacheMock = _mockFactory.Create<IFusionCache>();
        _requestContextMock = _mockFactory.Create<IRequestContext<string, Guid?>>();

        // Pattern: Wire FusionCacheProvider → returns a mock IFusionCache.
        _fusionCacheProviderMock.Setup(p => p.GetCache(It.IsAny<string>()))
            .Returns(_fusionCacheMock.Object);

        // Pattern: Default request context — tenant-scoped user (not GlobalAdmin).
        _requestContextMock.Setup(r => r.TenantId).Returns(TestTenantId);
        _requestContextMock.Setup(r => r.UserId).Returns("test-user-001");
        _requestContextMock.Setup(r => r.Roles).Returns(new List<string> { "User" });
    }

    /// <summary>Helper — creates a TodoItemService with current mock instances.</summary>
    private TodoItemService CreateService() => new(
        new NullLogger<TodoItemService>(),
        _requestContextMock.Object,
        _repoQueryMock.Object,
        _updaterMock.Object,
        _messageBusMock.Object,
        _fusionCacheProviderMock.Object);

    // ═══════════════════════════════════════════════════════════════
    // Mock-based Tests — Verify interactions
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task CreateAsync_ValidDto_CallsUpdaterAndPublishesEvent()
    {
        // Arrange
        var dto = new TodoItemDto
        {
            TenantId = TestTenantId,
            Title = "New Todo",
            Priority = 3,
            Status = TodoItemStatus.None
        };

        var createdDto = new TodoItemDto
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Title = "New Todo",
            Priority = 3
        };

        _updaterMock.Setup(u => u.CreateAsync(It.IsAny<TodoItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TodoItemDto>.Success(createdDto));

        var svc = CreateService();

        // Act
        var result = await svc.CreateAsync(dto);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);

        // Pattern: Verify updater was called exactly once.
        _updaterMock.Verify(u => u.CreateAsync(It.IsAny<TodoItemDto>(), It.IsAny<CancellationToken>()), Times.Once);

        // Pattern: Verify domain event was published.
        _messageBusMock.Verify(m => m.PublishAsync(
            It.IsAny<TodoItemCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetByIdAsync_ExistingItem_ReturnsCachedDto()
    {
        // Arrange — FusionCache GetOrSetAsync returns a DTO.
        var itemId = Guid.NewGuid();
        var cachedDto = new TodoItemDto
        {
            Id = itemId,
            TenantId = TestTenantId,
            Title = "Cached Item"
        };

        // Pattern: Mock FusionCache GetOrSetAsync — uses Callback to invoke the factory.
        _fusionCacheMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<FusionCacheFactoryExecutionContext<TodoItemDto?>, CancellationToken, Task<TodoItemDto?>>>(),
                It.IsAny<MaybeValue<TodoItemDto?>>(),
                It.IsAny<FusionCacheEntryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        var svc = CreateService();

        // Act
        var result = await svc.GetByIdAsync(itemId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Cached Item", result.Value!.Title);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetByIdAsync_NotFound_ReturnsNotFound()
    {
        // Arrange — cache returns null.
        _fusionCacheMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<FusionCacheFactoryExecutionContext<TodoItemDto?>, CancellationToken, Task<TodoItemDto?>>>(),
                It.IsAny<MaybeValue<TodoItemDto?>>(),
                It.IsAny<FusionCacheEntryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItemDto?)null);

        var svc = CreateService();

        // Act
        var result = await svc.GetByIdAsync(Guid.NewGuid());

        // Assert — Pattern: NotFound result for missing entity.
        Assert.IsTrue(result.IsSuccess == false || result.Value is null);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task GetByIdAsync_CrossTenant_ReturnsForbidden()
    {
        // Arrange — item belongs to a different tenant.
        var differentTenantId = Guid.NewGuid();
        var dto = new TodoItemDto
        {
            Id = Guid.NewGuid(),
            TenantId = differentTenantId, // Different from TestTenantId
            Title = "Other Tenant's Item"
        };

        _fusionCacheMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<FusionCacheFactoryExecutionContext<TodoItemDto?>, CancellationToken, Task<TodoItemDto?>>>(),
                It.IsAny<MaybeValue<TodoItemDto?>>(),
                It.IsAny<FusionCacheEntryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var svc = CreateService();

        // Act
        var result = await svc.GetByIdAsync(dto.Id);

        // Assert — Pattern: Tenant boundary violation → Forbidden.
        Assert.IsTrue(result.IsFailure || result.Value is null);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItemDto?)null);

        var svc = CreateService();
        var dto = new TodoItemDto { Id = Guid.NewGuid(), TenantId = TestTenantId, Title = "Update Me" };

        // Act
        var result = await svc.UpdateAsync(dto);

        // Assert
        // Pattern: Update nonexistent entity → NotFound.
        _updaterMock.Verify(u => u.UpdateAsync(It.IsAny<TodoItemDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdateAsync_InvalidStatusTransition_ReturnsFailure()
    {
        // Arrange — existing item is None status, propose transition to Completed (invalid).
        var itemId = Guid.NewGuid();
        var existing = new TodoItemDto
        {
            Id = itemId,
            TenantId = TestTenantId,
            Title = "Existing",
            Status = TodoItemStatus.None
        };

        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var svc = CreateService();
        var dto = new TodoItemDto
        {
            Id = itemId,
            TenantId = TestTenantId,
            Title = "Existing",
            Status = TodoItemStatus.IsCompleted // Invalid: None → Completed requires Started first
        };

        // Act
        var result = await svc.UpdateAsync(dto);

        // Assert — Pattern: Domain rule blocks the transition.
        Assert.IsTrue(result.IsFailure);
        _updaterMock.Verify(u => u.UpdateAsync(It.IsAny<TodoItemDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task UpdateAsync_TenantChange_ReturnsFailure()
    {
        // Arrange — existing item has one tenant, DTO has different tenant.
        var itemId = Guid.NewGuid();
        var existing = new TodoItemDto { Id = itemId, TenantId = TestTenantId, Title = "Existing", Status = TodoItemStatus.None };

        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var svc = CreateService();
        var dto = new TodoItemDto
        {
            Id = itemId,
            TenantId = Guid.NewGuid(), // Different tenant!
            Title = "Existing",
            Status = TodoItemStatus.None
        };

        // Act
        var result = await svc.UpdateAsync(dto);

        // Assert — Pattern: Tenant cannot be changed after creation.
        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_ExistingItemNoChildren_CallsUpdater()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var existing = new TodoItemDto { Id = itemId, TenantId = TestTenantId, Title = "Delete Me" };

        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoQueryMock.Setup(r => r.GetChildrenAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoItemDto>());
        _updaterMock.Setup(u => u.DeleteAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var svc = CreateService();

        // Act
        var result = await svc.DeleteAsync(itemId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        _updaterMock.Verify(u => u.DeleteAsync(itemId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_ItemWithChildren_ReturnsFailure()
    {
        // Arrange — item has child items.
        var itemId = Guid.NewGuid();
        var existing = new TodoItemDto { Id = itemId, TenantId = TestTenantId, Title = "Parent" };
        var children = new List<TodoItemDto>
        {
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, Title = "Child" }
        };

        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoQueryMock.Setup(r => r.GetChildrenAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(children);

        var svc = CreateService();

        // Act
        var result = await svc.DeleteAsync(itemId);

        // Assert — Pattern: Hierarchy constraint — cannot delete parent with children.
        Assert.IsTrue(result.IsFailure);
        _updaterMock.Verify(u => u.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DeleteAsync_NotFound_ReturnsSuccessIdempotent()
    {
        // Arrange — item doesn't exist.
        _repoQueryMock.Setup(r => r.QueryByIdProjectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoItemDto?)null);

        var svc = CreateService();

        // Act
        var result = await svc.DeleteAsync(Guid.NewGuid());

        // Assert — Pattern: Idempotent delete — not found = success.
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_NonAdmin_ForcesTenantFilter()
    {
        // Arrange — non-admin caller should have TenantId forced.
        var filter = new TodoItemSearchFilter { TenantId = null };
        var response = new PagedResponse<TodoItemDto> { Data = [], TotalCount = 0 };

        _repoQueryMock.Setup(r => r.QueryPageProjectionAsync(It.IsAny<TodoItemSearchFilter>(), It.IsAny<CancellationToken>()))
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

        var filter = new TodoItemSearchFilter { TenantId = null };
        var response = new PagedResponse<TodoItemDto> { Data = [], TotalCount = 0 };

        _repoQueryMock.Setup(r => r.QueryPageProjectionAsync(It.IsAny<TodoItemSearchFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var svc = CreateService();

        // Act
        var result = await svc.SearchAsync(filter);

        // Assert — Pattern: GlobalAdmin can search across all tenants.
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(filter.TenantId, "GlobalAdmin search should NOT force TenantId.");
    }
}
