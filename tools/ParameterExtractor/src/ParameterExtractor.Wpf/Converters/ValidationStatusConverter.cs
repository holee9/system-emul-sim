using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Wpf.Converters;

/// <summary>
/// Converts ValidationStatus to background brush for DataGrid rows.
/// </summary>
public class ValidationStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.Valid => new SolidColorBrush(Color.FromRgb(212, 237, 218)),
                ValidationStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 243, 205)),
                ValidationStatus.Error => new SolidColorBrush(Color.FromRgb(248, 215, 218)),
                _ => Brushes.White
            };
        }

        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ValidationStatus to display text.
/// </summary>
public class ValidationStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.Valid => "Valid",
                ValidationStatus.Warning => "Warning",
                ValidationStatus.Error => "Error",
                ValidationStatus.Pending => "Pending",
                _ => "Unknown"
            };
        }

        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
