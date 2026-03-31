using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PassNotes.Behaviors;

/// <summary>
/// Resets single-line search text boxes back to the beginning of the text
/// after they lose keyboard focus, so long queries do not remain visually shifted.
/// </summary>
public static class SearchTextViewportResetBehavior
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(SearchTextViewportResetBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DependencyObject element, bool value) =>
        element.SetValue(EnableProperty, value);

    public static bool GetEnable(DependencyObject element) =>
        (bool)element.GetValue(EnableProperty);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
            return;

        textBox.LostKeyboardFocus -= OnLostKeyboardFocus;

        if (e.NewValue is bool enabled && enabled)
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        textBox.Dispatcher.BeginInvoke(() => ResetViewport(textBox), DispatcherPriority.Background);
    }

    private static void ResetViewport(TextBox textBox)
    {
        if (string.IsNullOrEmpty(textBox.Text))
            return;

        textBox.CaretIndex = 0;
        textBox.Select(0, 0);
        textBox.ScrollToHome();
    }
}