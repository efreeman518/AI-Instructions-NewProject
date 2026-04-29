# Test Templates — Index

Test scaffolding lives in five split files, one per harness tier. Load only the file matching the current sub-phase.

| Phase | Template | Generates |
|---|---|---|
| 5a | [test-templates-domain.md](test-templates-domain.md) | `Test/Test.Unit/Domain/**` |
| 5a | [test-templates-repository.md](test-templates-repository.md) | `Test/Test.Unit/Repositories/**` |
| 5b | [test-templates-service.md](test-templates-service.md) | `Test/Test.Unit/Services/**` |
| 5b | [test-templates-endpoint.md](test-templates-endpoint.md) | `Test/Test.Endpoints/**` (incl. shared `WebApplicationFactoryBase` in `Test.Support`) |
| 5d | [test-templates-quality.md](test-templates-quality.md) | `Test/Test.Architecture/**`, `Test/Test.Load/**`, `Test/Test.Benchmarks/**`, `Test/Test.PlaywrightUI/**` (Node, hosted-stack), `Test/Test.E2E/**` |

See [skills/testing.md](../skills/testing.md) for the harness model (Test.Unit / Test.Integration / Test.Endpoints / Test.E2E / Test.PlaywrightUI), TDD protocol, BDD naming, and profile selection.
