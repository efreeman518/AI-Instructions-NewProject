# Test Template — E2E

See [skills/testing.md](../skills/testing.md) for testing strategy and profile selection.

## Common Setup

Use Page Objects for selectors/flows and keep test methods focused on scenario intent (`create/edit/delete`, search, validation errors).

## Playwright E2E Tests

### File: `Test/Test.PlaywrightUI/Tests/{Entity}CrudTests.cs`

```csharp
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

[TestClass]
public class {Entity}CrudTests : PageTest
{
    private const string BaseUrl = "https://localhost:44318";

    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true };

    [TestInitialize]
    public async Task TestInitialize() => await Page.GotoAsync(BaseUrl);

    [TestMethod]
    [DataRow("item1", "suffix1")]
    [DataRow("item2", "suffix2")]
    public async Task AddEditDelete_Success(string baseName, string appendName)
    {
        // create row
        // edit row
        // delete row and assert not found
    }
}
```

### File: `Test/Test.PlaywrightUI/PageObjects/{Entity}PageObject.cs`

```csharp
public class {Entity}PageObject(IPage page)
{
    public Task NavigateAsync(string baseUrl) => page.GotoAsync($"{baseUrl}/{entity}");
    public Task FillNameAsync(string name) => page.FillAsync("#edit-name", name);
    public Task ClickSaveAsync() => page.ClickAsync("#btn-save");
    public Task ClickDeleteAsync(string itemName) => page.Locator($"tr:has-text('{itemName}') >> button.delete").ClickAsync();
    public async Task<bool> ItemExistsInGridAsync(string itemName) { /* selector wait */ }
    public async Task<bool> ItemNotInGridAsync(string itemName) { /* selector timeout */ }
}
```
