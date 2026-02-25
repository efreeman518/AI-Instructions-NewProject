# Test Template — Unit

See [skills/testing.md](../skills/testing.md) for testing strategy and profile selection.

## Common Setup

Use `UnitTestBase` for Moq defaults and keep a single helper method for service creation to avoid repeated mock/bootstrap code across test classes.

```csharp
private {Entity}Service CreateService(
    I{Entity}RepositoryTrxn? trxn = null,
    I{Entity}RepositoryQuery? query = null)
{
    return new {Entity}Service(
        new NullLogger<{Entity}Service>(),
        _requestContextMock.Object,
        trxn ?? _repoTrxnMock.Object,
        query ?? _repoQueryMock.Object,
        _entityCacheMock.Object,
        _fusionCacheProviderMock.Object,
        _tenantBoundaryMock.Object);
}
```

## Service Tests

### File: `Test/Test.Unit/Services/{Entity}ServiceTests.cs`

```csharp
[TestClass]
public class {Entity}ServiceTests : UnitTestBase
{
    [TestMethod]
    public async Task Create_ValidInput_ReturnsSuccess() { /* arrange/act/assert */ }

    [TestMethod]
    public async Task Update_NotFound_ReturnsNone() { /* arrange/act/assert */ }

    [TestMethod]
    public async Task CRUD_InMemory_Pass() { /* create/get/update/delete path */ }

    [TestMethod]
    public async Task Search_ReturnsResults() { /* page assertions */ }
}
```

## Domain Entity Tests

### File: `Test/Test.Unit/Domain/{Entity}Tests.cs`

```csharp
[TestClass]
public class {Entity}Tests
{
    [TestMethod]
    public void Create_ValidInput_ReturnsSuccess() { }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Create_InvalidName_ReturnsFailure(string? name) { }

    [TestMethod]
    public void Update_ValidInput_ReturnsSuccess() { }

    [TestMethod]
    public void AddChild_Duplicate_ReturnsExisting() { }
}
```

## Domain Rule Tests

### File: `Test/Test.Unit/Domain/{Entity}RulesTests.cs`

```csharp
[TestClass]
public class {Entity}RulesTests
{
    [TestMethod]
    public void NameLengthRule_ReturnsExpected() { }

    [TestMethod]
    public void CompositeRule_ReturnsExpected() { }
}
```

## Repository Tests

### Files:
- `Test/Test.Unit/Repositories/{Entity}RepositoryTrxnTests.cs`
- `Test/Test.Unit/Repositories/{Entity}RepositoryQueryTests.cs`

```csharp
[TestMethod]
public async Task CRUD_Pass()
{
    var db = new InMemoryDbBuilder().BuildInMemory<{App}DbContextTrxn>();
    var repo = new {Entity}RepositoryTrxn(db);
    // create → update via domain method → delete
}

[TestMethod]
public async Task Search_InMemory_ReturnsPage()
{
    var db = new InMemoryDbBuilder().SeedDefaultEntityData().BuildInMemory<{App}DbContextQuery>();
    var repo = new {Entity}RepositoryQuery(db);
    var page = await repo.Search{Entity}Async(new SearchRequest<{Entity}SearchFilter> { Page = 1, PageSize = 10 });
    Assert.IsTrue(page.TotalCount > 0);
}
```

## Mapper Tests

### File: `Test/Test.Unit/Mappers/{Entity}MapperTests.cs`

```csharp
[TestClass]
public class {Entity}MapperTests
{
    [TestMethod]
    public void ToDto_MapsAllProperties() { }

    [TestMethod]
    public void ToEntity_ReturnsValidDomainResult() { }
}
```
