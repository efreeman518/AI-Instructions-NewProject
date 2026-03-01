namespace Test.Unit.Domain;

[TestClass]
public class CategoryTests
{
    [TestMethod]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var result = Category.Create(tenantId, "Work", "Work items", "#FF0000", 1);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Work", result.Value.Name);
    }

    [TestMethod]
    public void Create_WithEmptyName_ReturnsFailure()
    {
        var result = Category.Create(Guid.NewGuid(), "", "Description", "#FF0000", 1);

        Assert.IsTrue(result.IsFailure);
    }
}
