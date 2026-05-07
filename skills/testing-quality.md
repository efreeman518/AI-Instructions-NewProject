# Testing — Quality Gates & Hosted UI

Use this skill for Phase 5d quality suites and release hardening (architecture, hosted Playwright UI, load, benchmarks). For Phase 5a/5b unit/endpoint authoring and Aspire-hosted integration fixtures, load [testing.md](testing.md) instead.

## Quality Gate Suites

- `Test.Architecture`: layering rules (NetArchTest)
- `Test.PlaywrightUI`: hosted browser UI checks
- `Test.Load`: NBomber scenario thresholds
- `Test.Benchmarks`: BenchmarkDotNet regression tracking

## Architecture Rules

Assert:

- Domain does not depend on Infrastructure/Application/EF
- Application does not depend on Infrastructure
- API avoids direct persistence coupling

## Load Rules

- Start with one critical endpoint scenario.
- Define rps and duration explicitly.
- Track p50/p95/p99 and error rate.

## Benchmark Rules

- Use realistic setup and representative datasets.
- Benchmark hot paths only.
- Compare trends over time; do not use one-off numbers as hard pass/fail without baseline.

## Optional Extras

- Mutation testing (Stryker) for high-value domain/service paths.
- Coverage settings via `coverlet.runsettings` for stable CI behavior.

## Release Matrix

| Need | Recommended profile |
|---|---|
| Fast startup | `minimal` |
| Team default | `balanced` |
| Release hardening | `comprehensive` |

## Slice Gate by Profile

- `minimal`: Unit + Endpoint
- `balanced`: Unit + Endpoint + Integration + Architecture
- `comprehensive`: balanced + PlaywrightUI + Load + Benchmarks (when scenario enabled)

If a slice spans multiple entities/stores, run at least one integration path that covers the full composite flow.

---

## Hosted Browser UI (Test.PlaywrightUI)

### Harness Contract

Playwright requires a real hosted stack. It cannot run on `WebApplicationFactory`. Run against Aspire AppHost locally, a docker-compose stack, or a preview deployment.

### Baseline Rules

- Use Page Object Model.
- Prefer stable selectors (`data-testid`).
- Isolate test data with unique names/ids.
- Assert structural UI strings, not data-dependent counts.

### Data-Dependent Assertion Anti-Pattern

Never assert seeded titles or exact row/page counts against shared dev DBs.

Bad: `"Showing 1 to 10 of 14 tasks"`, `"Page 1 of 2"`, specific seed task names.
Good: column headers, empty-state guidance text, static labels and landmarks.

### Uno WASM: Boot Once Per Describe

WASM cold-start is slow. Do not use the default `{ page }` fixture for Uno. Use serial describe + shared context/page in `beforeAll`.

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

Set Playwright timeout to `120000` for suites containing Uno WASM cold-start.

`test.use({ viewport })` does not apply to `beforeAll`-owned contexts. Pass viewport to `browser.newContext({ viewport })`.

### Uno WASM: DOM/Click Strategy

Uno WASM often needs coordinate-click interaction.

- Query Uno elements by attributes like `xamltype` or `xamlautomationid`.
- Compute center with `getBoundingClientRect()`.
- Use `page.mouse.click(x, y)` (or down/up) with retry loop.
- Filter target text with known prefix (for example `E2E-`) to avoid collisions.

### Uno WASM: Slow Router After Many Navigations

Increase late-lifecycle assertions to `60000` when page loads occur after several navigations in same shared page.

### MudBlazor Timing Rules

- Before fill/click on a field after navigation, wait for visibility first.
- Confirmation dialogs may need `15000` timeout.

```typescript
await field.first().waitFor({ state: "visible" });
await expect(dialog).toBeVisible({ timeout: 15_000 });
```

### Playwright Config Output Location

Set `outputDir` under `Test/Test.PlaywrightUI`, not under app project directories.

## Verification Checklist

- [ ] Architecture tests enforce layering rules.
- [ ] Load scenarios track p50/p95/p99 and error rate against an explicit baseline.
- [ ] Benchmark suites use representative datasets; results compared to a baseline, not measured in isolation.
- [ ] Hosted Playwright stack is reachable and base URL is correct.
- [ ] Selector strategy is stable for the target UI tech.
- [ ] UI assertions are structural, not seed/count-dependent.
- [ ] Timeout profile matches UI runtime behavior (Uno WASM cold-start: 120s).
- [ ] Test output folder is inside the test project.
