// ═══════════════════════════════════════════════════════════════
// Pattern: BoolInverterConverter — inverts a boolean value.
// Commonly used for Visibility bindings where you want to show/hide
// based on the opposite of a bound bool property.
//
// Usage in XAML:
//   Visibility="{Binding IsCompleted, Converter={StaticResource BoolInverterConverter}}"
//
// Works with both bool and bool? values.
// ═══════════════════════════════════════════════════════════════

using Microsoft.UI.Xaml.Data;

namespace TaskFlow.UI.Converters;

/// <summary>
/// Pattern: Boolean inversion converter — two-way capable.
/// Converts true→false and false→true for binding inversions.
/// </summary>
public sealed class BoolInverterConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        return value is bool b ? !b : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        // Pattern: Two-way capable — inverting twice returns original value.
        return value is bool b ? !b : value;
    }
}
