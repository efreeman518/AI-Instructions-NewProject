# Testing

Entry point for test scaffolding guidance. Load only the focused file you need for the current phase and harness.

Reference patterns: [../patterns/expected-output-index.md](../patterns/expected-output-index.md) (Testing).

## Skill File Map

| File | Load when |
|---|---|
| [testing-core.md](testing-core.md) | Default for Phase 5a/5b. Covers profiles, harness tiers, categories, assertions, Unit/Endpoint/E2E patterns, and builders. |
| [testing-integration-hosts.md](testing-integration-hosts.md) | Service-level integration and Aspire shared-host fixture rules (`DistributedApplicationTestingBuilder`). |
| [testing-playwright-ui.md](testing-playwright-ui.md) | Hosted browser UI tests (`Test.PlaywrightUI`), Uno WASM, MudBlazor timing rules, selector strategy. |
| [testing-quality-gates.md](testing-quality-gates.md) | Phase 5d quality suites: architecture, hosted UI gate, load, benchmarks. |
| [testing-templates-map.md](testing-templates-map.md) | Fast route from phase to exact template file(s) to load. |

## Quick Routing

- Unit/Endpoint first: load [testing-core.md](testing-core.md)
- Integration fixture trouble: load [testing-integration-hosts.md](testing-integration-hosts.md)
- Playwright/UI trouble: load [testing-playwright-ui.md](testing-playwright-ui.md)
- Release hardening: load [testing-quality-gates.md](testing-quality-gates.md)

## Template References

- Domain: [../templates/test-templates-domain.md](../templates/test-templates-domain.md)
- Repository: [../templates/test-templates-repository.md](../templates/test-templates-repository.md)
- Service: [../templates/test-templates-service.md](../templates/test-templates-service.md)
- Endpoint: [../templates/test-templates-endpoint.md](../templates/test-templates-endpoint.md)
- Quality: [../templates/test-templates-quality.md](../templates/test-templates-quality.md)
- Full reference fallback: [../templates/test-templates.md](../templates/test-templates.md)
