// ═══════════════════════════════════════════════════════════════
// Pattern: StringFormatConverter — IValueConverter for formatted display strings.
// Supports string.Format with ConverterParameter as the format template.
//
// Usage in XAML:
//   Text="{Binding DueDate, Converter={StaticResource StringFormatConverter},
//          ConverterParameter='Due: {0:d}'}"
//
// Null-safe: returns empty string for null values.
// ═══════════════════════════════════════════════════════════════

using Microsoft.UI.Xaml.Data;

namespace TaskFlow.UI.Converters;

/// <summary>
/// Pattern: Generic string format converter — formats any value using string.Format.
/// ConverterParameter provides the format string (e.g., "Due: {0:d}").
/// </summary>
public sealed class StringFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is null)
            return string.Empty;

        if (parameter is string format && !string.IsNullOrEmpty(format))
            return string.Format(format, value);

        return value.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        // Pattern: One-way converter — ConvertBack is not supported.
        throw new NotSupportedException("StringFormatConverter is one-way only.");
    }
}
