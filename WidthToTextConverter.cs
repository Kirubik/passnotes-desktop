using System;
using System.Globalization;
using System.Windows.Data;

namespace PassNotes;

public sealed class WidthToTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3)
            return string.Empty;

        double width = 0;
        if (values[0] is double d)
            width = d;

        double threshold = 900;
        if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var t))
            threshold = t;

        var longText = values[1]?.ToString() ?? string.Empty;
        var shortText = values[2]?.ToString() ?? string.Empty;

        return width > 0 && width < threshold ? shortText : longText;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
