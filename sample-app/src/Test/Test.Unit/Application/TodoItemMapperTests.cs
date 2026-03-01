namespace Test.Unit.Application;

[TestClass]
public class TodoItemMapperTests
{
    [TestMethod]
    public void ToDto_MapsAllProperties()
    {
        var item = TodoItem.Create(Guid.NewGuid(), "Test", "Description", 3).Value!;

        var dto = item.ToDto();

        Assert.AreEqual(item.Id, dto.Id);
        Assert.AreEqual("Test", dto.Title);
        Assert.AreEqual("Description", dto.Description);
        Assert.AreEqual(3, dto.Priority);
        Assert.AreEqual(TodoItemStatus.None, dto.Status);
    }
}
