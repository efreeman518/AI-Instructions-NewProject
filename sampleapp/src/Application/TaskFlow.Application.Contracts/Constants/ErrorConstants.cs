// ═══════════════════════════════════════════════════════════════
// Pattern: Error constants — structured error codes for API responses.
// Used in ProblemDetails "type" and "extensions" for machine-readable errors.
// Consumers can switch on ErrorCode to display localized messages.
// ═══════════════════════════════════════════════════════════════

namespace Application.Contracts.Constants;

/// <summary>
/// Pattern: Centralized error codes — every service failure maps to one of these.
/// Pair with <see cref="Application.Services.Rules.ServiceErrorMessages"/> for human-readable messages.
/// </summary>
public static class ErrorConstants
{
    // ── Not found ──
    public const string ENTITY_NOT_FOUND = "ENTITY_NOT_FOUND";

    // ── Validation ──
    public const string VALIDATION_FAILED = "VALIDATION_FAILED";
    public const string PAYLOAD_REQUIRED = "PAYLOAD_REQUIRED";
    public const string FIELD_REQUIRED = "FIELD_REQUIRED";
    public const string FIELD_TOO_LONG = "FIELD_TOO_LONG";
    public const string INVALID_FORMAT = "INVALID_FORMAT";

    // ── Authorization ──
    public const string FORBIDDEN = "FORBIDDEN";
    public const string TENANT_BOUNDARY_VIOLATION = "TENANT_BOUNDARY_VIOLATION";
    public const string TENANT_CHANGE_NOT_ALLOWED = "TENANT_CHANGE_NOT_ALLOWED";
    public const string INSUFFICIENT_ROLE = "INSUFFICIENT_ROLE";

    // ── Business rules ──
    public const string DUPLICATE_NAME = "DUPLICATE_NAME";
    public const string INVALID_STATUS_TRANSITION = "INVALID_STATUS_TRANSITION";
    public const string HIERARCHY_CYCLE_DETECTED = "HIERARCHY_CYCLE_DETECTED";
    public const string MAX_DEPTH_EXCEEDED = "MAX_DEPTH_EXCEEDED";
    public const string SELF_REFERENCE_NOT_ALLOWED = "SELF_REFERENCE_NOT_ALLOWED";

    // ── Infrastructure ──
    public const string CONCURRENCY_CONFLICT = "CONCURRENCY_CONFLICT";
    public const string EXTERNAL_SERVICE_UNAVAILABLE = "EXTERNAL_SERVICE_UNAVAILABLE";
}
