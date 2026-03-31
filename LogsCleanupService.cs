using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PassNotes;

/// <summary>
/// Best-effort periodic cleanup of old diagnostic/error logs in %APPDATA%\PassNotes.
/// Must never crash the app.
/// </summary>
public sealed class LogsCleanupService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private int _isRunning;

    public LogsCleanupService(TimeSpan? interval = null)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval ?? TimeSpan.FromMinutes(10)
        };

        _timer.Tick += (_, __) => Trigger();
    }

    public void Start()
    {
        // Run at startup.
        Trigger();

        try { _timer.Start(); } catch { }
    }

    public void Dispose()
    {
        try { _timer.Stop(); } catch { }
    }

    private void Trigger()
    {
        // Prevent overlapping runs.
        if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                TryCleanupIfDue();
            }
            catch
            {
                // best-effort
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        });
    }

    /// <summary>
    /// Runs cleanup if enabled and due. Rate-limited to at most once per 24h via App.Settings.LastLogsCleanupUtc.
    /// Safe to call frequently.
    /// </summary>
    public static void TryCleanupIfDue()
    {
        try
        {
            var settings = App.Settings;
            if (settings == null)
                return;

            if (!settings.CleanLogsEnabled)
                return;

            var nowUtc = DateTime.UtcNow;
            var last = settings.LastLogsCleanupUtc;

            if (last.HasValue)
            {
                var delta = nowUtc - DateTime.SpecifyKind(last.Value, DateTimeKind.Utc);
                if (delta < TimeSpan.FromDays(1))
                    return;
            }

            var retentionDays = settings.LogRetentionDays;
            // Defensive clamp; SettingsStore.Normalize should already enforce this.
            retentionDays = retentionDays switch
            {
                7 or 14 or 30 or 90 or 180 or 365 => retentionDays,
                _ => 30
            };

            var cutoffUtc = nowUtc.AddDays(-retentionDays);
            var dir = SettingsStore.GetAppDir();

            // Minimum required set.
            var files = new[]
            {
                Path.Combine(dir, "diagnostic.log"),
                Path.Combine(dir, "last_error.txt")
            };

            foreach (var path in files)
            {
                try
                {
                    if (!File.Exists(path))
                        continue;

                    DateTime lastWriteUtc;
                    try { lastWriteUtc = File.GetLastWriteTimeUtc(path); }
                    catch { continue; }

                    if (lastWriteUtc < cutoffUtc)
                    {
                        try { File.Delete(path); }
                        catch { /* best-effort */ }
                    }
                }
                catch
                {
                    // best-effort per-file
                }
            }

            // Rate-limit: update after attempt (even if nothing deleted).
            settings.LastLogsCleanupUtc = nowUtc;
            try { SettingsStore.Save(settings); } catch { }
        }
        catch
        {
            // best-effort
        }
    }
}
