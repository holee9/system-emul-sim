using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;
using ParameterExtractor.Core.Models;

namespace XrayDetector.Gui.Core;

/// <summary>
/// Converts ValidationStatus to Brush for UI coloring.
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.Valid => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
                ValidationStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // Orange
                ValidationStatus.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),    // Red
                ValidationStatus.Pending => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
