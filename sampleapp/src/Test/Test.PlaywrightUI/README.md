# Test.PlaywrightUI — Playwright E2E Browser Tests

End-to-end browser tests for the TaskFlow Uno Platform WASM application using [Microsoft Playwright for .NET](https://playwright.dev/dotnet/).

---

## Prerequisites

- **.NET SDK** (latest stable)
- **Node.js** (required by Playwright for browser management)
- **TaskFlow.UI** running at a reachable URL (default: `https://localhost:5001`)

---

## Setup

### 1. Build the test project

```bash
cd sampleapp/src/Test/Test.PlaywrightUI
dotnet build
```

### 2. Install Playwright browsers

After building, run the Playwright install script to download Chromium, Firefox, and WebKit:

```bash
# Windows
pwsh bin/Debug/net10.0/playwright.ps1 install

# macOS / Linux
bash bin/Debug/net10.0/playwright.sh install
```

### 3. Configure the base URL

Edit `appsettings-playwright.json` to point to your running app:

```json
{
  "BaseUrl": "https://localhost:5001",
  "Headless": true,
  "TestUser": {
    "Email": "testuser@taskflow.dev",
    "Password": "Test1234!"
  }
}
```

For CI, override via environment variables or a CI-specific appsettings file.

---

## Running Tests

### Run all E2E tests

```bash
dotnet test --filter "TestCategory=E2E"
```

### Run with headed browser (for debugging)

```bash
dotnet test --filter "TestCategory=E2E" -- Playwright.LaunchOptions.Headless=false
```

### Run a specific test

```bash
dotnet test --filter "FullyQualifiedName~TodoItemCrudTests.TodoItem_FullCrudLifecycle"
```

---

## Architecture

```
Test.PlaywrightUI/
├── Test.PlaywrightUI.csproj        # MSTest + Microsoft.Playwright.MSTest
├── appsettings-playwright.json     # Config: BaseUrl, Headless, test user
├── PageObjects/
│   └── TodoItemPageObject.cs       # Page Object encapsulating UI locators
├── Tests/
│   └── TodoItemCrudTests.cs        # Full CRUD lifecycle + search + cancel tests
└── README.md                       # This file
```

### Page Object Pattern

Tests use the Page Object pattern (`TodoItemPageObject`) to encapsulate all UI locators and interactions. This means:

- **Locators change in one place** — if the UI restructures, only the page object updates
- **Tests read like user stories** — `ClickCreateNew → FillForm → Submit → VerifyInList`
- **No raw selectors in tests** — all interaction goes through typed methods

### Uno WASM Specifics

The Uno Platform renders WASM apps using either managed DOM elements or a Canvas. Locators use:

- `[data-automation='...']` attributes set via `AutomationProperties.AutomationId`
- Text-based locators via `GetByText()` for buttons and labels
- Placeholder text locators via `GetByPlaceholder()` for text inputs

---

## CI Integration

Add to your GitHub Actions / Azure DevOps pipeline:

```yaml
- name: Install Playwright
  run: pwsh Test/Test.PlaywrightUI/bin/Release/net10.0/playwright.ps1 install --with-deps

- name: Run E2E Tests
  run: dotnet test Test/Test.PlaywrightUI --configuration Release --filter "TestCategory=E2E"
  env:
    BaseUrl: ${{ secrets.E2E_APP_URL }}
```

> **Note:** The app must be running (e.g., via `dotnet run` in a preceding step or a deployed staging environment) before E2E tests execute.

---

## Troubleshooting

| Issue | Solution |
|-------|---------|
| `Browser was not found` | Run `playwright.ps1 install` after building |
| `Timeout waiting for selector` | Ensure the app is running and `BaseUrl` is correct |
| `SSL errors` | `IgnoreHTTPSErrors = true` is set in `ContextOptions()` |
| `Element not visible` | Uno WASM may render asynchronously — increase `WaitForSelector` timeout |
