# Test.PlaywrightUI — Playwright MSTest Integration

End-to-end UI tests for TaskFlow using Playwright with MSTest.

## Pattern Reference
Based on [EF.DemoApp1.net/Test.PlaywrightUI](https://github.com/efreeman518/EF.DemoApp1.net/tree/main/Test.PlaywrightUI).

## Structure

```
Test.PlaywrightUI/
├── Tests/
│   └── TodoItemCrudTests.cs
├── PageObjects/
│   └── BasePageObject.cs          (kept for reference)
└── Test.PlaywrightUI.csproj
```

## Prerequisites

1. Install Playwright browsers:
   ```powershell
   pwsh bin/Debug/net10.0/playwright.ps1 install
   ```
2. Start the TaskFlow UI at the configured `BaseUrl`.

## Running Tests

```bash
dotnet test Test/Test.PlaywrightUI/Test.PlaywrightUI.csproj
```

## Key Patterns

- Tests inherit from `PageTest` (from `Microsoft.Playwright.MSTest`), which provides the `Page` property.
- `BrowserNewContextOptions` overridden to ignore HTTPS errors for local dev.
- Data-driven tests use MSTest `[DataRow]` attribute.
- Browser lifecycle managed automatically by Playwright MSTest integration.

## Selectors

Selectors use `data-testid` attributes. When the Uno UI is wired up, update selectors to match actual elements.
