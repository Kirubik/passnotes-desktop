using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PassNotes;

/// <summary>
/// Resolves semantic IconData.* values to real Fluent geometry data for XAML paths.
/// </summary>
public sealed class IconDataResourceResolverConverter : IMultiValueConverter
{
    private const string StaticResourcePrefix = "{StaticResource ";

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0)
            return DependencyProperty.UnsetValue;

        object value = values[0];
        if (value is null || value == DependencyProperty.UnsetValue)
            return DependencyProperty.UnsetValue;

        if (value is not string text)
            return value;

        var context = values.Length > 1 ? values[1] as FrameworkElement : null;

        object? resolved = ResolveResourceReferenceChain(
            text,
            context);

        return TryConvertToGeometry(resolved ?? text);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static object? ResolveResourceReferenceChain(string value, FrameworkElement? context)
    {
        object? current = value;

        for (int i = 0; i < 4; i++)
        {
            if (current is not string currentText)
                return current;

            var resolved = TryResolveSingleResourceReference(currentText, context);
            if (resolved is null || ReferenceEquals(resolved, current))
                return current;

            if (resolved is string resolvedText &&
                string.Equals(resolvedText, currentText, StringComparison.Ordinal))
                return current;

            current = resolved;
        }

        return current;
    }

    private static object? TryResolveSingleResourceReference(string value, FrameworkElement? context)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string key;

        if (value.StartsWith(StaticResourcePrefix, StringComparison.Ordinal) &&
            value.EndsWith("}", StringComparison.Ordinal))
        {
            key = value.Substring(
                StaticResourcePrefix.Length,
                value.Length - StaticResourcePrefix.Length - 1).Trim();
        }
        else
        {
            // Support semantic keys like "IconData.Email" when they come from bindings.
            key = value.Trim();
        }

        if (key.Length == 0)
            return null;

        if (context?.TryFindResource(key) is object localResource)
            return localResource;

        return Application.Current?.TryFindResource(key);
    }

    private static object TryConvertToGeometry(object value)
    {
        if (value is Geometry)
            return value;

        if (value is string geometryText)
            return Geometry.Parse(geometryText);

        return value;
    }
}
