// ═══════════════════════════════════════════════════════════════
// Pattern: Strongly-typed settings POCO for IOptions<T> injection.
// Registered via builder.Services.Configure<Settings>(config.GetSection("Settings")).
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.FunctionApp;

/// <summary>
/// Pattern: Settings POCO — used across function triggers via IOptions&lt;Settings&gt;.
/// Create additional Settings POCOs as needed for different config sections.
/// </summary>
public class Settings
{
    public string? SomeString { get; set; }
    public int? SomeInt { get; set; }
}
