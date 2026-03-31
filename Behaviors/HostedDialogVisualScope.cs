using System.Windows;

namespace PassNotes.Behaviors;

public static class HostedDialogVisualScope
{
    public static readonly DependencyProperty IsHostedDialogOpenProperty =
        DependencyProperty.RegisterAttached(
            "IsHostedDialogOpen",
            typeof(bool),
            typeof(HostedDialogVisualScope),
            new FrameworkPropertyMetadata(false));

    public static bool GetIsHostedDialogOpen(DependencyObject obj)
        => (bool)obj.GetValue(IsHostedDialogOpenProperty);

    public static void SetIsHostedDialogOpen(DependencyObject obj, bool value)
        => obj.SetValue(IsHostedDialogOpenProperty, value);
}
