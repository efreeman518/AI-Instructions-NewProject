// Pattern: Specification pattern — IRule<T> interface + RuleBase<T> abstract class.
// Used for complex domain validation that crosses entity boundaries or involves
// business logic too complex for the entity's Valid() method.
// Also includes composite rule helpers: AllRule (AND), AnyRule (OR).

namespace Domain.Model.Rules;

/// <summary>
/// Specification interface — evaluates whether an entity satisfies a business rule.
/// </summary>
/// <typeparam name="T">The entity or input type to evaluate.</typeparam>
public interface IRule<in T>
{
    /// <summary>Human-readable error message when the rule is not satisfied.</summary>
    string ErrorMessage { get; }

    /// <summary>Returns true if the rule is satisfied by the given entity.</summary>
    bool IsSatisfiedBy(T entity);
}

/// <summary>
/// Base class for rules — provides Evaluate() returning DomainResult for use in pipelines.
/// </summary>
public abstract class RuleBase<T> : IRule<T>
{
    public abstract string ErrorMessage { get; }
    public abstract bool IsSatisfiedBy(T entity);

    /// <summary>
    /// Evaluates the rule and returns Success or Failure with error message.
    /// </summary>
    public DomainResult<T> Evaluate(T entity)
    {
        return IsSatisfiedBy(entity)
            ? DomainResult<T>.Success(entity)
            : DomainResult<T>.Failure(ErrorMessage);
    }
}

// ═══════════════════════════════════════════════════════════════
// Pattern: Composite rules — combine multiple rules with AND/OR logic.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// AND composite — all child rules must be satisfied.
/// Collects ALL errors (does not short-circuit).
/// </summary>
public class AllRule<T>(IEnumerable<IRule<T>> rules) : IRule<T>
{
    public string ErrorMessage => "One or more rules failed.";

    public bool IsSatisfiedBy(T entity) => rules.All(r => r.IsSatisfiedBy(entity));

    /// <summary>
    /// Evaluates all rules and returns all error messages (not just the first).
    /// Pattern: EvaluateAll collects every error for comprehensive feedback.
    /// </summary>
    public DomainResult<T> EvaluateAll(T entity)
    {
        var errors = rules
            .Where(r => !r.IsSatisfiedBy(entity))
            .Select(r => r.ErrorMessage)
            .ToList();

        return errors.Count == 0
            ? DomainResult<T>.Success(entity)
            : DomainResult<T>.Failure(errors);
    }
}

/// <summary>
/// OR composite — at least one child rule must be satisfied.
/// </summary>
public class AnyRule<T>(IEnumerable<IRule<T>> rules) : IRule<T>
{
    public string ErrorMessage => "None of the rules were satisfied.";

    public bool IsSatisfiedBy(T entity) => rules.Any(r => r.IsSatisfiedBy(entity));
}

// ═══════════════════════════════════════════════════════════════
// Pattern: Rule evaluation helpers — static methods for pipeline use.
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Static helpers for evaluating rule sequences.
/// </summary>
public static class RuleEvaluator
{
    /// <summary>
    /// Evaluates all rules, collecting ALL errors. Use for comprehensive validation.
    /// </summary>
    public static DomainResult<T> EvaluateAll<T>(T entity, params IRule<T>[] rules)
    {
        var errors = rules
            .Where(r => !r.IsSatisfiedBy(entity))
            .Select(r => r.ErrorMessage)
            .ToList();

        return errors.Count == 0
            ? DomainResult<T>.Success(entity)
            : DomainResult<T>.Failure(errors);
    }

    /// <summary>
    /// Evaluates rules in order, stopping at the first failure.
    /// Use for chain validation where early rules are prerequisites.
    /// </summary>
    public static DomainResult<T> EvaluateChain<T>(T entity, params IRule<T>[] rules)
    {
        foreach (var rule in rules)
        {
            if (!rule.IsSatisfiedBy(entity))
                return DomainResult<T>.Failure(rule.ErrorMessage);
        }
        return DomainResult<T>.Success(entity);
    }
}
