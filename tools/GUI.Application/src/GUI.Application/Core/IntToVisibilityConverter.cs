using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace XrayDetector.Gui.Core;

/// <summary>
/// Converts integer values to Visibility.
/// Returns Visible when value > 0 (or < 0 for inverse), Collapsed otherwise.
/// </summary>
public sealed class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            var inverse = parameter?.ToString() == "inverse";
            if (inverse)
                return intValue > 0 ? Visibility.Collapsed : Visibility.Visible;
            else
                return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
