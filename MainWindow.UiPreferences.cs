using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PassNotes;

public partial class MainWindow
{
    private const double MainWindowRestoreMinWidth = 480.0;
    private const double MainWindowRestoreMinHeight = 360.0;
    private DispatcherTimer? _uiPrefsSaveDebounceTimer;
    private bool _uiPrefsReady;
    private bool _uiPrefsApplying;
    private const int UiPrefsSaveDebounceMs = 600;

    private void ApplyWindowUiPreferencesBestEffort()
    {
        try
        {
            if (App.Settings.UiPrefsVersion != SettingsStore.CurrentUiPrefsVersion)
                return;

            if (!App.Settings.UiMainWindowLeft.HasValue
                || !App.Settings.UiMainWindowTop.HasValue
                || !App.Settings.UiMainWindowWidth.HasValue
                || !App.Settings.UiMainWindowHeight.HasValue)
                return;

            var savedBounds = new Rect(
                App.Settings.UiMainWindowLeft.Value,
                App.Settings.UiMainWindowTop.Value,
                App.Settings.UiMainWindowWidth.Value,
                App.Settings.UiMainWindowHeight.Value);

            var savedContext = TryGetSavedMainWindowGeometryContext();
            var rect = WindowGeometryHelper.TryGetCurrentContext(this, out var currentContext)
                ? WindowGeometryHelper.NormalizeBounds(
                    savedBounds,
                    currentContext,
                    savedContext,
                    MainWindowRestoreMinWidth,
                    MainWindowRestoreMinHeight)
                : ClampToPrimaryWorkAreaFallback(savedBounds);

            _uiPrefsApplying = true;
            try
            {
                WindowStartupLocation = WindowStartupLocation.Manual;

                Left = rect.Left;
                Top = rect.Top;
                Width = rect.Width;
                Height = rect.Height;

                WindowState = App.Settings.UiMainWindowState == 2
                    ? WindowState.Maximized
                    : WindowState.Normal;
            }
            finally
            {
                _uiPrefsApplying = false;
            }
        }
        catch
        {
            _uiPrefsApplying = false;
        }
    }

    private void ApplyRowHeightUiPreferencesBestEffort()
    {
        try
        {
            if (App.Settings.UiPrefsVersion != SettingsStore.CurrentUiPrefsVersion)
                return;

            if (!App.Settings.UiEntriesRowHeight.HasValue)
                return;

            var v = App.Settings.UiEntriesRowHeight.Value;
            v = Math.Clamp(v, RowHeightSlider.Minimum, RowHeightSlider.Maximum);

            _uiPrefsApplying = true;
            try
            {
                RowHeightSlider.Value = v;
            }
            finally
            {
                _uiPrefsApplying = false;
            }
        }
        catch
        {
            _uiPrefsApplying = false;
        }
    }

    private void MarkUiPrefsDirty()
    {
        try
        {
            if (!_uiPrefsReady || _uiPrefsApplying)
                return;

            UpdateWindowUiPrefsFromCurrent();

            _uiPrefsSaveDebounceTimer?.Stop();
            _uiPrefsSaveDebounceTimer?.Start();
        }
        catch { }
    }

    private void RowHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            if (!_uiPrefsReady || _uiPrefsApplying)
                return;

            App.Settings.UiEntriesRowHeight = Math.Clamp(e.NewValue, RowHeightSlider.Minimum, RowHeightSlider.Maximum);

            _uiPrefsSaveDebounceTimer?.Stop();
            _uiPrefsSaveDebounceTimer?.Start();
        }
        catch { }
    }

    private void UpdateWindowUiPrefsFromCurrent()
    {
        try
        {
            if (WindowState == WindowState.Minimized)
                return;

            var b = WindowState == WindowState.Maximized
                ? RestoreBounds
                : new Rect(Left, Top, Width, Height);

            if (double.IsNaN(b.Width) || double.IsNaN(b.Height) || b.Width <= 0 || b.Height <= 0)
                return;

            App.Settings.UiMainWindowState = WindowState == WindowState.Maximized ? 2 : 0;
            App.Settings.UiMainWindowLeft = b.Left;
            App.Settings.UiMainWindowTop = b.Top;
            App.Settings.UiMainWindowWidth = b.Width;
            App.Settings.UiMainWindowHeight = b.Height;

            if (WindowGeometryHelper.TryGetCurrentContext(this, out var currentContext))
            {
                App.Settings.UiMainWindowWorkAreaLeft = currentContext.WorkAreaDip.Left;
                App.Settings.UiMainWindowWorkAreaTop = currentContext.WorkAreaDip.Top;
                App.Settings.UiMainWindowWorkAreaWidth = currentContext.WorkAreaDip.Width;
                App.Settings.UiMainWindowWorkAreaHeight = currentContext.WorkAreaDip.Height;
                App.Settings.UiMainWindowDpiScaleX = currentContext.DpiScaleX;
                App.Settings.UiMainWindowDpiScaleY = currentContext.DpiScaleY;
            }
            else
            {
                App.Settings.UiMainWindowWorkAreaLeft = null;
                App.Settings.UiMainWindowWorkAreaTop = null;
                App.Settings.UiMainWindowWorkAreaWidth = null;
                App.Settings.UiMainWindowWorkAreaHeight = null;
                App.Settings.UiMainWindowDpiScaleX = null;
                App.Settings.UiMainWindowDpiScaleY = null;
            }
        }
        catch { }
    }

    private void SaveUiPreferencesBestEffort(string reason)
    {
        try
        {
            // Ensure we store the latest bounds/state even if a debounce tick didn't happen.
            UpdateWindowUiPrefsFromCurrent();

            SettingsStore.Save(App.Settings);
        }
        catch { }
    }

    private WindowGeometryContext? TryGetSavedMainWindowGeometryContext()
    {
        if (!App.Settings.UiMainWindowWorkAreaLeft.HasValue
            || !App.Settings.UiMainWindowWorkAreaTop.HasValue
            || !App.Settings.UiMainWindowWorkAreaWidth.HasValue
            || !App.Settings.UiMainWindowWorkAreaHeight.HasValue)
            return null;

        var workArea = new Rect(
            App.Settings.UiMainWindowWorkAreaLeft.Value,
            App.Settings.UiMainWindowWorkAreaTop.Value,
            App.Settings.UiMainWindowWorkAreaWidth.Value,
            App.Settings.UiMainWindowWorkAreaHeight.Value);

        if (workArea.Width <= 0 || workArea.Height <= 0)
            return null;

        var dpiScaleX = App.Settings.UiMainWindowDpiScaleX ?? 1.0;
        var dpiScaleY = App.Settings.UiMainWindowDpiScaleY ?? 1.0;
        if (dpiScaleX <= 0 || dpiScaleY <= 0)
            return null;

        return new WindowGeometryContext(workArea, dpiScaleX, dpiScaleY);
    }

    private static Rect ClampToPrimaryWorkAreaFallback(Rect rect)
        => WindowGeometryHelper.ClampToWorkArea(
            rect,
            SystemParameters.WorkArea,
            MainWindowRestoreMinWidth,
            MainWindowRestoreMinHeight);
}
