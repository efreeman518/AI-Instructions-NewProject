# Testing Quality Gates

Use this file for Phase 5d quality suites and release hardening.

## When to Load

- Load when adding or running architecture, hosted UI, load, or benchmark gates.
- Load for release hardening strategy.
- Skip for day-to-day unit/endpoint authoring.

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

- minimal: Unit + Endpoint
- balanced: Unit + Endpoint + Integration + Architecture
- comprehensive: balanced + PlaywrightUI + Load + Benchmarks (when scenario enabled)

If a slice spans multiple entities/stores, run at least one integration path that covers the full composite flow.
