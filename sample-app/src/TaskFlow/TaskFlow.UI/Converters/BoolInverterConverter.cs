using Microsoft.UI.Xaml.Data;

namespace TaskFlow.UI.Converters;

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class BoolInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : value;
}
