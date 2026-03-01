using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

// Enable parallel execution: adjust Workers to the desired concurrency
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace Test.PlaywrightUI.Tests;

/// <summary>
/// End-to-end UI tests for TaskFlow using Playwright with MSTest integration.
/// Based on https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.PlaywrightUI/Tests/TodoItemCrudTests.cs
///
/// Prerequisites:
///   1. Install Playwright browsers: pwsh bin/Debug/net10.0/playwright.ps1 install
///   2. Start the TaskFlow UI at the configured BaseUrl
/// </summary>
[TestClass]
public class TodoItemCrudTests : PageTest
{
    // TODO: Update to match the running TaskFlow UI URL
    private const string BaseUrl = "https://localhost:5001/";

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            // ViewportSize = new() { Width = 1920, Height = 1080 }
        };
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await Page.GotoAsync(BaseUrl);
    }

    // ── Data-driven CRUD test ────────────────────────────────

    [TestMethod]
    [DataRow("taskA", "123")]
    [DataRow("taskB", "456")]
    public async Task TodoItem_AddEditDelete_Success(string todoItemName, string appendName)
    {
        // Arrange — generate a unique item name
        var itemName = $"{todoItemName}-{DateTime.UtcNow.Ticks}";

        // Act — add a new item (selectors are placeholders; replace with actual UI selectors)
        await Page.FillAsync("[data-testid='todo-title-input']", itemName);
        await Page.ClickAsync("[data-testid='todo-save-btn']");

        // Assert — item appears in the list
        var itemInList = await Page.WaitForSelectorAsync(
            $"text={itemName}", new PageWaitForSelectorOptions { Timeout = 5000 });
        Assert.IsNotNull(itemInList, "Item should appear after creation");

        // Act — edit the item
        await Page.ClickAsync($"[data-testid='todo-edit-{itemName}']");
        var updatedName = itemName + appendName;
        await Page.FillAsync("[data-testid='todo-title-input']", updatedName);
        await Page.ClickAsync("[data-testid='todo-save-btn']");

        // Assert — updated item appears
        var updatedItem = await Page.WaitForSelectorAsync(
            $"text={updatedName}", new PageWaitForSelectorOptions { Timeout = 5000 });
        Assert.IsNotNull(updatedItem, "Updated item should appear");

        // Act — delete the item
        await Page.ClickAsync($"[data-testid='todo-delete-{updatedName}']");

        // Assert — item no longer exists
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(500);
        bool itemRemoved = false;
        try
        {
            await Page.WaitForSelectorAsync($"text={updatedName}",
                new PageWaitForSelectorOptions { Timeout = 1000 });
        }
        catch
        {
            itemRemoved = true;
        }
        Assert.IsTrue(itemRemoved, "Item should be removed after deletion");
    }

    // ── Simple add test ──────────────────────────────────────

    [TestMethod]
    public async Task TodoItem_AddNewItem_Success()
    {
        var itemName = $"SingleItem-{DateTime.UtcNow.Ticks}";

        await Page.FillAsync("[data-testid='todo-title-input']", itemName);
        await Page.ClickAsync("[data-testid='todo-save-btn']");

        var itemInList = await Page.WaitForSelectorAsync(
            $"text={itemName}", new PageWaitForSelectorOptions { Timeout = 5000 });
        Assert.IsNotNull(itemInList, "Item should exist after creation");
    }
}
