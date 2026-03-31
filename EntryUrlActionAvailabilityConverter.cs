using System;
using System.Globalization;
using System.Windows.Data;

namespace PassNotes;

public sealed class EntryUrlActionAvailabilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var url = value as string;
        var action = (parameter as string ?? "copy").Trim();

        return string.Equals(action, "open", StringComparison.OrdinalIgnoreCase)
            ? EntryUrlActions.CanOpenInBrowser(url)
            : EntryUrlActions.CanCopy(url);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
