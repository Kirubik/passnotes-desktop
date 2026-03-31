using System;
using System.Globalization;
using System.Windows.Data;

namespace PassNotes;

/// <summary>
/// Converts stored UTC DateTime values to the currently selected display timezone.
/// </summary>
public sealed class UtcToDisplayDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return TimeZoneService.ConvertFromUtc(dt);

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
