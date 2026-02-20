// ═══════════════════════════════════════════════════════════════
// Pattern: Centralized error messages — all service-layer error strings.
// Static methods with interpolation for entity-specific messages.
// Prevents duplicate, inconsistent error messages across services.
// ═══════════════════════════════════════════════════════════════

namespace Application.Services.Rules;

/// <summary>
/// Pattern: Static error message factory — parameterized methods return
/// consistent, descriptive error messages for service-layer validation.
/// Centralizes all user-facing error text in one location.
/// </summary>
public static class ServiceErrorMessages
{
    // ── Payload / null checks ──
    public static string PayloadRequired(string entityName) =>
        $"{entityName} payload is required.";

    public static string NameRequired(string entityName) =>
        $"Name is required for {entityName}.";

    public static string ItemNotFound(string entityName, Guid id) =>
        $"{entityName} not found: {id}";

    public static string ItemNotFoundByName(string entityName, string name) =>
        $"{entityName} not found: {name}";

    // ── Tenant boundary ──
    public static string TenantMismatch(string label) =>
        $"Cannot add child because it belongs to a different tenant: {label}.";

    public static string TenantChangeNotAllowed(string entityName) =>
        $"TenantId cannot be changed for an existing {entityName}.";

    // ── Hierarchy / structure ──
    public static string CycleDetected(string label) =>
        $"Cannot add child because it would create a cycle: {label}.";

    public static string SelfReferenceNotAllowed(string entityName) =>
        $"A {entityName} cannot be a child of itself.";

    public static string MaxDepthExceeded(string entityName, int maxDepth) =>
        $"{entityName} hierarchy depth cannot exceed {maxDepth} levels.";

    // ── Lookup / reference ──
    public static string TenantNotFound(Guid tenantId) =>
        $"Tenant with ID {tenantId} not found.";

    public static string DuplicateName(string entityName, string name) =>
        $"A {entityName} with the name '{name}' already exists in this tenant.";

    public static string InvalidStatusTransition(string from, string to) =>
        $"Cannot transition from '{from}' to '{to}'.";

    // ── Field-level ──
    public static string FieldRequired(string fieldName) =>
        $"{fieldName} is required.";

    public static string FieldTooLong(string fieldName, int maxLength) =>
        $"{fieldName} must not exceed {maxLength} characters.";

    public static string FieldInvalidFormat(string fieldName, string expectedFormat) =>
        $"{fieldName} is not in the expected format: {expectedFormat}.";
}
