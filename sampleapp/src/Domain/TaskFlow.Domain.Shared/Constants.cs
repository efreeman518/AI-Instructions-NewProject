// Pattern: Domain.Shared constants — role names, cache names, and other
// cross-cutting constants used by multiple layers without domain entity dependencies.

namespace Domain.Shared.Constants;

/// <summary>
/// Role constants used for authorization checks across the application.
/// These match the app roles configured in Entra ID / identity provider.
/// </summary>
public static class Roles
{
    /// <summary>Full access across all tenants — bypasses tenant boundary checks.</summary>
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>Tenant administrator — manages users, teams, categories within their tenant.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Standard user — CRUD on own items, read access to team items.</summary>
    public const string User = "User";

    /// <summary>Read-only access — can view but not modify.</summary>
    public const string ReadOnly = "ReadOnly";
}

/// <summary>
/// Named cache instance identifiers.
/// Each name maps to a FusionCache instance with specific TTL/behavior settings.
/// Configured in appsettings.json CacheSettings array.
/// </summary>
public static class CacheNames
{
    /// <summary>Short-TTL cache for frequently-changing entity data (e.g., TodoItems).</summary>
    public const string Default = "Default";

    /// <summary>Long-TTL cache for slowly-changing reference data (e.g., Categories, Tags).</summary>
    public const string StaticData = "StaticData";
}

/// <summary>
/// Internal message bus event names — used as type discriminators for IMessageHandler routing.
/// </summary>
public static class EventNames
{
    public const string TodoItemCreated = nameof(TodoItemCreated);
    public const string TodoItemUpdated = nameof(TodoItemUpdated);
    public const string TodoItemAssigned = nameof(TodoItemAssigned);
    public const string TodoItemCompleted = nameof(TodoItemCompleted);
}
