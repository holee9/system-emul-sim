using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace XrayDetector.Gui.Core;

/// <summary>Converts bool to Brush: true → Green (#4CAF50), false → Red (#F44336).</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool ready = value is bool b && b;
        return ready
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
            : new SolidColorBrush(Color.FromRgb(244, 67, 54));  // Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
