using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace PassNotes;

internal readonly record struct WindowGeometryContext(
    Rect WorkAreaDip,
    double DpiScaleX,
    double DpiScaleY);

internal static class WindowGeometryHelper
{
    private const double DefaultMargin = 24.0;

    public static bool TryGetCurrentContext(Window window, out WindowGeometryContext context)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            var source = HwndSource.FromHwnd(handle);
            if (source?.CompositionTarget != null)
            {
                var transformFromDevice = source.CompositionTarget.TransformFromDevice;
                var workingAreaPx = Forms.Screen.FromHandle(handle).WorkingArea;
                var topLeft = transformFromDevice.Transform(new Point(workingAreaPx.Left, workingAreaPx.Top));
                var bottomRight = transformFromDevice.Transform(new Point(workingAreaPx.Right, workingAreaPx.Bottom));
                var dpi = VisualTreeHelper.GetDpi(window);
                var workAreaDip = new Rect(topLeft, bottomRight);

                if (IsValidWorkArea(workAreaDip) && dpi.DpiScaleX > 0 && dpi.DpiScaleY > 0)
                {
                    context = new WindowGeometryContext(workAreaDip, dpi.DpiScaleX, dpi.DpiScaleY);
                    return true;
                }
            }
        }

        var fallbackWorkArea = SystemParameters.WorkArea;
        if (IsValidWorkArea(fallbackWorkArea))
        {
            context = new WindowGeometryContext(fallbackWorkArea, 1.0, 1.0);
            return true;
        }

        context = default;
        return false;
    }

    public static Rect NormalizeBounds(
        Rect savedBounds,
        WindowGeometryContext currentContext,
        WindowGeometryContext? savedContext,
        double minWidth,
        double minHeight)
    {
        var candidate = savedBounds;

        if (savedContext.HasValue && IsContextCompatible(savedContext.Value))
            candidate = ReprojectBounds(savedBounds, savedContext.Value.WorkAreaDip, currentContext.WorkAreaDip);

        return ClampToWorkArea(candidate, currentContext.WorkAreaDip, minWidth, minHeight);
    }

    public static Rect ClampToWorkArea(Rect rect, Rect workArea, double minWidth, double minHeight)
    {
        if (!IsValidWorkArea(workArea))
            return rect;

        var availableWidth = Math.Max(0, workArea.Width - (DefaultMargin * 2.0));
        var availableHeight = Math.Max(0, workArea.Height - (DefaultMargin * 2.0));

        var width = availableWidth >= minWidth
            ? Math.Clamp(rect.Width, minWidth, availableWidth)
            : Math.Min(Math.Max(0, rect.Width), workArea.Width);

        var height = availableHeight >= minHeight
            ? Math.Clamp(rect.Height, minHeight, availableHeight)
            : Math.Min(Math.Max(0, rect.Height), workArea.Height);

        var minLeft = workArea.Left + (availableWidth >= minWidth ? DefaultMargin : 0);
        var maxLeft = workArea.Right - width - (availableWidth >= minWidth ? DefaultMargin : 0);
        var minTop = workArea.Top + (availableHeight >= minHeight ? DefaultMargin : 0);
        var maxTop = workArea.Bottom - height - (availableHeight >= minHeight ? DefaultMargin : 0);

        var left = maxLeft >= minLeft
            ? Math.Clamp(rect.Left, minLeft, maxLeft)
            : workArea.Left + Math.Max(0, (workArea.Width - width) / 2.0);

        var top = maxTop >= minTop
            ? Math.Clamp(rect.Top, minTop, maxTop)
            : workArea.Top + Math.Max(0, (workArea.Height - height) / 2.0);

        return new Rect(left, top, width, height);
    }

    public static Rect GetDialogWorkArea(Window dialog, Window? owner = null)
    {
        if (owner != null && TryGetCurrentContext(owner, out var ownerContext))
            return ownerContext.WorkAreaDip;

        if (TryGetCurrentContext(dialog, out var dialogContext))
            return dialogContext.WorkAreaDip;

        return SystemParameters.WorkArea;
    }

    public static void ApplyResponsiveDialogConstraints(Window dialog, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var workArea = GetDialogWorkArea(dialog, owner);
        if (!IsValidWorkArea(workArea))
            return;

        var maxWidth = Math.Max(240, workArea.Width - (DefaultMargin * 2.0));
        var maxHeight = Math.Max(160, workArea.Height - (DefaultMargin * 2.0));

        dialog.MaxWidth = double.IsInfinity(dialog.MaxWidth)
            ? maxWidth
            : Math.Min(dialog.MaxWidth, maxWidth);
        dialog.MaxHeight = double.IsInfinity(dialog.MaxHeight)
            ? maxHeight
            : Math.Min(dialog.MaxHeight, maxHeight);

        if (dialog.MinWidth > dialog.MaxWidth)
            dialog.MinWidth = dialog.MaxWidth;

        if (dialog.MinHeight > dialog.MaxHeight)
            dialog.MinHeight = dialog.MaxHeight;

        if (!double.IsNaN(dialog.Width) && dialog.Width > dialog.MaxWidth)
            dialog.Width = dialog.MaxWidth;

        if (!double.IsNaN(dialog.Height) && dialog.Height > dialog.MaxHeight)
            dialog.Height = dialog.MaxHeight;
    }

    public static void CenterDialogInWorkArea(Window dialog, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var workArea = GetDialogWorkArea(dialog, owner);
        if (!IsValidWorkArea(workArea))
            return;

        var dialogWidth = GetCurrentWindowWidth(dialog);
        var dialogHeight = GetCurrentWindowHeight(dialog);
        if (dialogWidth <= 0 || dialogHeight <= 0)
            return;

        var left = workArea.Left + ((workArea.Width - dialogWidth) / 2.0);
        var top = workArea.Top + ((workArea.Height - dialogHeight) / 2.0);

        var normalized = ClampToWorkArea(new Rect(left, top, dialogWidth, dialogHeight), workArea, 0, 0);
        dialog.Left = normalized.Left;
        dialog.Top = normalized.Top;
    }

    public static double GetCurrentWindowWidth(Window dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (dialog.ActualWidth > 0)
            return dialog.ActualWidth;

        if (!double.IsNaN(dialog.Width) && dialog.Width > 0)
            return dialog.Width;

        if (dialog.MinWidth > 0)
            return dialog.MinWidth;

        return 0;
    }

    public static double GetCurrentWindowHeight(Window dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (dialog.ActualHeight > 0)
            return dialog.ActualHeight;

        if (!double.IsNaN(dialog.Height) && dialog.Height > 0)
            return dialog.Height;

        if (dialog.MinHeight > 0)
            return dialog.MinHeight;

        return 0;
    }

    private static bool IsValidWorkArea(Rect workArea)
        => !double.IsNaN(workArea.Width)
           && !double.IsNaN(workArea.Height)
           && !double.IsInfinity(workArea.Width)
           && !double.IsInfinity(workArea.Height)
           && workArea.Width > 0
           && workArea.Height > 0;

    private static bool IsContextCompatible(WindowGeometryContext context)
        => IsValidWorkArea(context.WorkAreaDip)
           && context.DpiScaleX > 0
           && context.DpiScaleY > 0;

    private static Rect ReprojectBounds(Rect bounds, Rect savedWorkArea, Rect currentWorkArea)
    {
        if (!IsValidWorkArea(savedWorkArea) || !IsValidWorkArea(currentWorkArea))
            return bounds;

        var widthRatio = bounds.Width / savedWorkArea.Width;
        var heightRatio = bounds.Height / savedWorkArea.Height;

        var width = currentWorkArea.Width * widthRatio;
        var height = currentWorkArea.Height * heightRatio;

        var savedXSpan = Math.Max(1.0, savedWorkArea.Width - bounds.Width);
        var savedYSpan = Math.Max(1.0, savedWorkArea.Height - bounds.Height);
        var xRatio = Math.Clamp((bounds.Left - savedWorkArea.Left) / savedXSpan, 0.0, 1.0);
        var yRatio = Math.Clamp((bounds.Top - savedWorkArea.Top) / savedYSpan, 0.0, 1.0);

        var currentXSpan = Math.Max(0.0, currentWorkArea.Width - width);
        var currentYSpan = Math.Max(0.0, currentWorkArea.Height - height);

        var left = currentWorkArea.Left + (currentXSpan * xRatio);
        var top = currentWorkArea.Top + (currentYSpan * yRatio);

        return new Rect(left, top, width, height);
    }
}
