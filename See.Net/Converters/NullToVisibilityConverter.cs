using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace See.Converters;

/// <summary>
/// null → Collapsed，非 null → Visible。
/// ConverterParameter 为 Invert / inverted / true 时取反。
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = parameter is string s &&
                      (s.Equals("Invert", StringComparison.OrdinalIgnoreCase)
                       || s.Equals("inverted", StringComparison.OrdinalIgnoreCase)
                       || s.Equals("true", StringComparison.OrdinalIgnoreCase));
        bool visible = value is not null;
        if (invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
