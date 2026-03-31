using System;
using System.Windows.Threading;

namespace PassNotes;

/// <summary>
/// Periodically creates encrypted vault backups by copying the vault file.
/// Best-effort: never throws, rate-limited by App.Settings.LastAutoBackupUtc.
/// </summary>
public sealed class AutoBackupService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _started;

    // Local backoff to avoid spamming attempts when something is consistently failing.
    private DateTime _lastAttemptUtc = DateTime.MinValue;

    public AutoBackupService(TimeSpan checkInterval)
    {
        if (checkInterval <= TimeSpan.Zero)
            checkInterval = TimeSpan.FromMinutes(10);

        _timer = new DispatcherTimer
        {
            Interval = checkInterval
        };
        _timer.Tick += (_, _) => TryRunOnce();
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;

        // Run once at startup (best-effort).
        TryRunOnce();

        try { _timer.Start(); } catch { }
    }

    private void TryRunOnce()
    {
        try
        {
            var settings = App.Settings;
            if (settings == null)
                return;

            if (!settings.AutoBackupEnabled)
                return;

            var nowUtc = DateTime.UtcNow;

            // Backoff after a failed attempt.
            if (_lastAttemptUtc != DateTime.MinValue && (nowUtc - _lastAttemptUtc) < TimeSpan.FromMinutes(30))
                return;

            var intervalHours = settings.AutoBackupIntervalHours;
            intervalHours = intervalHours switch
            {
                1 or 6 or 12 or 24 => intervalHours,
                _ => 24
            };

            var last = settings.LastAutoBackupUtc;
            if (last.HasValue)
            {
                // Ensure UTC kind.
                var lastUtc = DateTime.SpecifyKind(last.Value, DateTimeKind.Utc);
                if ((nowUtc - lastUtc) < TimeSpan.FromHours(intervalHours))
                    return;
            }

            // Attempt the backup.
            _lastAttemptUtc = nowUtc;

            // NOTE: CreateBackupNowAuto uses VaultIoGate internally.
            BackupService.CreateBackupNowAuto();

            // Mark success.
            try
            {
                settings.LastAutoBackupUtc = nowUtc;
                SettingsStore.Save(settings);
            }
            catch
            {
                // best-effort
            }
        }
        catch
        {
            // best-effort
        }
    }

    public void Dispose()
    {
        try { _timer.Stop(); } catch { }
    }
}
