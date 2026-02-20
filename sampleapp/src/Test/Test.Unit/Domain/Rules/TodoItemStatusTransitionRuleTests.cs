// ═══════════════════════════════════════════════════════════════
// Pattern: Domain rule unit tests — TodoItemStatusTransitionRule.
// Tests the [Flags] enum state machine with valid/invalid transitions.
// Uses IsSatisfiedBy() directly with tuple input — pure domain logic, no mocks.
// Demonstrates: Specification pattern testing, state machine validation.
// ═══════════════════════════════════════════════════════════════

using Domain.Model.Enums;
using Domain.Model.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Unit.Domain.Rules;

[TestClass]
public class TodoItemStatusTransitionRuleTests
{
    // ═══════════════════════════════════════════════════════════════
    // Pattern: Same status → always allowed (no-op transition).
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(TodoItemStatus.None, DisplayName = "None → None")]
    [DataRow(TodoItemStatus.IsStarted, DisplayName = "Started → Started")]
    [DataRow(TodoItemStatus.IsCompleted, DisplayName = "Completed → Completed")]
    [DataRow(TodoItemStatus.IsBlocked, DisplayName = "Blocked → Blocked")]
    [DataRow(TodoItemStatus.IsArchived, DisplayName = "Archived → Archived")]
    [DataRow(TodoItemStatus.IsCancelled, DisplayName = "Cancelled → Cancelled")]
    public void SameStatus_AlwaysAllowed(TodoItemStatus status)
    {
        var rule = new TodoItemStatusTransitionRule();
        var result = rule.IsSatisfiedBy((status, status));

        Assert.IsTrue(result, $"Same-status transition {status} → {status} should always be allowed.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Valid transitions — each [DataRow] maps an allowed path.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(TodoItemStatus.None, TodoItemStatus.IsStarted, DisplayName = "None → Started")]
    [DataRow(TodoItemStatus.None, TodoItemStatus.IsBlocked, DisplayName = "None → Blocked")]
    [DataRow(TodoItemStatus.None, TodoItemStatus.IsCancelled, DisplayName = "None → Cancelled")]
    [DataRow(TodoItemStatus.None, TodoItemStatus.IsArchived, DisplayName = "None → Archived")]
    [DataRow(TodoItemStatus.IsStarted, TodoItemStatus.IsCompleted, DisplayName = "Started → Completed")]
    [DataRow(TodoItemStatus.IsStarted, TodoItemStatus.IsBlocked, DisplayName = "Started → Blocked")]
    [DataRow(TodoItemStatus.IsStarted, TodoItemStatus.IsCancelled, DisplayName = "Started → Cancelled")]
    [DataRow(TodoItemStatus.IsStarted, TodoItemStatus.None, DisplayName = "Started → None (revert)")]
    [DataRow(TodoItemStatus.IsBlocked, TodoItemStatus.IsStarted, DisplayName = "Blocked → Started (unblock)")]
    [DataRow(TodoItemStatus.IsBlocked, TodoItemStatus.None, DisplayName = "Blocked → None")]
    [DataRow(TodoItemStatus.IsBlocked, TodoItemStatus.IsCancelled, DisplayName = "Blocked → Cancelled")]
    [DataRow(TodoItemStatus.IsCompleted, TodoItemStatus.IsStarted, DisplayName = "Completed → Started (reopen)")]
    [DataRow(TodoItemStatus.IsCompleted, TodoItemStatus.IsArchived, DisplayName = "Completed → Archived")]
    [DataRow(TodoItemStatus.IsArchived, TodoItemStatus.None, DisplayName = "Archived → None (unarchive)")]
    [DataRow(TodoItemStatus.IsArchived, TodoItemStatus.IsCompleted, DisplayName = "Archived → Completed")]
    [DataRow(TodoItemStatus.IsCancelled, TodoItemStatus.None, DisplayName = "Cancelled → None (reopen)")]
    public void ValidTransition_ReturnsTrue(TodoItemStatus current, TodoItemStatus proposed)
    {
        var rule = new TodoItemStatusTransitionRule();
        var result = rule.IsSatisfiedBy((current, proposed));

        Assert.IsTrue(result,
            $"Transition {current} → {proposed} should be allowed.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Invalid transitions — disallowed by the state machine.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    [DataRow(TodoItemStatus.None, TodoItemStatus.IsCompleted, DisplayName = "None → Completed (must start first)")]
    [DataRow(TodoItemStatus.IsCompleted, TodoItemStatus.IsBlocked, DisplayName = "Completed → Blocked")]
    [DataRow(TodoItemStatus.IsCompleted, TodoItemStatus.IsCancelled, DisplayName = "Completed → Cancelled")]
    [DataRow(TodoItemStatus.IsCompleted, TodoItemStatus.None, DisplayName = "Completed → None")]
    [DataRow(TodoItemStatus.IsCancelled, TodoItemStatus.IsStarted, DisplayName = "Cancelled → Started")]
    [DataRow(TodoItemStatus.IsCancelled, TodoItemStatus.IsCompleted, DisplayName = "Cancelled → Completed")]
    [DataRow(TodoItemStatus.IsCancelled, TodoItemStatus.IsBlocked, DisplayName = "Cancelled → Blocked")]
    [DataRow(TodoItemStatus.IsCancelled, TodoItemStatus.IsArchived, DisplayName = "Cancelled → Archived")]
    [DataRow(TodoItemStatus.IsArchived, TodoItemStatus.IsStarted, DisplayName = "Archived → Started")]
    [DataRow(TodoItemStatus.IsArchived, TodoItemStatus.IsBlocked, DisplayName = "Archived → Blocked")]
    [DataRow(TodoItemStatus.IsArchived, TodoItemStatus.IsCancelled, DisplayName = "Archived → Cancelled")]
    [DataRow(TodoItemStatus.IsBlocked, TodoItemStatus.IsCompleted, DisplayName = "Blocked → Completed")]
    [DataRow(TodoItemStatus.IsBlocked, TodoItemStatus.IsArchived, DisplayName = "Blocked → Archived")]
    public void InvalidTransition_ReturnsFalse(TodoItemStatus current, TodoItemStatus proposed)
    {
        var rule = new TodoItemStatusTransitionRule();
        var result = rule.IsSatisfiedBy((current, proposed));

        Assert.IsFalse(result,
            $"Transition {current} → {proposed} should NOT be allowed.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Evaluate() — returns DomainResult for pipeline use.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void Evaluate_ValidTransition_ReturnsSuccessResult()
    {
        // Pattern: RuleBase.Evaluate() wraps IsSatisfiedBy in DomainResult.
        var rule = new TodoItemStatusTransitionRule();
        var domainResult = rule.Evaluate((TodoItemStatus.None, TodoItemStatus.IsStarted));

        Assert.IsTrue(domainResult.IsSuccess);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Evaluate_InvalidTransition_ReturnsFailureWithMessage()
    {
        var rule = new TodoItemStatusTransitionRule();
        var domainResult = rule.Evaluate((TodoItemStatus.None, TodoItemStatus.IsCompleted));

        Assert.IsTrue(domainResult.IsFailure);
        // Pattern: ErrorMessage contains the transition details for debugging.
        Assert.IsTrue(rule.ErrorMessage.Contains("None"),
            "Error message should reference the current status.");
        Assert.IsTrue(rule.ErrorMessage.Contains("IsCompleted"),
            "Error message should reference the proposed status.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Pattern: Composite rule evaluation — RuleEvaluator helpers.
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [TestCategory("Unit")]
    public void RuleEvaluator_EvaluateAll_CollectsAllErrors()
    {
        // Pattern: EvaluateAll collects ALL failed rules (doesn't short-circuit).
        var rule1 = new TodoItemStatusTransitionRule();
        var rule2 = new TodoItemStatusTransitionRule();

        var context = (TodoItemStatus.None, TodoItemStatus.IsCompleted);
        var result = RuleEvaluator.EvaluateAll(context, rule1, rule2);

        Assert.IsTrue(result.IsFailure);
        // Both rules fail on the same invalid transition.
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void RuleEvaluator_EvaluateChain_StopsAtFirstFailure()
    {
        // Pattern: EvaluateChain stops at the first failure (short-circuit).
        var rule = new TodoItemStatusTransitionRule();

        var invalidContext = (TodoItemStatus.None, TodoItemStatus.IsCompleted);
        var result = RuleEvaluator.EvaluateChain(invalidContext, rule);

        Assert.IsTrue(result.IsFailure);
    }
}
