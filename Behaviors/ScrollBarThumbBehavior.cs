using System.Windows;

namespace PassNotes.Behaviors;

public enum ScrollBarThumbAxis
{
    Vertical,
    Horizontal
}

/// <summary>
/// Switches scrollbar thumbs into a compact visual mode when the real thumb length
/// becomes too small for the normal rounded geometry.
/// </summary>
public static class ScrollBarThumbBehavior
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(ScrollBarThumbBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static readonly DependencyProperty AxisProperty =
        DependencyProperty.RegisterAttached(
            "Axis",
            typeof(ScrollBarThumbAxis),
            typeof(ScrollBarThumbBehavior),
            new PropertyMetadata(ScrollBarThumbAxis.Vertical, OnStateInputChanged));

    public static readonly DependencyProperty CompactThresholdProperty =
        DependencyProperty.RegisterAttached(
            "CompactThreshold",
            typeof(double),
            typeof(ScrollBarThumbBehavior),
            new PropertyMetadata(18d, OnStateInputChanged));

    private static readonly DependencyPropertyKey IsCompactPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsCompact",
            typeof(bool),
            typeof(ScrollBarThumbBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;

    public static void SetEnable(DependencyObject element, bool value) =>
        element.SetValue(EnableProperty, value);

    public static bool GetEnable(DependencyObject element) =>
        (bool)element.GetValue(EnableProperty);

    public static void SetAxis(DependencyObject element, ScrollBarThumbAxis value) =>
        element.SetValue(AxisProperty, value);

    public static ScrollBarThumbAxis GetAxis(DependencyObject element) =>
        (ScrollBarThumbAxis)element.GetValue(AxisProperty);

    public static void SetCompactThreshold(DependencyObject element, double value) =>
        element.SetValue(CompactThresholdProperty, value);

    public static double GetCompactThreshold(DependencyObject element) =>
        (double)element.GetValue(CompactThresholdProperty);

    public static bool GetIsCompact(DependencyObject element) =>
        (bool)element.GetValue(IsCompactProperty);

    private static void SetIsCompact(DependencyObject element, bool value) =>
        element.SetValue(IsCompactPropertyKey, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.Loaded -= OnElementLayoutChanged;
        element.SizeChanged -= OnElementSizeChanged;

        if (e.NewValue is bool enabled && enabled)
        {
            element.Loaded += OnElementLayoutChanged;
            element.SizeChanged += OnElementSizeChanged;
            UpdateCompactState(element);
        }
        else
        {
            SetIsCompact(element, false);
        }
    }

    private static void OnStateInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && GetEnable(element))
            UpdateCompactState(element);
    }

    private static void OnElementLayoutChanged(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
            UpdateCompactState(element);
    }

    private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
            UpdateCompactState(element);
    }

    private static void UpdateCompactState(FrameworkElement element)
    {
        var compactThreshold = GetCompactThreshold(element);
        if (compactThreshold <= 0)
        {
            SetIsCompact(element, false);
            return;
        }

        var actualLength = GetAxis(element) switch
        {
            ScrollBarThumbAxis.Horizontal => element.ActualWidth,
            _ => element.ActualHeight
        };

        if (double.IsNaN(actualLength) || double.IsInfinity(actualLength) || actualLength <= 0)
        {
            SetIsCompact(element, false);
            return;
        }

        SetIsCompact(element, actualLength < compactThreshold);
    }
}
