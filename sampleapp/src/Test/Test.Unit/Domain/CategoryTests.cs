// ═══════════════════════════════════════════════════════════════
// Pattern: Domain entity unit tests — Category (simple tenant entity).
// Tests Create/Update factory, validation (Name required, ColorHex regex),
// DisplayOrder, and IsActive toggle.
// Demonstrates: cacheable static data entity testing.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Unit.Domain;

[TestClass]
public class CategoryTests
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // ═══════════════════════════════════════════════════════════════
    // Create — Happy Path
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_ValidInput_ReturnsSuccess()
    {
        var result = Category.Create(
            tenantId: TestTenantId,
            name: "Work",
            description: "Work-related tasks",
            colorHex: "#FF5733",
            displayOrder: 1);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Work", result.Value!.Name);
        Assert.AreEqual("#FF5733", result.Value.ColorHex);
        Assert.AreEqual(1, result.Value.DisplayOrder);
        Assert.IsTrue(result.Value.IsActive);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_NullOptionalFields_ReturnsSuccess()
    {
        // Pattern: Optional fields — description and colorHex can be null.
        var result = Category.Create(TestTenantId, "Minimal", null, null, 0);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value!.Description);
        Assert.IsNull(result.Value.ColorHex);
    }

    // ═══════════════════════════════════════════════════════════════
    // Create — Validation Failures
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(null, DisplayName = "Null name")]
    [DataRow("", DisplayName = "Empty name")]
    [DataRow("   ", DisplayName = "Whitespace name")]
    public void Create_InvalidName_ReturnsFailure(string? name)
    {
        var result = Category.Create(TestTenantId, name!, null, null, 0);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_NameExceeds100Chars_ReturnsFailure()
    {
        // Pattern: Max length validation — Name must not exceed 100 chars.
        var longName = new string('x', 101);
        var result = Category.Create(TestTenantId, longName, null, null, 0);

        Assert.IsTrue(result.IsFailure);
    }

    // ═══════════════════════════════════════════════════════════════
    // ColorHex Validation
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow("#FF5733", true, DisplayName = "Valid uppercase hex")]
    [DataRow("#ff5733", true, DisplayName = "Valid lowercase hex")]
    [DataRow("#Aa1234", true, DisplayName = "Valid mixed case hex")]
    [DataRow("#000000", true, DisplayName = "Valid black")]
    [DataRow("#FFFFFF", true, DisplayName = "Valid white")]
    [DataRow("FF5733", false, DisplayName = "Missing # prefix")]
    [DataRow("#FF573", false, DisplayName = "Too short — 5 hex chars")]
    [DataRow("#FF57331", false, DisplayName = "Too long — 7 hex chars")]
    [DataRow("#GGGGGG", false, DisplayName = "Invalid hex chars")]
    [DataRow("red", false, DisplayName = "Color name — not hex format")]
    public void Create_ColorHexValidation_ReturnsExpected(string colorHex, bool expectedValid)
    {
        // Pattern: Regex validation — ^#[0-9A-Fa-f]{6}$ for hex colors.
        var result = Category.Create(TestTenantId, "Test Category", null, colorHex, 0);

        Assert.AreEqual(expectedValid, result.IsSuccess,
            $"ColorHex '{colorHex}' — expected {(expectedValid ? "valid" : "invalid")}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Update — Happy Path
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_ValidInput_ReturnsSuccess()
    {
        var entity = Category.Create(TestTenantId, "Original", null, null, 1).Value!;

        var result = entity.Update(
            name: "Updated Category",
            description: "New description",
            colorHex: "#00FF00",
            displayOrder: 5,
            isActive: true);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Updated Category", entity.Name);
        Assert.AreEqual("#00FF00", entity.ColorHex);
        Assert.AreEqual(5, entity.DisplayOrder);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_Deactivate_SetsIsActiveFalse()
    {
        // Pattern: Soft delete — IsActive toggle hides category from UI pickers.
        var entity = Category.Create(TestTenantId, "Active Category", null, null, 1).Value!;
        Assert.IsTrue(entity.IsActive);

        var result = entity.Update("Active Category", null, null, 1, isActive: false);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(entity.IsActive);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_InvalidColorHex_ReturnsFailure()
    {
        var entity = Category.Create(TestTenantId, "Test", null, null, 0).Value!;

        var result = entity.Update("Test", null, "not-a-color", 0, true);

        Assert.IsTrue(result.IsFailure);
    }
}
