using System;
using System.Windows;

namespace PassNotes.Behaviors;

public static class AppOwnedModalDialogScope
{
    public static readonly DependencyProperty IsAppOwnedModalDialogOpenProperty =
        DependencyProperty.RegisterAttached(
            "IsAppOwnedModalDialogOpen",
            typeof(bool),
            typeof(AppOwnedModalDialogScope),
            new FrameworkPropertyMetadata(false));

    private static readonly DependencyProperty ScopeDepthProperty =
        DependencyProperty.RegisterAttached(
            "ScopeDepth",
            typeof(int),
            typeof(AppOwnedModalDialogScope),
            new FrameworkPropertyMetadata(0));

    public static bool GetIsAppOwnedModalDialogOpen(DependencyObject obj)
        => (bool)obj.GetValue(IsAppOwnedModalDialogOpenProperty);

    public static void SetIsAppOwnedModalDialogOpen(DependencyObject obj, bool value)
        => obj.SetValue(IsAppOwnedModalDialogOpenProperty, value);

    private static int GetScopeDepth(DependencyObject obj)
        => (int)obj.GetValue(ScopeDepthProperty);

    private static void SetScopeDepth(DependencyObject obj, int value)
        => obj.SetValue(ScopeDepthProperty, value);

    public static bool? ShowDialog(Window dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var owner = dialog.Owner;
        if (owner == null)
            return dialog.ShowDialog();

        PrepareFirstFrame(dialog, owner);
        Enter(owner);
        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            Exit(owner);
        }
    }

    private static void Enter(Window owner)
    {
        var nextDepth = GetScopeDepth(owner) + 1;
        SetScopeDepth(owner, nextDepth);

        if (nextDepth == 1)
            SetIsAppOwnedModalDialogOpen(owner, true);
    }

    private static void Exit(Window owner)
    {
        var nextDepth = Math.Max(0, GetScopeDepth(owner) - 1);
        SetScopeDepth(owner, nextDepth);
        SetIsAppOwnedModalDialogOpen(owner, nextDepth > 0);
    }

    private static void PrepareFirstFrame(Window dialog, Window owner)
    {
        var initialOpacity = dialog.Opacity;
        var revealDone = false;

        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Opacity = 0;
        WindowGeometryHelper.ApplyResponsiveDialogConstraints(dialog, owner);

        void RevealAndCleanup()
        {
            if (revealDone)
                return;

            revealDone = true;
            WindowGeometryHelper.ApplyResponsiveDialogConstraints(dialog, owner);
            CenterOnOwner(dialog, owner);
            dialog.Opacity = initialOpacity;

            dialog.Loaded -= OnLoaded;
            dialog.ContentRendered -= OnContentRendered;
            dialog.Closed -= OnClosed;
        }

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            WindowGeometryHelper.ApplyResponsiveDialogConstraints(dialog, owner);
            CenterOnOwner(dialog, owner);

            if (dialog.ActualWidth > 0 && dialog.ActualHeight > 0)
                RevealAndCleanup();
        }

        void OnContentRendered(object? sender, EventArgs e)
            => RevealAndCleanup();

        void OnClosed(object? sender, EventArgs e)
        {
            dialog.Loaded -= OnLoaded;
            dialog.ContentRendered -= OnContentRendered;
            dialog.Closed -= OnClosed;
        }

        dialog.Loaded += OnLoaded;
        dialog.ContentRendered += OnContentRendered;
        dialog.Closed += OnClosed;
    }

    private static void CenterOnOwner(Window dialog, Window owner)
    {
        WindowGeometryHelper.CenterDialogInWorkArea(dialog, owner);
    }
}
