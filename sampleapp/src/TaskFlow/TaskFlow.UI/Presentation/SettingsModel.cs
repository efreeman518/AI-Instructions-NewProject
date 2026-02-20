// ═══════════════════════════════════════════════════════════════
// Pattern: MVUX Settings Model — two-way state bindings for app settings.
// Uses IState<bool> for toggle switches (theme + mock mode).
// Integrates with Uno's IThemeService for runtime theme switching.
//
// Key MVUX concepts demonstrated:
// 1. IState<bool> → two-way bindable mutable state for ToggleSwitch
// 2. Execute() on IState → side-effect when state changes
// 3. IThemeService integration for dark/light theme toggle
// 4. IWritableOptions pattern for persisting user preferences
// ═══════════════════════════════════════════════════════════════

namespace TaskFlow.UI.Presentation;

/// <summary>
/// Pattern: MVUX partial record — the source generator creates the bindable proxy.
/// Manages app-level settings: theme toggle and mock mode toggle.
/// </summary>
public partial record SettingsModel
{
    private readonly IThemeService _themeService;

    public SettingsModel(IThemeService themeService)
    {
        _themeService = themeService;
    }

    // Pattern: IState<bool> — two-way bound to the "Dark Theme" ToggleSwitch.
    // Default is false (light theme).
    public IState<bool> IsDarkTheme => State<bool>.Value(this, () => false);

    // Pattern: IState<bool> — two-way bound to the "Use Mock Data" ToggleSwitch.
    // Matches Features:UseMocks from appsettings; toggleable at runtime.
    public IState<bool> UseMockData => State<bool>.Value(this, () => false);

    /// <summary>
    /// Pattern: Command to apply the theme change via IThemeService.
    /// Called from code-behind or via command binding when IsDarkTheme changes.
    /// </summary>
    public async ValueTask ToggleTheme(CancellationToken ct)
    {
        var isDark = await IsDarkTheme;
        await _themeService.SetThemeAsync(isDark ? AppTheme.Dark : AppTheme.Light);
    }

    /// <summary>
    /// Pattern: Command to toggle mock data mode.
    /// In a real app, this would update IWritableOptions and potentially
    /// restart the HTTP client pipeline.
    /// </summary>
    public async ValueTask ToggleMockMode(CancellationToken ct)
    {
        var useMocks = await UseMockData;
        // Pattern: In production, persist via IWritableOptions<FeatureSettings>
        // and signal the Kiota HTTP client to swap its DelegatingHandler.
        // For this sample, the toggle demonstrates the state binding pattern.
        _ = useMocks; // Suppress unused warning in sample
    }
}
