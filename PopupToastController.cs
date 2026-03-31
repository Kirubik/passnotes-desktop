using System;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace PassNotes;

/// <summary>
/// Small helper to show short-lived Popup "toast" notifications.
/// Ensures only one toast is visible at a time and auto-closes it after a timeout.
/// </summary>
public sealed class PopupToastController
{
    private readonly int _defaultDurationMs;
    private DispatcherTimer? _timer;
    private Popup? _current;
    private Action? _onCloseCurrent;

    public PopupToastController(int defaultDurationMs = 900)
    {
        _defaultDurationMs = defaultDurationMs <= 0 ? 900 : defaultDurationMs;
    }

    public void Show(Popup popup, int? durationMs = null, Action? onClose = null)
    {
        if (popup is null)
            return;

        try
        {
            if (_current != null && !ReferenceEquals(_current, popup))
                CloseCurrentInternal();
        }
        catch { }

        _current = popup;
        _onCloseCurrent = onClose;

        try { popup.IsOpen = true; } catch { }

        EnsureTimer();
        try
        {
            _timer!.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(durationMs ?? _defaultDurationMs);
            _timer.Start();
        }
        catch { }
    }

    public void CloseCurrent()
    {
        try { _timer?.Stop(); } catch { }
        CloseCurrentInternal();
    }

    private void CloseCurrentInternal()
    {
        try
        {
            if (_current != null)
                _current.IsOpen = false;
        }
        catch { }

        try { _onCloseCurrent?.Invoke(); } catch { }
        _onCloseCurrent = null;
    }

    private void EnsureTimer()
    {
        if (_timer != null)
            return;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_defaultDurationMs)
        };

        _timer.Tick += (_, _) =>
        {
            try { _timer!.Stop(); } catch { }
            CloseCurrentInternal();
        };
    }
}
