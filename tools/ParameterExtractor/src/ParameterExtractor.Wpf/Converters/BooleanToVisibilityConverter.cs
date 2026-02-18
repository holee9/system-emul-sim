using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ParameterExtractor.Wpf.Converters;

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = false;

        // Handle bool values
        if (value is bool b)
        {
            boolValue = b;
        }
        // Handle string values (non-empty = true)
        else if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            boolValue = true;
        }

        // If parameter is "Invert", reverse the logic
        var invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;

        if (invert)
            return boolValue ? Visibility.Collapsed : Visibility.Visible;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            // If parameter is "Invert", reverse the logic
            var invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;

            if (invert)
                return visibility == Visibility.Collapsed;

            return visibility == Visibility.Visible;
        }

        return false;
    }
}
