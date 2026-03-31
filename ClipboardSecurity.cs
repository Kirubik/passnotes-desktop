using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PassNotes;

/// <summary>
/// Centralized clipboard handling for secrets (e.g., passwords).
/// Supports optional auto-clear with protection against clearing user-changed clipboard content.
/// </summary>
public static class ClipboardSecurity
{
    private static readonly object Gate = new();
    private static DispatcherTimer? _timer;
    private static int _generation;
    private static int _scheduledGeneration;
    private static string? _scheduledToken;

    public static event EventHandler? AutoCleared;

    public static void CopySecret(string text)
    {
        _ = TryCopySecret(text, out _);
    }

    /// <summary>
    /// Try to copy secret text to clipboard and schedule auto-clear.
    /// Returns false if clipboard is unavailable.
    /// </summary>
    public static bool TryCopySecret(string text, out string? failureReason)
    {
        text ??= string.Empty;

        if (!TrySetText(text, out var ex))
        {
            failureReason = ClassifyFailureReason(ex, out _);
            return false;
        }

        var seconds = App.Settings.ClipboardClearSeconds;

        lock (Gate)
        {
            _generation++;
            _scheduledGeneration = _generation;
            _scheduledToken = text;

            EnsureTimer();
            try { _timer!.Stop(); } catch { }

            if (seconds > 0)
            {
                _timer!.Interval = TimeSpan.FromSeconds(seconds);
                _timer!.Start();
            }
        }

        failureReason = null;
        return true;
    }
    /// <summary>
    /// Copy non-secret text to clipboard without scheduling auto-clear.
    /// Also cancels any pending secret auto-clear so we don't clear user clipboard unexpectedly.
    /// </summary>
    public static void CopyText(string text)
    {
        _ = TryCopyText(text, out _);
    }

    /// <summary>
    /// Try to copy non-secret text to clipboard without scheduling auto-clear.
    /// Also cancels any pending secret auto-clear so we don't clear user clipboard unexpectedly.
    /// Returns false if clipboard is unavailable.
    /// </summary>
    public static bool TryCopyText(string text, out string? failureReason)
    {
        text ??= string.Empty;

        if (!TrySetText(text, out var ex))
        {
            failureReason = ClassifyFailureReason(ex, out _);
            return false;
        }

        lock (Gate)
        {
            _generation++;
            _scheduledGeneration = _generation;
            _scheduledToken = null;

            try { _timer?.Stop(); } catch { }
        }

        failureReason = null;
        return true;
    }

    /// <summary>
    /// Copy login/username using the secret clipboard flow (auto-clear if configured).
    /// </summary>
    public static bool TryCopyLogin(string text, out string? failureReason)
        => TryCopySecret(text, out failureReason);


    public static void ClearNow()
    {
        int gen;

        lock (Gate)
        {
            _generation++;
            gen = _generation;
            _scheduledGeneration = gen;
            _scheduledToken = null;

            try { _timer?.Stop(); } catch { }
        }

        AttemptClearForce(gen, 0);
    }

    private static void EnsureTimer()
    {
        if (_timer != null)
            return;

        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => OnAutoClearTick();
    }

    private static void OnAutoClearTick()
    {
        int gen;
        string? token;

        lock (Gate)
        {
            try { _timer?.Stop(); } catch { }
            gen = _scheduledGeneration;
            token = _scheduledToken;
        }

        if (string.IsNullOrEmpty(token))
            return;

        AttemptClearIfMatches(gen, token, 0);
    }

    private static bool IsCurrentGeneration(int gen)
    {
        lock (Gate)
            return gen == _generation;
    }

    private static void AttemptClearIfMatches(int gen, string token, int retry)
    {
        if (!IsCurrentGeneration(gen))
            return;

        try
        {
            string currentText = "";

            try
            {
                if (Clipboard.ContainsText())
                    currentText = Clipboard.GetText() ?? "";
            }
            catch
            {
                currentText = "";
            }

            if (string.Equals(currentText, token, StringComparison.Ordinal))
            {
                Clipboard.Clear();

                lock (Gate)
                {
                    if (_scheduledGeneration == gen)
                        _scheduledToken = null;
                }

                RaiseAutoCleared();
            }
        }
        catch
        {
            ScheduleRetry(() => AttemptClearIfMatches(gen, token, retry + 1), retry);
        }
    }

    private static void AttemptClearForce(int gen, int retry)
    {
        if (!IsCurrentGeneration(gen))
            return;

        try
        {
            Clipboard.Clear();
        }
        catch
        {
            ScheduleRetry(() => AttemptClearForce(gen, retry + 1), retry);
        }
    }

    private static void ScheduleRetry(Action action, int retry)
    {
        const int maxRetries = 3;
        if (retry >= maxRetries)
            return;

        var delayMs = 150 * (retry + 1);

        try
        {
            Task.Delay(delayMs).ContinueWith(_ =>
            {
                try
                {
                    var disp = Application.Current?.Dispatcher;
                    if (disp == null)
                        return;

                    disp.BeginInvoke(action, DispatcherPriority.Background);
                }
                catch { }
            });
        }
        catch { }
    }

    private static void RaiseAutoCleared()
    {
        try { AutoCleared?.Invoke(null, EventArgs.Empty); } catch { }
    }

    private static bool TrySetText(string text, out Exception? ex)
    {
        try
        {
            Clipboard.SetText(text);
            ex = null;
            return true;
        }
        catch (Exception e)
        {
            ex = e;
            return false;
        }
    }

    private static string ClassifyFailureReason(Exception? ex, out string exType)
    {
        exType = ex?.GetType().Name ?? "";

        if (ex is null)
            return "unknown";

        // Clipboard failures are most commonly caused by External/COM exceptions when the clipboard is busy.
        if (ex is ExternalException || ex is COMException)
            return "busy";

        if (ex is SecurityException)
            return "denied";

        return "unknown";
    }
}
