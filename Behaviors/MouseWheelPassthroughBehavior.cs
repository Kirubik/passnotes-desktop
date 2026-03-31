using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PassNotes.Behaviors;

/// <summary>
/// Prevents wheel scrolling inside search text boxes and forwards the wheel event
/// to the nearest parent UI element so outer containers can keep their normal behavior.
/// </summary>
public static class MouseWheelPassthroughBehavior
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(MouseWheelPassthroughBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DependencyObject element, bool value) =>
        element.SetValue(EnableProperty, value);

    public static bool GetEnable(DependencyObject element) =>
        (bool)element.GetValue(EnableProperty);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        element.PreviewMouseWheel -= OnPreviewMouseWheel;

        if (e.NewValue is bool enabled && enabled)
            element.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not UIElement element)
            return;

        e.Handled = true;

        var parent = FindParent<UIElement>(element);
        if (parent is null)
            return;

        var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };

        parent.RaiseEvent(forwardedEvent);
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T typed)
                return typed;
        }

        return null;
    }
}