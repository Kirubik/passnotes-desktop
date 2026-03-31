using System;
using System.Globalization;
using System.Windows.Data;

namespace PassNotes;

public sealed class RowNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i)
            return (i + 1).ToString(culture);
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
