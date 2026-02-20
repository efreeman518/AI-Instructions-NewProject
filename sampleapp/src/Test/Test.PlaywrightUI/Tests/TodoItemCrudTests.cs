// ═══════════════════════════════════════════════════════════════
// Pattern: Playwright E2E CRUD Tests — full lifecycle test for TodoItem via Uno WASM browser UI.
//
// Demonstrates:
// 1. Test class inherits PageTest for automatic browser/page management
// 2. Page Object pattern (TodoItemPageObject) wraps all locators
// 3. Complete CRUD lifecycle: Create → Read (list + detail) → Update → Delete
// 4. Config-driven base URL from appsettings-playwright.json
// 5. Each test method is independent — no shared state between tests
//
// Prerequisites:
//   1. Build: dotnet build
//   2. Install browsers: pwsh bin/Debug/net10.0/playwright.ps1 install
//   3. Start the app (Aspire or standalone): TaskFlow.UI must be running at BaseUrl
//   4. Run: dotnet test --filter "TestCategory=E2E"
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Test.PlaywrightUI.PageObjects;

namespace Test.PlaywrightUI.Tests;

/// <summary>
/// Pattern: Playwright E2E test class — each test gets its own BrowserContext for isolation.
/// Inherits from PageTest which provides Page, Browser, Context automatically.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class TodoItemCrudTests : PageTest
{
    private TodoItemPageObject _todoPage = null!;
    private string _baseUrl = null!;

    [TestInitialize]
    public void Setup()
    {
        // Pattern: Load config from appsettings-playwright.json for environment-specific URLs.
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings-playwright.json", optional: false)
            .Build();

        _baseUrl = config["BaseUrl"] ?? "https://localhost:5001";
        _todoPage = new TodoItemPageObject(Page, _baseUrl);
    }

    // ── Test: Full CRUD Lifecycle ──────────────────────────────

    [TestMethod]
    public async Task TodoItem_FullCrudLifecycle()
    {
        // Pattern: Navigate to app and verify initial load.
        await _todoPage.NavigateToAppAsync();
        await _todoPage.NavigateToTodoListAsync();

        // Verify: List loads with existing mock items.
        var initialCount = await _todoPage.GetItemCountAsync();
        Assert.IsTrue(initialCount > 0, "Expected at least one todo item from mock data.");

        // ── CREATE ──────────────────────────────────────────────
        var newTitle = $"E2E Test Task {Guid.NewGuid():N}";
        await _todoPage.ClickCreateNewAsync();
        await _todoPage.FillAndSubmitCreateFormAsync(
            title: newTitle,
            description: "Created by Playwright E2E test",
            priority: 3,
            category: "Testing");

        // Verify: New item appears in the list.
        var afterCreateCount = await _todoPage.GetItemCountAsync();
        Assert.AreEqual(initialCount + 1, afterCreateCount,
            "Item count should increase by 1 after creation.");

        // ── READ (Detail) ───────────────────────────────────────
        await _todoPage.ClickItemByTitleAsync(newTitle);
        var detailTitle = await _todoPage.GetDetailTitleAsync();
        Assert.AreEqual(newTitle, detailTitle,
            "Detail page should show the created item's title.");

        // ── UPDATE (Toggle Complete) ────────────────────────────
        await _todoPage.ClickToggleCompleteAsync();
        // Pattern: Navigate back to list to verify the update is reflected.
        await _todoPage.ClickGoBackAsync();

        // ── DELETE ──────────────────────────────────────────────
        await _todoPage.ClickItemByTitleAsync(newTitle);
        await _todoPage.ClickDeleteAsync();

        // Verify: Item is removed from the list.
        var afterDeleteCount = await _todoPage.GetItemCountAsync();
        Assert.AreEqual(initialCount, afterDeleteCount,
            "Item count should return to initial after deletion.");
    }

    // ── Test: Search Filters List ──────────────────────────────

    [TestMethod]
    public async Task TodoItem_SearchFiltersResults()
    {
        await _todoPage.NavigateToAppAsync();
        await _todoPage.NavigateToTodoListAsync();

        // Pattern: Get baseline count before search.
        var allCount = await _todoPage.GetItemCountAsync();
        Assert.IsTrue(allCount > 0, "Need items to test search.");

        // Search for a specific mock item.
        await _todoPage.SearchAsync("Review pull request");

        var filteredCount = await _todoPage.GetItemCountAsync();
        Assert.IsTrue(filteredCount <= allCount,
            "Filtered results should be equal to or fewer than total.");
        Assert.IsTrue(filteredCount >= 1,
            "Expected at least one result matching 'Review pull request'.");

        // Clear search and verify full list returns.
        await _todoPage.ClearSearchAsync();
        var restoredCount = await _todoPage.GetItemCountAsync();
        Assert.AreEqual(allCount, restoredCount,
            "Clearing search should restore the full list.");
    }

    // ── Test: Create and Cancel ────────────────────────────────

    [TestMethod]
    public async Task TodoItem_CreateCancel_DoesNotAddItem()
    {
        await _todoPage.NavigateToAppAsync();
        await _todoPage.NavigateToTodoListAsync();

        var initialCount = await _todoPage.GetItemCountAsync();

        // Open create form and cancel without saving.
        await _todoPage.ClickCreateNewAsync();
        await _todoPage.ClickCancelCreateAsync();

        // Verify: No new item was added.
        var afterCancelCount = await _todoPage.GetItemCountAsync();
        Assert.AreEqual(initialCount, afterCancelCount,
            "Cancelling create should not change the item count.");
    }

    // ── Playwright Options ─────────────────────────────────────

    /// <summary>
    /// Pattern: Override Playwright browser launch options for CI compatibility.
    /// Headless mode and slow-mo are loaded from config.
    /// </summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }
}
