// ═══════════════════════════════════════════════════════════════
// Pattern: Repository query tests — TodoItemRepositoryQuery.
// Dual DB strategy: InMemory (fast) + SQLite (full SQL support for Like/Contains).
// Demonstrates: BuildFilter expression testing, paged projection, sort ordering,
// InMemoryDbBuilder fluent API, SQLite in-memory for SQL-specific behavior.
//
// Note: This file tests the repository's query logic directly against a real
// DbContext (not mocked), verifying that EF LINQ expressions translate correctly.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Entities;
using Domain.Model.Enums;
using Infrastructure;
using Infrastructure.Repositories;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Test.Support;

namespace Test.Unit.Repositories;

[TestClass]
public class TodoItemRepositoryQueryTests
{
    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-00000000000B");
    private static readonly Guid CategoryId = Guid.NewGuid();

    // ═══════════════════════════════════════════════════════════════
    // Seed Helper — creates a known set of TodoItems for filter tests.
    // ═══════════════════════════════════════════════════════════════

    private static void SeedTestData(TaskFlowDbContextQuery dbContext)
    {
        // Pattern: Use entity factory methods (Create) to get valid entities.
        // For InMemory tests, we bypass domain rules when needed by using the result directly.

        var item1 = TodoItem.Create(TenantA, "Buy groceries", "Milk and eggs", 3, null, CategoryId, null, null, null).Value!;
        var item2 = TodoItem.Create(TenantA, "Write report", "Q4 financials", 5, 8m, CategoryId, null, null, null).Value!;
        var item3 = TodoItem.Create(TenantA, "Fix bug #123", "Null reference in checkout", 4, 2m, null, null, null, null).Value!;
        var item4 = TodoItem.Create(TenantB, "Deploy staging", "v2.1 release", 2, null, null, null, null, null).Value!;
        var item5 = TodoItem.Create(TenantA, "Archived task", "Old work", 1, null, null, null, null, null).Value!;

        dbContext.Set<TodoItem>().AddRange(item1, item2, item3, item4, item5);
        dbContext.SaveChanges();
    }

    // ═══════════════════════════════════════════════════════════════
    // InMemory Tests — fast, no SQL dependencies
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_FilterByTenant_ReturnsOnlyTenantItems()
    {
        // Arrange — Pattern: InMemoryDbBuilder for fast, isolated tests.
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter { TenantId = TenantA },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: Tenant filter returns only Tenant A items.
        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.TotalCount, "TenantA should have 4 items.");
        Assert.IsTrue(result.Data.All(d => d.TenantId == TenantA));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_FilterByCategory_ReturnsMatchingItems()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter { CategoryId = CategoryId },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: Category filter narrows results.
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalCount, "Two items have CategoryId set.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_FilterByPriorityRange_ReturnsMatchingItems()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter { MinPriority = 4, MaxPriority = 5 },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: Priority range filter (4 <= p <= 5).
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalCount, "Two items have priority 4 or 5.");
        Assert.IsTrue(result.Data.All(d => d.Priority >= 4 && d.Priority <= 5));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_NoFilter_ReturnsAllItems()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = null,
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: Null filter returns everything.
        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.TotalCount, "All 5 items should be returned.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_Paging_ReturnsCorrectPage()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter { TenantId = TenantA },
            PageSize = 2,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: Paged results return correct page size.
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Data.Count, "Page 1 should have 2 items.");
        Assert.AreEqual(4, result.TotalCount, "Total count should reflect all matching items.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_SortByPriority_ReturnsOrdered()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter { TenantId = TenantA },
            PageSize = 10,
            PageIndex = 1,
            SortField = "priority",
            SortDescending = true
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: Custom sort via BuildOrderBy.
        Assert.IsNotNull(result);
        var priorities = result.Data.Select(d => d.Priority).ToList();
        CollectionAssert.AreEqual(
            priorities.OrderByDescending(p => p).ToList(),
            priorities,
            "Items should be sorted by priority descending.");
    }

    // ═══════════════════════════════════════════════════════════════
    // SQLite Tests — full SQL support (Like, Contains, computed columns)
    // Pattern: Use BuildSQLite when testing SQL-specific behavior
    // that the InMemory provider doesn't support accurately.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_SQLite_FilterByTitle_UsesContains()
    {
        // Arrange — Pattern: SQLite in-memory for SQL-accurate Contains/Like behavior.
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildSQLite<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter { Title = "bug" },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: String.Contains translates to SQL LIKE '%bug%'.
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalCount, "Only 'Fix bug #123' should match.");
        Assert.IsTrue(result.Data[0].Title.Contains("bug", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task SearchAsync_SQLite_MultipleFilters_CombineWithAnd()
    {
        // Arrange — Pattern: Multiple filter properties combine with AND.
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildSQLite<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);
        var request = new Package.Infrastructure.Common.SearchRequest<Application.Models.TodoItem.TodoItemSearchFilter>
        {
            Filter = new Application.Models.TodoItem.TodoItemSearchFilter
            {
                TenantId = TenantA,
                CategoryId = CategoryId,
                MinPriority = 3
            },
            PageSize = 10,
            PageIndex = 1
        };

        // Act
        var result = await repo.SearchTodoItemsAsync(request);

        // Assert — Pattern: All filter conditions applied together.
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Data.All(d =>
            d.TenantId == TenantA && d.CategoryId == CategoryId && d.Priority >= 3));
    }

    // ═══════════════════════════════════════════════════════════════
    // Lookup — lightweight autocomplete query
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LookupAsync_FiltersByTenantAndSearch()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);

        // Act
        var result = await repo.LookupTodoItemsAsync(TenantA, "Buy");

        // Assert — Pattern: Lookup returns lightweight StaticItem list.
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Data.Count, "Only 'Buy groceries' should match.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task LookupAsync_NullSearch_ReturnsAllForTenant()
    {
        // Arrange
        var db = new InMemoryDbBuilder()
            .UseEntityData(ctx => SeedTestData((TaskFlowDbContextQuery)ctx))
            .BuildInMemory<TaskFlowDbContextQuery>();

        var repo = new TodoItemRepositoryQuery(db);

        // Act
        var result = await repo.LookupTodoItemsAsync(TenantA, null);

        // Assert — Pattern: Null search returns all tenant items.
        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.Data.Count, "All TenantA items should be returned.");
    }
}
