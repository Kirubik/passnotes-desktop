using System;

namespace PassNotes;

/// <summary>
/// Centralizes timezone selection (System or user-selected) and notifies the UI
/// when displayed times must be recalculated.
/// </summary>
public static class TimeZoneService
{
    private static AppSettings? _settings;

    public static event EventHandler? TimeZoneChanged;

    public static void Initialize(AppSettings settings)
    {
        _settings = settings;
    }

    public static bool UseSystemTimeZone => _settings?.UseSystemTimeZone ?? true;

    public static string? SelectedTimeZoneId => _settings?.SelectedTimeZoneId;

    public static TimeZoneInfo CurrentTimeZone
    {
        get
        {
            if (_settings is null)
                return TimeZoneInfo.Local;

            if (_settings.UseSystemTimeZone)
                return TimeZoneInfo.Local;

            if (string.IsNullOrWhiteSpace(_settings.SelectedTimeZoneId))
                return TimeZoneInfo.Local;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(_settings.SelectedTimeZoneId);
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }

    public static DateTime ConvertFromUtc(DateTime utc)
    {
        // Be defensive about Kind; JSON can sometimes produce Unspecified.
        if (utc.Kind == DateTimeKind.Unspecified)
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        else if (utc.Kind == DateTimeKind.Local)
            utc = utc.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, CurrentTimeZone);
    }

    public static string GetCurrentOffsetLabel()
    {
        var tz = CurrentTimeZone;
        var offset = tz.GetUtcOffset(DateTime.UtcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return $"UTC{sign}{offset:hh\\:mm}";
    }

    public static string GetSystemOffsetLabel()
    {
        var tz = TimeZoneInfo.Local;
        var offset = tz.GetUtcOffset(DateTime.UtcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return $"UTC{sign}{offset:hh\\:mm}";
    }

    public static void NotifyTimeZoneChanged()
    {
        TimeZoneChanged?.Invoke(null, EventArgs.Empty);
    }
}
