# Test Templates — Index

Test scaffolding lives in split files by phase and harness. Load only the matching template(s) for the current task.

| Phase | Template | Generates |
|---|---|---|
| 5a | [test-templates-domain.md](test-templates-domain.md) | `Test/Test.Unit/Domain/**` |
| 5a | [test-templates-repository.md](test-templates-repository.md) | `Test/Test.Unit/Repositories/**` |
| 5b | [test-templates-service.md](test-templates-service.md) | `Test/Test.Unit/Services/**` |
| 5b | [test-templates-endpoint.md](test-templates-endpoint.md) | `Test/Test.Endpoints/**` (incl. shared `WebApplicationFactoryBase` in `Test.Support`) |
| 5d | [test-templates-quality.md](test-templates-quality.md) | `Test/Test.Architecture/**`, `Test/Test.Load/**`, `Test/Test.Benchmarks/**`, `Test/Test.PlaywrightUI/**` (Node, hosted-stack), `Test/Test.E2E/**` |

Primary routing: [../skills/testing-templates-map.md](../skills/testing-templates-map.md)

Core policy and harness model: [../skills/testing-core.md](../skills/testing-core.md)

Hosted browser UI rules: [../skills/testing-playwright-ui.md](../skills/testing-playwright-ui.md)

Integration host fixture rules: [../skills/testing-integration-hosts.md](../skills/testing-integration-hosts.md)
