// ═══════════════════════════════════════════════════════════════
// Pattern: Page Object — encapsulates UI element locators for Uno WASM-rendered pages.
// Follows the Page Object Model pattern for maintainable E2E tests.
//
// Key patterns demonstrated:
// 1. Locators reference Uno WASM elements by AutomationId / text / role
// 2. Async methods wrap common user interactions (click, fill, wait)
// 3. Assertions stay in test classes — page objects only expose actions + queries
// ═══════════════════════════════════════════════════════════════

using Microsoft.Playwright;

namespace Test.PlaywrightUI.PageObjects;

/// <summary>
/// Pattern: Page Object for the TodoItem UI — wraps all locators for the
/// Uno WASM-rendered TodoItem list, detail, and create pages.
/// Tests use this class instead of raw locators for maintainability.
/// </summary>
public class TodoItemPageObject
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public TodoItemPageObject(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    // ── Navigation ─────────────────────────────────────────────

    /// <summary>Navigate to the app root and wait for the Uno WASM host to load.</summary>
    public async Task NavigateToAppAsync()
    {
        await _page.GotoAsync(_baseUrl);
        // Pattern: Uno WASM apps render inside a canvas or managed DOM.
        // Wait for the app shell to appear indicating the WASM runtime loaded.
        await _page.WaitForSelectorAsync("[data-automation='Shell']",
            new() { Timeout = 30_000 });
    }

    /// <summary>Navigate to the Todo Items tab.</summary>
    public async Task NavigateToTodoListAsync()
    {
        // Pattern: Use text locator for Uno TabBar items.
        await _page.GetByText("Tasks").ClickAsync();
        await WaitForListLoadedAsync();
    }

    // ── List Page Actions ──────────────────────────────────────

    /// <summary>Wait for the todo list FeedView to finish loading.</summary>
    public async Task WaitForListLoadedAsync()
    {
        // Pattern: Wait for a known list item to appear, indicating FeedView
        // has transitioned from Loading → Data state.
        await _page.WaitForSelectorAsync("[data-automation='TodoItemList']",
            new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
    }

    /// <summary>Get the count of visible todo items in the list.</summary>
    public async Task<int> GetItemCountAsync()
    {
        var items = await _page.QuerySelectorAllAsync("[data-automation='TodoItemRow']");
        return items.Count;
    }

    /// <summary>Search for tasks by entering text in the search box.</summary>
    public async Task SearchAsync(string searchTerm)
    {
        var searchBox = _page.GetByPlaceholder("Search tasks...");
        await searchBox.FillAsync(searchTerm);
        // Pattern: Allow reactive feed update to propagate.
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>Clear the search box.</summary>
    public async Task ClearSearchAsync()
    {
        var searchBox = _page.GetByPlaceholder("Search tasks...");
        await searchBox.FillAsync(string.Empty);
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>Click the "+ New" button to navigate to the create form.</summary>
    public async Task ClickCreateNewAsync()
    {
        await _page.GetByText("+ New").ClickAsync();
        // Pattern: Wait for the create page form to appear.
        await _page.WaitForSelectorAsync("[data-automation='CreateTodoItemPage']",
            new() { Timeout = 10_000 });
    }

    /// <summary>Click on a todo item by its title to navigate to detail.</summary>
    public async Task ClickItemByTitleAsync(string title)
    {
        await _page.GetByText(title).ClickAsync();
        await _page.WaitForSelectorAsync("[data-automation='TodoItemDetailPage']",
            new() { Timeout = 10_000 });
    }

    // ── Create Page Actions ────────────────────────────────────

    /// <summary>Fill in the create todo item form and submit.</summary>
    public async Task FillAndSubmitCreateFormAsync(
        string title,
        string description = "",
        int priority = 1,
        string? category = null)
    {
        // Pattern: Fill form fields mapped by Uno AutomationId or placeholder text.
        await _page.GetByPlaceholder("Task title").FillAsync(title);

        if (!string.IsNullOrEmpty(description))
        {
            await _page.GetByPlaceholder("Description (optional)").FillAsync(description);
        }

        // Pattern: NumberBox interaction — clear and type the priority value.
        var priorityBox = _page.GetByLabel("Priority");
        await priorityBox.FillAsync(priority.ToString());

        if (category is not null)
        {
            // Pattern: ComboBox selection in Uno WASM — click the picker, then select item.
            var categoryPicker = _page.GetByLabel("Category");
            await categoryPicker.ClickAsync();
            await _page.GetByText(category).ClickAsync();
        }

        // Pattern: Click Save to submit the form.
        await _page.GetByText("Save").ClickAsync();
        // Wait for navigation back to list.
        await WaitForListLoadedAsync();
    }

    /// <summary>Click Cancel on the create form.</summary>
    public async Task ClickCancelCreateAsync()
    {
        await _page.GetByText("Cancel").ClickAsync();
        await WaitForListLoadedAsync();
    }

    // ── Detail Page Actions ────────────────────────────────────

    /// <summary>Get the title text displayed on the detail page.</summary>
    public async Task<string> GetDetailTitleAsync()
    {
        var element = await _page.WaitForSelectorAsync("[data-automation='DetailTitle']");
        return await element!.InnerTextAsync();
    }

    /// <summary>Toggle the completion status on the detail page.</summary>
    public async Task ClickToggleCompleteAsync()
    {
        await _page.GetByText("Toggle Complete").ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>Delete the current item from the detail page.</summary>
    public async Task ClickDeleteAsync()
    {
        await _page.GetByText("Delete").ClickAsync();
        // Pattern: Wait for navigation back to the list after deletion.
        await WaitForListLoadedAsync();
    }

    /// <summary>Click the Back button on the detail page.</summary>
    public async Task ClickGoBackAsync()
    {
        await _page.GetByText("Back").ClickAsync();
        await WaitForListLoadedAsync();
    }
}
