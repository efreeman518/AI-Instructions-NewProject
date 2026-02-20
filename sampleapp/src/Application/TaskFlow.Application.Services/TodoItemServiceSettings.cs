// ═══════════════════════════════════════════════════════════════
// Pattern: Per-service settings — configuration bound from appsettings.
// Each service can have its own settings class with service-specific params.
// Bound via services.Configure<T>(config.GetSection(T.ConfigSectionName)).
// Injected as IOptions<T> in the service constructor.
// ═══════════════════════════════════════════════════════════════

namespace Application.Services;

/// <summary>
/// Pattern: Service settings — bound from "TodoItemServiceSettings" config section.
/// Controls service-layer behavior (hierarchy depth, defaults, feature flags).
/// <code>
/// // appsettings.json:
/// "TodoItemServiceSettings": {
///   "MaxHierarchyDepth": 5,
///   "DefaultPageSize": 25,
///   "EnableAutoAssignment": false
/// }
/// </code>
/// </summary>
public class TodoItemServiceSettings
{
    public const string ConfigSectionName = "TodoItemServiceSettings";

    /// <summary>Maximum nesting depth for parent-child TodoItem hierarchies.</summary>
    public int MaxHierarchyDepth { get; set; } = 5;

    /// <summary>Default page size when no explicit PageSize is provided in the search filter.</summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>Maximum allowed page size — clamped in SearchAsync.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Feature flag: auto-assign new TodoItems to the creating user.</summary>
    public bool EnableAutoAssignment { get; set; }
}
