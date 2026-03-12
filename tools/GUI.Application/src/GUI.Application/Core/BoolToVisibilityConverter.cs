using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace XrayDetector.Gui.Core;

/// <summary>
/// Converts bool to Visibility. Supports inverse via ConverterParameter="Inverse".
/// true  -> Visible  (or Collapsed  when Inverse)
/// false -> Collapsed (or Visible   when Inverse)
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        bool inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

        if (inverse)
            boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is Visibility v && v == Visibility.Visible;
        bool inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        return inverse ? !visible : visible;
    }
}
