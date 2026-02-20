// ═══════════════════════════════════════════════════════════════
// Pattern: Domain entity unit tests — TodoItem (richest entity).
// Tests the Create/Update factory pattern, DomainResult<T> success/failure,
// child collection management (AddComment/RemoveComment),
// computed properties (IsOverdue, IsRootItem), and validation rules.
// Uses [DataRow] for parameterized tests — no mocks needed for pure domain logic.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Entities;
using Domain.Model.Enums;
using Domain.Model.ValueObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Unit.Domain;

[TestClass]
public class TodoItemTests
{
    // Pattern: Shared test data — valid tenant ID and schedule for reuse.
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateRange ValidSchedule = DateRange.Create(
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7)).Value!;

    // ═══════════════════════════════════════════════════════════════
    // Create — Happy Path
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_ValidInput_ReturnsSuccess()
    {
        // Pattern: Full Create() call with all parameters — demonstrates the factory.
        var result = TodoItem.Create(
            tenantId: TestTenantId,
            title: "Write unit tests",
            description: "Cover all domain entity patterns",
            priority: 3,
            estimatedHours: 4.5m,
            categoryId: null,
            assignedToId: null,
            parentId: null,
            schedule: ValidSchedule);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Write unit tests", result.Value!.Title);
        Assert.AreEqual(3, result.Value.Priority);
        Assert.AreEqual(TodoItemStatus.None, result.Value.Status);
        Assert.AreEqual(TestTenantId, result.Value.TenantId);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_MinimalInput_ReturnsSuccess()
    {
        // Pattern: Minimal valid input — only required fields.
        var result = TodoItem.Create(
            tenantId: TestTenantId,
            title: "Minimal item",
            description: null,
            priority: 1,
            estimatedHours: null,
            categoryId: null,
            assignedToId: null,
            parentId: null,
            schedule: ValidSchedule);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value!.Description);
        Assert.IsNull(result.Value.EstimatedHours);
    }

    // ═══════════════════════════════════════════════════════════════
    // Create — Validation Failures
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(null, DisplayName = "Null title")]
    [DataRow("", DisplayName = "Empty title")]
    [DataRow("   ", DisplayName = "Whitespace title")]
    public void Create_InvalidTitle_ReturnsFailure(string? title)
    {
        // Pattern: DomainResult.Failure — Title is required.
        var result = TodoItem.Create(
            TestTenantId, title!, null, 3, null, null, null, null, ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_TitleExceeds200Chars_ReturnsFailure()
    {
        // Pattern: Max length validation — Title must not exceed 200 characters.
        var longTitle = new string('x', 201);
        var result = TodoItem.Create(
            TestTenantId, longTitle, null, 3, null, null, null, null, ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(0, DisplayName = "Priority 0 — below minimum")]
    [DataRow(6, DisplayName = "Priority 6 — above maximum")]
    [DataRow(-1, DisplayName = "Priority -1 — negative")]
    public void Create_InvalidPriority_ReturnsFailure(int priority)
    {
        // Pattern: Range validation — Priority must be 1–5.
        var result = TodoItem.Create(
            TestTenantId, "Valid Title", null, priority, null, null, null, null, ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_NegativeEstimatedHours_ReturnsFailure()
    {
        var result = TodoItem.Create(
            TestTenantId, "Valid Title", null, 3, -1m, null, null, null, ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }

    // ═══════════════════════════════════════════════════════════════
    // Update — Happy Path + Validation
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_ValidInput_ReturnsSuccess()
    {
        // Pattern: Create then Update — demonstrates entity lifecycle.
        var entity = TodoItem.Create(
            TestTenantId, "Original", null, 3, null, null, null, null, ValidSchedule).Value!;

        var updateResult = entity.Update(
            title: "Updated Title",
            description: "New description",
            status: TodoItemStatus.IsStarted,
            priority: 2,
            estimatedHours: 8m,
            actualHours: 2m,
            categoryId: null,
            assignedToId: null,
            parentId: null,
            schedule: ValidSchedule);

        Assert.IsTrue(updateResult.IsSuccess);
        Assert.AreEqual("Updated Title", entity.Title);
        Assert.AreEqual(TodoItemStatus.IsStarted, entity.Status);
        Assert.AreEqual(2, entity.Priority);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_CompletedAndBlocked_ReturnsFailure()
    {
        // Pattern: Cross-field validation — cannot be both Completed and Blocked.
        var entity = TodoItem.Create(
            TestTenantId, "Test", null, 3, null, null, null, null, ValidSchedule).Value!;

        var result = entity.Update(
            "Test", null,
            TodoItemStatus.IsCompleted | TodoItemStatus.IsBlocked,
            3, null, null, null, null, null, ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_CancelledAndStarted_ReturnsFailure()
    {
        // Pattern: Terminal state conflict — Cancelled + Started is invalid.
        var entity = TodoItem.Create(
            TestTenantId, "Test", null, 3, null, null, null, null, ValidSchedule).Value!;

        var result = entity.Update(
            "Test", null,
            TodoItemStatus.IsCancelled | TodoItemStatus.IsStarted,
            3, null, null, null, null, null, ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }

    // ═══════════════════════════════════════════════════════════════
    // Child Collection — AddComment / RemoveComment
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void AddComment_ValidText_ReturnsSuccess()
    {
        // Pattern: Child entity creation through aggregate root.
        var entity = TodoItem.Create(
            TestTenantId, "Parent Item", null, 3, null, null, null, null, ValidSchedule).Value!;

        var result = entity.AddComment("This is a test comment", "user-123");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, entity.Comments.Count);
        Assert.AreEqual("This is a test comment", result.Value!.Text);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(null, DisplayName = "Null comment")]
    [DataRow("", DisplayName = "Empty comment")]
    [DataRow("   ", DisplayName = "Whitespace comment")]
    public void AddComment_InvalidText_ReturnsFailure(string? text)
    {
        var entity = TodoItem.Create(
            TestTenantId, "Parent Item", null, 3, null, null, null, null, ValidSchedule).Value!;

        var result = entity.AddComment(text!, "user-123");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(0, entity.Comments.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void RemoveComment_ExistingComment_Removes()
    {
        // Pattern: Idempotent removal — remove by ID.
        var entity = TodoItem.Create(
            TestTenantId, "Parent Item", null, 3, null, null, null, null, ValidSchedule).Value!;

        var commentResult = entity.AddComment("Comment to remove", "user-123");
        var commentId = commentResult.Value!.Id;

        entity.RemoveComment(commentId);

        Assert.AreEqual(0, entity.Comments.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void RemoveComment_NonExistentId_NoOp()
    {
        // Pattern: Idempotent — removing non-existent comment does nothing.
        var entity = TodoItem.Create(
            TestTenantId, "Parent Item", null, 3, null, null, null, null, ValidSchedule).Value!;

        entity.AddComment("Keep this", "user-123");
        entity.RemoveComment(Guid.NewGuid()); // Non-existent ID

        Assert.AreEqual(1, entity.Comments.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // Computed Properties — IsOverdue, IsRootItem, IsCompleted
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void IsOverdue_PastDueAndNotCompleted_ReturnsTrue()
    {
        // Pattern: Computed property testing — uses a past-due schedule.
        var pastSchedule = DateRange.Create(
            DateTimeOffset.UtcNow.AddDays(-10),
            DateTimeOffset.UtcNow.AddDays(-1)).Value!;

        var entity = TodoItem.Create(
            TestTenantId, "Overdue Item", null, 3, null, null, null, null, pastSchedule).Value!;

        Assert.IsTrue(entity.IsOverdue);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void IsOverdue_FutureDue_ReturnsFalse()
    {
        var futureSchedule = DateRange.Create(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30)).Value!;

        var entity = TodoItem.Create(
            TestTenantId, "Future Item", null, 3, null, null, null, null, futureSchedule).Value!;

        Assert.IsFalse(entity.IsOverdue);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void IsRootItem_NoParent_ReturnsTrue()
    {
        // Pattern: Self-referencing hierarchy — null ParentId = root item.
        var entity = TodoItem.Create(
            TestTenantId, "Root Item", null, 3, null, null, null, null, ValidSchedule).Value!;

        Assert.IsTrue(entity.IsRootItem);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void IsRootItem_HasParent_ReturnsFalse()
    {
        var parentId = Guid.NewGuid();
        var entity = TodoItem.Create(
            TestTenantId, "Child Item", null, 3, null, null, null, parentId, ValidSchedule).Value!;

        Assert.IsFalse(entity.IsRootItem);
    }

    // ═══════════════════════════════════════════════════════════════
    // Self-Reference Guard
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void Update_SelfAsParent_ReturnsFailure()
    {
        // Pattern: Self-reference guard — an item cannot be its own parent.
        var entity = TodoItem.Create(
            TestTenantId, "Test", null, 3, null, null, null, null, ValidSchedule).Value!;

        var result = entity.Update(
            "Test", null, TodoItemStatus.None, 3, null, null, null, null,
            entity.Id, // Set parentId to own Id
            ValidSchedule);

        Assert.IsTrue(result.IsFailure);
    }
}
