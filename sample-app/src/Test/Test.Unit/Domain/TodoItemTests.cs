namespace Test.Unit.Domain;

[TestClass]
public class TodoItemTests
{
    [TestMethod]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var result = TodoItem.Create(tenantId, "Test Item", "Description", 1);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Test Item", result.Value.Title);
        Assert.AreEqual(tenantId, result.Value.TenantId);
        Assert.AreEqual(TodoItemStatus.None, result.Value.Status);
    }

    [TestMethod]
    public void Create_WithEmptyTitle_ReturnsFailure()
    {
        var result = TodoItem.Create(Guid.NewGuid(), "", "Description", 1);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    public void Start_FromNone_ReturnsSuccess()
    {
        var item = TodoItem.Create(Guid.NewGuid(), "Test", "Desc", 1).Value!;

        var result = item.Start();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(item.Status.HasFlag(TodoItemStatus.IsStarted));
    }

    [TestMethod]
    public void Complete_FromStarted_ReturnsSuccess()
    {
        var item = TodoItem.Create(Guid.NewGuid(), "Test", "Desc", 1).Value!;
        item.Start();

        var result = item.Complete();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(item.Status.HasFlag(TodoItemStatus.IsCompleted));
    }

    [TestMethod]
    public void Block_SetsBlockedFlag()
    {
        var item = TodoItem.Create(Guid.NewGuid(), "Test", "Desc", 1).Value!;
        item.Start();

        var result = item.Block();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(item.Status.HasFlag(TodoItemStatus.IsBlocked));
    }

    [TestMethod]
    public void Assign_SetsAssignedToId()
    {
        var item = TodoItem.Create(Guid.NewGuid(), "Test", "Desc", 1).Value!;
        var assigneeId = Guid.NewGuid();

        item.Assign(assigneeId);

        Assert.AreEqual(assigneeId, item.AssignedToId);
    }

    [TestMethod]
    public void AddComment_WithValidData_ReturnsSuccess()
    {
        var item = TodoItem.Create(Guid.NewGuid(), "Test", "Desc", 1).Value!;

        var result = item.AddComment("Test comment", Guid.NewGuid());

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, item.Comments.Count);
    }
}
