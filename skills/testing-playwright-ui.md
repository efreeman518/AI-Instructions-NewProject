# Testing Playwright UI

Use this file for hosted browser UI tests in `Test.PlaywrightUI`.

## When to Load

- Load when writing or debugging Playwright UI tests.
- Load for Uno WASM and MudBlazor timing/selector rules.
- Skip for WAF endpoint/E2E tests.

## Harness Contract

Playwright requires a real hosted stack. It cannot run on `WebApplicationFactory`.

Run against Aspire AppHost locally, docker-compose stack, or preview deployment.

## Baseline Rules

- Use Page Object Model.
- Prefer stable selectors (`data-testid`).
- Isolate test data with unique names/ids.
- Assert structural UI strings, not data-dependent counts.

## Data-Dependent Assertion Anti-Pattern

Never assert seeded titles or exact row/page counts against shared dev DBs.

Bad examples:

- `"Showing 1 to 10 of 14 tasks"`
- `"Page 1 of 2"`
- specific seed task names

Good examples:

- column headers
- empty-state guidance text
- static labels and landmarks

## Uno WASM: Boot Once Per Describe

WASM cold-start is slow. Do not use default `{ page }` fixture for Uno.

Use serial describe + shared context/page in `beforeAll`.

```typescript
test.describe("EntityCrud", () => {
  test.describe.configure({ mode: "serial" });

  test.beforeAll(async ({ browser }) => {
    context = await browser.newContext({ ignoreHTTPSErrors: true });
    sharedPage = await context.newPage();
    await sharedPage.goto("https://localhost:7069");
    await waitForApp(sharedPage);
  });

  test.afterAll(async () => {
    await context.close();
  });
});
```

Set Playwright timeout to 120000 for suites containing Uno WASM cold-start.

`test.use({ viewport })` does not apply to beforeAll-owned contexts. Pass viewport to `browser.newContext({ viewport })`.

## Uno WASM: DOM/Click Strategy

Uno WASM often needs coordinate-click interaction.

- Query Uno elements by attributes like `xamltype` or `xamlautomationid`.
- Compute center with `getBoundingClientRect()`.
- Use `page.mouse.click(x, y)` (or down/up) with retry loop.
- Filter target text with known prefix (for example `E2E-`) to avoid collisions.

## Uno WASM: Slow Router After Many Navigations

Increase late-lifecycle assertions to 60000 when page loads occur after several navigations in same shared page.

## MudBlazor Timing Rules

- Before fill/click on a field after navigation, wait for visibility first.
- Confirmation dialogs may need 15000 timeout.

```typescript
await field.first().waitFor({ state: "visible" });
await expect(dialog).toBeVisible({ timeout: 15_000 });
```

## Playwright Config Output Location

Set `outputDir` under `Test/Test.PlaywrightUI`, not under app project directories.

## Verification Checklist

- [ ] Hosted stack is running and base URL is correct.
- [ ] Selector strategy is stable for target UI tech.
- [ ] Data assertions are structural, not seed/count-dependent.
- [ ] Timeout profile matches UI runtime behavior.
- [ ] Test output folder is inside test project.
