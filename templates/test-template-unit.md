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
    public async Task CreateAsync_WithValidDto_ReturnsSuccessResult()
    {
        // Arrange
        var dto = new {Entity}Dto
        {
            Name = "Test {Entity}",
            TenantId = _testTenantId,
            Description = "A test entity"
        };
        var request = new DefaultRequest<{Entity}Dto> { Item = dto };

        var createdEntity = {Entity}.Create(dto.TenantId, dto.Name).Value!;
        _repoTrxnMock.Setup(r => r.Create(ref It.Ref<{Entity}>.IsAny));
        _repoTrxnMock.Setup(r => r.UpdateFromDto(It.IsAny<{Entity}>(), It.IsAny<{Entity}Dto>()))
            .Returns(DomainResult<{Entity}>.Success(createdEntity));
        _repoTrxnMock.Setup(r => r.SaveChangesAsync(It.IsAny<OptimisticConcurrencyWinner>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tenantBoundaryMock.Setup(t => t.EnsureTenantBoundary(
                It.IsAny<ILogger>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Result.Success());

        var service = CreateService();

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value?.Item);
        Assert.AreEqual(dto.Name, result.Value.Item.Name);
    }

    [TestMethod]
    public async Task Update_NotFound_ReturnsNone()
    {
        // Arrange
        _repoTrxnMock.Setup(r => r.Get{Entity}Async(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(({Entity}?)null);
        var service = CreateService();
        var request = new DefaultRequest<{Entity}Dto> { Item = new {Entity}Dto { Id = Guid.NewGuid(), Name = "Test" } };

        // Act
        var result = await service.UpdateAsync(request);

        // Assert
        Assert.IsTrue(result.IsNone);
    }

    [TestMethod]
    public async Task Delete_ExistingEntity_ReturnsSuccess()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entity = {Entity}.Create(_testTenantId, "ToDelete").Value!;
        _repoTrxnMock.Setup(r => r.Get{Entity}Async(entityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _repoTrxnMock.Setup(r => r.SaveChangesAsync(It.IsAny<OptimisticConcurrencyWinner>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tenantBoundaryMock.Setup(t => t.EnsureTenantBoundary(
                It.IsAny<ILogger>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Result.Success());
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(entityId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        _repoTrxnMock.Verify(r => r.Delete(entity), Times.Once);
    }
}
```

## Domain Entity Tests

### File: `Test/Test.Unit/Domain/{Entity}Tests.cs`

```csharp
[TestClass]
public class {Entity}Tests
{
    [TestMethod]
    public void Create_ValidInput_ReturnsSuccess()
    {
        // Arrange & Act
        var result = {Entity}.Create(Guid.NewGuid(), "Valid Name");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Valid Name", result.Value.Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Create_WithEmptyName_ReturnsDomainFailure(string? name)
    {
        // Arrange & Act
        var result = {Entity}.Create(Guid.NewGuid(), name!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.IsTrue(result.ErrorMessage!.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Update_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var entity = {Entity}.Create(Guid.NewGuid(), "Original").Value!;

        // Act
        var result = entity.Update(name: "Updated");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Updated", result.Value!.Name);
    }

    [TestMethod]
    public void AddChild_Duplicate_ReturnsExisting()
    {
        // Arrange
        var entity = {Entity}.Create(Guid.NewGuid(), "Parent").Value!;
        var child = {ChildEntity}.Create(Guid.NewGuid(), "Child").Value!;
        entity.Add{ChildEntity}(child);

        // Act — add same child again
        var result = entity.Add{ChildEntity}(child);

        // Assert — idempotent, returns existing
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, entity.{ChildEntity}s.Count);
    }
}
```

## Domain Rule Tests

### File: `Test/Test.Unit/Domain/{Entity}RulesTests.cs`

```csharp
[TestClass]
public class {Entity}RulesTests
{
    [TestMethod]
    public void TitleRequired_WhenTitleEmpty_IsNotSatisfied()
    {
        // Arrange
        var rule = new {Entity}NameRequiredRule();
        var entity = {Entity}.Create(Guid.NewGuid(), "").Value!;

        // Act
        var satisfied = rule.IsSatisfiedBy(entity);

        // Assert
        Assert.IsFalse(satisfied);
        Assert.IsFalse(string.IsNullOrEmpty(rule.ErrorMessage));
    }

    [TestMethod]
    public void CompositeRule_AllRules_WhenOneFails_ReturnsFalse()
    {
        // Arrange
        var rules = new IRule<{Entity}>[]
        {
            new {Entity}NameRequiredRule(),
            new {Entity}CannotDeactivateWithActiveChildrenRule()
        };
        var entity = {Entity}.Create(Guid.NewGuid(), "").Value!; // empty name

        // Act
        var result = rules.EvaluateAll(entity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.IsTrue(result.ErrorMessage!.Contains("name", StringComparison.OrdinalIgnoreCase));
    }
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
public async Task SearchAsync_WithFilter_ReturnsMatchingEntities()
{
    // Arrange
    var db = new InMemoryDbBuilder()
        .UseEntityData(ctx =>
        {
            var tenantId = Guid.NewGuid();
            ctx.Set<{Entity}>().Add({Entity}.Create(tenantId, "Alpha").Value!);
            ctx.Set<{Entity}>().Add({Entity}.Create(tenantId, "Beta").Value!);
            ctx.Set<{Entity}>().Add({Entity}.Create(tenantId, "AlphaTwo").Value!);
            ctx.SaveChanges();
        })
        .BuildInMemory<{App}DbContextQuery>();
    var repo = new {Entity}RepositoryQuery(db);

    // Act
    var page = await repo.Search{Entity}Async(
        new SearchRequest<{Entity}SearchFilter>
        {
            Page = 1,
            PageSize = 10,
            Filter = new {Entity}SearchFilter { SearchTerm = "Alpha" }
        });

    // Assert
    Assert.AreEqual(2, page.TotalCount);
    Assert.IsTrue(page.Items.All(i => i.Name.Contains("Alpha")));
}
```

## Mapper Tests

### File: `Test/Test.Unit/Mappers/{Entity}MapperTests.cs`

```csharp
[TestClass]
public class {Entity}MapperTests
{
    [TestMethod]
    public void ToDto_MapsAllProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entity = {Entity}.Create(tenantId, "Test Name").Value!;

        // Act
        var dto = entity.ToDto();

        // Assert
        Assert.AreEqual(entity.Id, dto.Id);
        Assert.AreEqual(entity.Name, dto.Name);
        Assert.AreEqual(entity.TenantId, dto.TenantId);
        Assert.AreEqual(entity.Flags, dto.Flags);
        // Add assertions for each additional mapped property
        // Note: Audit fields (CreatedDate, etc.) are NOT mapped — managed by AuditInterceptor
    }

    [TestMethod]
    public void ToEntity_ReturnsValidDomainResult()
    {
        // Arrange
        var dto = new {Entity}Dto { Name = "From DTO", TenantId = Guid.NewGuid() };

        // Act
        var result = dto.ToEntity(dto.TenantId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(dto.Name, result.Value!.Name);
    }
}
```
