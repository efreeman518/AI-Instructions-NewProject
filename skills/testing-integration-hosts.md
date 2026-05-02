# Testing Integration Hosts

Use this file for service-level integration tests and Aspire-hosted test fixture rules.

## When to Load

- Load when authoring or fixing `Test.Integration`.
- Load when using `DistributedApplicationTestingBuilder`.
- Skip for unit/endpoint-only work.

## Service-Level Integration vs Endpoint Tests

`Test.Integration` is not for endpoint contract tests.

- Integration: service/repository scenarios against real external services (SQL/Redis/broker emulator)
- Endpoint: HTTP contract tests via `WebApplicationFactory` in `Test.Endpoints`

If the test posts JSON to an API and asserts HTTP response shape, it belongs in `Test.Endpoints`.

## Aspire SQL Fixture Rule

When AppHost already runs persistent SQL:

- Do not add a second `MsSqlBuilder` container to `DatabaseFixture`.
- Start AppHost in fixture and read connection via `GetConnectionStringAsync("sql")`.
- Set SQL password parameter before host startup.

```csharp
Environment.SetEnvironmentVariable("Parameters__sql-password", LocalSqlSettings.SharedSaPassword);
var connectionString = await app.GetConnectionStringAsync("sql");
```

## Aspire Shared Environment Rules

### Rule 1: One shared app per assembly

Do not start/stop distributed app per test class. Start once in `[AssemblyInitialize]` and reuse.

### Rule 2: Set TASKFLOW_ASPIRE_TESTING before CreateAsync

Set `TASKFLOW_ASPIRE_TESTING=true` before `DistributedApplicationTestingBuilder.CreateAsync(...)`.

### Rule 3: Conditional Functions inclusion

Detect `func.exe` once in fixture before startup. Set include flag there, not per test class.

### Rule 4: Timeout mandatory

Use `[Timeout]` on every Aspire integration test method.

```csharp
[Timeout(300000)] // Aspire end-to-end
[Timeout(120000)] // SQL container only
```

### Rule 5: local.settings.json override trap

Hardcoded DB connection strings in Functions `local.settings.json` override Aspire injection.

Remove DB connection strings for tests (except safe Azurite-style values when needed).

### Rule 6: Aspire.Hosting.Testing using directive

Keep `using Aspire.Hosting.Testing;` in files calling `CreateHttpClient()` or `GetConnectionStringAsync()` extension methods.

## Integration Verification Checklist

- [ ] Integration tests call service/repository layer directly.
- [ ] No endpoint-contract tests in `Test.Integration`.
- [ ] Shared Aspire app starts once per assembly.
- [ ] Required env vars are set before app creation.
- [ ] Timeout attributes are present on methods.
- [ ] No conflicting DB strings in Functions local settings.
