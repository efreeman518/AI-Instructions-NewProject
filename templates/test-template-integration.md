# Test Template — Integration

See [skills/testing.md](../skills/testing.md) for testing strategy and profile selection.

## Common Setup

Keep API factory/bootstrap and DB reset helpers centralized; integration classes should only contain scenario setup and assertions.

## Endpoint Tests

### File: `Test/Test.Integration/CustomApiFactory.cs`

```csharp
public class CustomApiFactory<TProgram>(string? dbConnectionString = null)
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development").ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            DbSupport.ConfigureServicesTestDB<{App}DbContextTrxn, {App}DbContextQuery>(
                services,
                dbConnectionString,
                "Test.Integration.TestDB");
        });
    }
}
```

### File: `Test/Test.Integration/EndpointTestBase.cs`

```csharp
public abstract class EndpointTestBase : DbIntegrationTestBase
{
    protected static async Task<HttpClient> GetHttpClient(params DelegatingHandler[] handlers) { /* shared client factory */ }
}
```

### File: `Test/Test.Integration/Endpoints/{Entity}EndpointsTests.cs`

```csharp
[TestClass]
public class {Entity}EndpointsTests : EndpointTestBase
{
    [TestMethod]
    public async Task CRUD_Pass() { /* POST/GET/PUT/DELETE + 404 */ }

    [TestMethod]
    public async Task GetPage_ReturnsOk() { }
}
```

### File: `Test/Test.Integration/appsettings-test.json`

```json
{
  "TestSettings": {
    "DBSource": "UseInMemoryDatabase",
    "DBName": "Test.Integration.TestDB"
  }
}
```

## Test.Support Infrastructure

### File: `Test/Test.Support/UnitTestBase.cs`

```csharp
public abstract class UnitTestBase
{
    protected readonly MockRepository _mockFactory =
        new(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
}
```

### File: `Test/Test.Support/InMemoryDbBuilder.cs`

```csharp
public class InMemoryDbBuilder
{
    public InMemoryDbBuilder SeedDefaultEntityData() { /* flag */ return this; }
    public InMemoryDbBuilder UseEntityData(Action<DbContext> seedAction) { /* add seed */ return this; }
    public T BuildInMemory<T>(string? dbName = null) where T : DbContext { /* create + seed */ }
    public T BuildSQLite<T>() where T : DbContext { /* sqlite in-memory + EnsureCreated */ }
}
```

### File: `Test/Test.Support/DbSupport.cs`

```csharp
public static class DbSupport
{
    public static void ConfigureServicesTestDB<TTrxn, TQuery>(
        IServiceCollection services,
        string? dbConnectionString,
        string dbName = "TestDB")
        where TTrxn : DbContext where TQuery : DbContext
    {
        // in-memory or sqlserver wiring + no-tracking query context
    }
}
```

### File: `Test/Test.Support/Utility.cs`

```csharp
public static class Utility
{
    public static IConfigurationBuilder BuildConfiguration(string? path = "appsettings.json", bool includeEnvironmentVars = true) { }
    public static string RandomString(int length) { }
}
```
