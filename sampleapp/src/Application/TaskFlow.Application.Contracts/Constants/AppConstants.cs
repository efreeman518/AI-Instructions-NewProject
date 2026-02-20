// ═══════════════════════════════════════════════════════════════
// Pattern: Application constants — centralized values used across the application layer.
// Keeps Domain.Shared.Constants focused on domain concerns;
// these are application/infrastructure-level constants.
// ═══════════════════════════════════════════════════════════════

namespace Application.Contracts.Constants;

/// <summary>
/// Pattern: Application-level constants — page sizes, header names, cache names, formats.
/// Distinct from <c>Domain.Shared.Constants</c> which holds domain-specific values.
/// </summary>
public static class AppConstants
{
    // ── Roles (mirrored from Domain.Shared for convenience in services) ──
    public const string ROLE_GLOBAL_ADMIN = "GlobalAdmin";
    public const string ROLE_TENANT_ADMIN = "TenantAdmin";
    public const string ROLE_TENANT_USER = "TenantUser";

    // ── Pagination defaults ──
    public const int DEFAULT_PAGE_SIZE = 25;
    public const int MAX_PAGE_SIZE = 100;

    // ── Caching ──
    public const int CACHE_PROVIDER_DEFAULT_DURATION_SECONDS = 60 * 60; // 1 hour
    public const string DEFAULT_CACHE = "TaskFlow.DefaultCache";
    public const string STATIC_DATA_CACHE = "TaskFlow.StaticData";

    // ── HTTP headers ──
    public const string CORRELATION_ID_HEADER = "X-Correlation-ID";
    public const string ORIG_REQUEST_HEADER = "X-Orig-Request";

    // ── Date/time formatting ──
    public const string DEFAULT_TIMEZONE = "America/New_York";
    public const string DEFAULT_DATETIME_FORMAT = "yyyy-MM-ddTHH:mm";

    // ── Content types ──
    public const string JSON_CONTENT_TYPE = "application/json";
}
