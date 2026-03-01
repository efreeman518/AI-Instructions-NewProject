using Microsoft.Playwright;

namespace Test.PlaywrightUI.PageObjects;

/// <summary>
/// Base class for page objects — provides shared navigation and browser references.
/// Kept for reference; tests use PageTest base class directly.
/// See https://github.com/efreeman518/EF.DemoApp1.net/blob/main/Test.PlaywrightUI/PageObjects/BasePageObject.cs
/// </summary>
public abstract class BasePageObject
{
    public abstract string PagePath { get; }
    public abstract IPage Page { get; set; }
    public abstract IBrowser Browser { get; set; }

    public async Task NavigateAsync()
    {
        Page = await Browser.NewPageAsync();
        await Page.GotoAsync(PagePath);
    }
}
