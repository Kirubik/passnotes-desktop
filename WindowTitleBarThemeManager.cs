using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PassNotes;

internal static class WindowTitleBarThemeManager
{
    private const int DwmaUseImmersiveDarkMode = 20;
    private const int DwmaBorderColor = 34;
    private const int DwmaCaptionColor = 35;
    private const int DwmaTextColor = 36;

    private static bool _initialized;

    public static void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_initialized)
            return;

        _initialized = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    public static void RefreshAllOpenWindows(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (Window window in application.Windows)
            Apply(window);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ReferenceEquals(window, e.OriginalSource))
            return;

        Apply(window);
    }

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowStyle == WindowStyle.None || window.AllowsTransparency)
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var captionColor = TryResolveColor(window, "Brush.TitleBarBackground")
            ?? TryResolveColor(window, "Brush.WindowBackground")
            ?? TryResolveColor(window, "Brush.DialogWindowBackground");
        var textColor = TryResolveColor(window, "Brush.TitleBarText")
            ?? TryResolveColor(window, "Brush.TextPrimary");
        var borderColor = TryResolveColor(window, "Brush.TitleBarBorder")
            ?? TryResolveColor(window, "Brush.BorderStrong")
            ?? TryResolveColor(window, "Brush.Border");

        if (captionColor is Color bg)
        {
            SetIntAttribute(hwnd, DwmaUseImmersiveDarkMode, IsDark(bg) ? 1 : 0);
            SetColorAttribute(hwnd, DwmaCaptionColor, bg);
        }

        if (textColor is Color fg)
            SetColorAttribute(hwnd, DwmaTextColor, fg);

        if (borderColor is Color border)
            SetColorAttribute(hwnd, DwmaBorderColor, border);
    }

    private static Color? TryResolveColor(FrameworkElement element, string resourceKey)
    {
        if (element.TryFindResource(resourceKey) is SolidColorBrush brush)
            return brush.Color;

        if (Application.Current?.TryFindResource(resourceKey) is SolidColorBrush appBrush)
            return appBrush.Color;

        return null;
    }

    private static bool IsDark(Color color)
        => ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) < 128.0;

    private static void SetColorAttribute(IntPtr hwnd, int attribute, Color color)
    {
        var colorRef = color.R | (color.G << 8) | (color.B << 16);
        SetIntAttribute(hwnd, attribute, colorRef);
    }

    private static void SetIntAttribute(IntPtr hwnd, int attribute, int value)
    {
        try
        {
            _ = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
