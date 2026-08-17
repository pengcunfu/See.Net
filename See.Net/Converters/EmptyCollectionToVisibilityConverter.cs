using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace See.Converters;

/// <summary>空集合 / null → Collapsed，否则 Visible。</summary>
public sealed class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return Visibility.Collapsed;
        if (value is ICollection c) return c.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is IEnumerable e)
        {
            var enumerator = e.GetEnumerator();
            try { return enumerator.MoveNext() ? Visibility.Visible : Visibility.Collapsed; }
            finally { (enumerator as IDisposable)?.Dispose(); }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
