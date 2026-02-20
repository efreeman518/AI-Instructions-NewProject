// ═══════════════════════════════════════════════════════════════
// Pattern: High-performance logging — source-generated LoggerMessage.
// Compile-time-generated log methods avoid boxing and string allocation.
// Used by ValidationHelper and TenantBoundaryValidator for audit trails.
// ═══════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;

namespace Application.Services.Rules;

/// <summary>
/// Pattern: Source-generated logging extensions — partial class with [LoggerMessage] attributes.
/// Each method is generated at compile time for zero-allocation structured logging.
/// </summary>
internal static partial class TenantBoundaryLoggingExtensions
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Validation failure in {Context}. Messages={Messages}")]
    public static partial void LogValidationFailure(this ILogger logger, string context, string messages);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Potential tenant filter manipulation in {Context}. CallerTenant={CallerTenantId} SuppliedTenant={SuppliedTenantId}")]
    public static partial void LogTenantFilterManipulation(this ILogger logger,
        string context, Guid? callerTenantId, Guid? suppliedTenantId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Attempted tenant change on {Entity} {EntityId}. ExistingTenant={ExistingTenantId} IncomingTenant={IncomingTenantId}")]
    public static partial void LogTenantChangeAttempt(this ILogger logger,
        string entity, Guid? entityId, Guid? existingTenantId, Guid? incomingTenantId);
}
