using Domain.Shared;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TaskFlow.UI.Converters;

/// <summary>
/// Converts TodoItemStatus flags to a corresponding SolidColorBrush.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TodoItemStatus status)
            return new SolidColorBrush(Colors.Gray);

        var color = status switch
        {
            _ when status.HasFlag(TodoItemStatus.IsCompleted) => Color.FromArgb(255, 76, 175, 80),    // Green
            _ when status.HasFlag(TodoItemStatus.IsCancelled) => Color.FromArgb(255, 211, 47, 47),    // Red
            _ when status.HasFlag(TodoItemStatus.IsArchived) => Color.FromArgb(255, 96, 125, 139),    // BlueGrey
            _ when status.HasFlag(TodoItemStatus.IsBlocked) => Color.FromArgb(255, 255, 152, 0),      // Orange
            _ when status.HasFlag(TodoItemStatus.IsStarted) => Color.FromArgb(255, 25, 118, 210),     // Blue
            TodoItemStatus.None => Color.FromArgb(255, 158, 158, 158),                                  // Grey
            _ => Colors.Gray,
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
