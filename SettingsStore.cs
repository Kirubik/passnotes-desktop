using System;
using System.IO;
using System.Text.Json;

namespace PassNotes;

public enum CloseButtonAction
{
    Exit = 0,
    MinimizeToTray = 1
}

public sealed class AppSettings
{
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Stable selected application theme identifier.
    /// </summary>
    public string ThemeId { get; set; } = AppThemeCatalog.AmberCircuitThemeId;

    /// <summary>
    /// If true, displayed times follow the current Windows time zone.
    /// </summary>
    public bool UseSystemTimeZone { get; set; } = true;

    /// <summary>
    /// Used when <see cref="UseSystemTimeZone"/> is false.
    /// The value must match <see cref="TimeZoneInfo.Id"/> on Windows.
    /// </summary>
    public string? SelectedTimeZoneId { get; set; } = null;

    /// <summary>
    /// Optional custom display name for the special tree node that represents
    /// entries without a folder ("Без папки"). When null/empty, the localized
    /// default (<see cref="Loc"/> key "FolderNone") is used.
    /// </summary>
    public string? NoFolderDisplayName { get; set; } = null;

    /// <summary>
    /// Auto-lock timeout in minutes. Set to 0 to disable.
    /// </summary>
    public int AutoLockMinutes { get; set; } = 0;


    /// <summary>
    /// Clipboard auto-clear timeout in seconds after copying a secret (e.g., password).
    /// Set to 0 to disable.
    /// </summary>
    public int ClipboardClearSeconds { get; set; } = 0;

    /// <summary>
    /// Full path to the encrypted vault file.
    /// When null/empty, defaults to %APPDATA%\PassNotes\vault.dat
    /// </summary>
    public string? VaultPath { get; set; } = null;

    /// <summary>
    /// Full path to the backups folder.
    /// When null/empty, defaults to %APPDATA%\PassNotes\Backups
    /// </summary>
    public string? BackupsFolderPath { get; set; } = null;

    /// <summary>
    /// Keep last N regular backups (PassNotesBackup_*). Set to 0 to disable pruning.
    /// Note: safety backups (BeforeRestore_*, BeforeVaultSwitch_*) are never pruned automatically.
    /// </summary>
    public int KeepLastBackups { get; set; } = 0;

    /// <summary>
    /// Last directory used for Export… dialog.
    /// </summary>
    public string? LastExportDirectory { get; set; } = null;

    /// <summary>
    /// Last directory used for Import… dialog.
    /// </summary>
    public string? LastImportDirectory { get; set; } = null;

    // -----------------------------
    // Tray
    // -----------------------------

    /// <summary>
    /// If true, the app shows a system tray icon.
    /// </summary>
    public bool TrayEnabled { get; set; } = false;

    /// <summary>
    /// If true, minimizing the main window hides it to the tray (when tray is enabled).
    /// </summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// Determines what the window close button (X) does when tray is enabled.
    /// </summary>
    public CloseButtonAction CloseButtonAction { get; set; } = CloseButtonAction.Exit;

    /// <summary>
    /// If true, the app starts minimized to tray (when tray is enabled).
    /// </summary>
    public bool StartMinimizedToTray { get; set; } = false;

    /// <summary>
    /// If true, tray balloon notifications are allowed for background app events.
    /// </summary>
    public bool TrayNotificationsEnabled { get; set; } = true;

    // -----------------------------
    // Logs cleanup
    // -----------------------------

    /// <summary>
    /// If true, the app will periodically delete old diagnostic/error logs from %APPDATA%\PassNotes.
    /// </summary>
    public bool CleanLogsEnabled { get; set; } = false;

    /// <summary>
    /// Log retention period in days. Supported values: 7/14/30/90/180/365.
    /// </summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>
    /// Last time logs cleanup ran (UTC). Used for rate-limiting.
    /// </summary>
    public DateTime? LastLogsCleanupUtc { get; set; } = null;

    // -----------------------------
    // Attachments orphan cleanup
    // -----------------------------

    /// <summary>
    /// Last time orphan attachments cleanup ran (UTC). Used for rate-limiting.
    /// </summary>
    public DateTime? LastOrphanAttachmentsCleanupUtc { get; set; } = null;

    // -----------------------------
    // Attachments metadata self-heal
    // -----------------------------

    /// <summary>
    /// Last time dangling attachment metadata self-heal ran (UTC). Used for rate-limiting.
    /// </summary>
    public DateTime? LastAttachmentsMetaSelfHealUtc { get; set; } = null;

    // -----------------------------
    // Auto backup
    // -----------------------------

    /// <summary>
    /// If true, the app will automatically create encrypted vault backups on a schedule.
    /// </summary>
    public bool AutoBackupEnabled { get; set; } = false;

    /// <summary>
    /// Auto-backup interval in hours. Supported values: 1/6/12/24.
    /// </summary>
    public int AutoBackupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Last time an auto-backup ran (UTC). Used for rate-limiting.
    /// </summary>
    public DateTime? LastAutoBackupUtc { get; set; } = null;

    // -----------------------------
    // UI preferences (safe)
    // -----------------------------

    /// <summary>
    /// Version of persisted UI preferences. Used to safely reset UI state after major UI redesigns.
    /// </summary>
    public int UiPrefsVersion { get; set; } = 2;

    /// <summary>
    /// Persisted main window state. 0=Normal, 2=Maximized.
    /// (We intentionally do not persist Minimized.)
    /// </summary>
    public int UiMainWindowState { get; set; } = 0;

    public double? UiMainWindowLeft { get; set; } = null;
    public double? UiMainWindowTop { get; set; } = null;
    public double? UiMainWindowWidth { get; set; } = null;
    public double? UiMainWindowHeight { get; set; } = null;
    public double? UiMainWindowWorkAreaLeft { get; set; } = null;
    public double? UiMainWindowWorkAreaTop { get; set; } = null;
    public double? UiMainWindowWorkAreaWidth { get; set; } = null;
    public double? UiMainWindowWorkAreaHeight { get; set; } = null;
    public double? UiMainWindowDpiScaleX { get; set; } = null;
    public double? UiMainWindowDpiScaleY { get; set; } = null;

    /// <summary>
    /// Persisted DataGrid row height (Entries list). When null, default XAML value is used.
    /// </summary>
    public double? UiEntriesRowHeight { get; set; } = null;

}

public static class SettingsStore
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PassNotes");

    private static readonly string SettingsPath = Path.Combine(AppDir, "settings.json");
    private static readonly string SettingsBackupPath = Path.Combine(AppDir, "settings.json.bak");
    private static readonly string SettingsTempPath = Path.Combine(AppDir, "settings.json.tmp");

    public const int CurrentUiPrefsVersion = 2;

    public static string GetDefaultVaultPath() => Path.Combine(AppDir, "vault.dat");

    public static string GetDefaultBackupsFolderPath() => Path.Combine(AppDir, "Backups");

    public static AppSettings Load()
    {
        Directory.CreateDirectory(AppDir);

        // Primary settings file missing: try a best-effort fallback to .bak (if any).
        if (!File.Exists(SettingsPath))
        {
            if (TryLoadFromFile(SettingsBackupPath, out var bakSettings, out var bakError))
            {
                DiagnosticsLog.AppendLine("SETTINGS_LOAD_FALLBACK_BAK_OK", "primary_missing=true");
                return FinalizeLoadedSettings(bakSettings, healPrimary: true);
            }

            if (bakError is not null)
                DiagnosticsLog.AppendLine("SETTINGS_LOAD_FALLBACK_BAK_ERROR", $"primary_missing=true error={bakError.GetType().Name}: {bakError.Message}");

            return Normalize(new AppSettings());
        }

        // Primary settings file exists.
        if (TryLoadFromFile(SettingsPath, out var settings, out var error))
            return FinalizeLoadedSettings(settings, healPrimary: false);

        // Primary failed: log and try fallback to .bak.
        if (error is not null)
            DiagnosticsLog.AppendLine("SETTINGS_LOAD_ERROR", $"file=settings.json error={error.GetType().Name}: {error.Message}");

        if (TryLoadFromFile(SettingsBackupPath, out var bak, out var bakErr))
        {
            DiagnosticsLog.AppendLine("SETTINGS_LOAD_FALLBACK_BAK_OK", "primary_failed=true");
            return FinalizeLoadedSettings(bak, healPrimary: true);
        }

        if (bakErr is not null)
            DiagnosticsLog.AppendLine("SETTINGS_LOAD_FALLBACK_BAK_ERROR", $"primary_failed=true error={bakErr.GetType().Name}: {bakErr.Message}");

        return Normalize(new AppSettings());
    }

    public static void Save(AppSettings settings)
    {
        Normalize(settings);
        Directory.CreateDirectory(AppDir);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

        // Atomic write: write to temp, then replace/move.
        File.WriteAllText(SettingsTempPath, json);

        try
        {
            if (File.Exists(SettingsPath))
            {
                // Keep a single rolling backup .bak.
                File.Replace(SettingsTempPath, SettingsPath, SettingsBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(SettingsTempPath, SettingsPath, overwrite: true);
            }
        }
        finally
        {
            // Best-effort cleanup if the temp file is still around.
            try
            {
                if (File.Exists(SettingsTempPath))
                    File.Delete(SettingsTempPath);
            }
            catch
            {
                // best-effort
            }
        }
    }

    public static string GetAppDir() => AppDir;

    private static bool ApplyUiPrefsVersioning(AppSettings s)
    {
        if (s.UiPrefsVersion == CurrentUiPrefsVersion)
            return false;

        var previousVersion = s.UiPrefsVersion;
        var preservedRowHeight = s.UiEntriesRowHeight;

        // Major UI change detected: do not apply old UI values.
        s.UiPrefsVersion = CurrentUiPrefsVersion;

        s.UiMainWindowState = 0;
        s.UiMainWindowLeft = null;
        s.UiMainWindowTop = null;
        s.UiMainWindowWidth = null;
        s.UiMainWindowHeight = null;
        s.UiMainWindowWorkAreaLeft = null;
        s.UiMainWindowWorkAreaTop = null;
        s.UiMainWindowWorkAreaWidth = null;
        s.UiMainWindowWorkAreaHeight = null;
        s.UiMainWindowDpiScaleX = null;
        s.UiMainWindowDpiScaleY = null;

        // 1 -> 2 only changes DPI/window geometry persistence. Keep the row height preference.
        s.UiEntriesRowHeight = previousVersion == 1
            ? preservedRowHeight
            : null;

        return true;
    }

    private static AppSettings FinalizeLoadedSettings(AppSettings s, bool healPrimary)
    {
        var uiPrefsReset = ApplyUiPrefsVersioning(s);
        var normalized = Normalize(s);

        // Persist the updated version/defaults immediately so future runs don't keep resetting.
        // Also heal primary settings.json if we loaded from .bak.
        if (uiPrefsReset || healPrimary)
        {
            try
            {
                Save(normalized);
            }
            catch (Exception ex)
            {
                var tag = uiPrefsReset
                    ? "SETTINGS_SAVE_AFTER_UI_RESET_FAIL"
                    : "SETTINGS_SAVE_HEAL_FAIL";

                DiagnosticsLog.AppendLine(tag, $"error={ex.GetType().Name}: {ex.Message}");
            }
        }

        return normalized;
    }

    private static bool TryLoadFromFile(string path, out AppSettings settings, out Exception? error)
    {
        settings = new AppSettings();
        error = null;

        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            settings = new AppSettings();
            return false;
        }
    }


    private static AppSettings Normalize(AppSettings s)
    {
        // Ensure UI prefs version is known (older settings.json may deserialize as 0).
        if (s.UiPrefsVersion == 0)
            s.UiPrefsVersion = CurrentUiPrefsVersion;

        // Sanitize UI prefs (only if version matches; otherwise they should have been reset earlier).
        if (s.UiPrefsVersion == CurrentUiPrefsVersion)
        {
            s.UiMainWindowState = s.UiMainWindowState switch
            {
                0 or 2 => s.UiMainWindowState,
                _ => 0
            };

            static bool IsBadDouble(double v) => double.IsNaN(v) || double.IsInfinity(v);

            if (s.UiMainWindowLeft.HasValue && IsBadDouble(s.UiMainWindowLeft.Value))
                s.UiMainWindowLeft = null;
            if (s.UiMainWindowTop.HasValue && IsBadDouble(s.UiMainWindowTop.Value))
                s.UiMainWindowTop = null;
            if (s.UiMainWindowWidth.HasValue && (IsBadDouble(s.UiMainWindowWidth.Value) || s.UiMainWindowWidth.Value <= 0))
                s.UiMainWindowWidth = null;
            if (s.UiMainWindowHeight.HasValue && (IsBadDouble(s.UiMainWindowHeight.Value) || s.UiMainWindowHeight.Value <= 0))
                s.UiMainWindowHeight = null;
            if (s.UiMainWindowWorkAreaLeft.HasValue && IsBadDouble(s.UiMainWindowWorkAreaLeft.Value))
                s.UiMainWindowWorkAreaLeft = null;
            if (s.UiMainWindowWorkAreaTop.HasValue && IsBadDouble(s.UiMainWindowWorkAreaTop.Value))
                s.UiMainWindowWorkAreaTop = null;
            if (s.UiMainWindowWorkAreaWidth.HasValue && (IsBadDouble(s.UiMainWindowWorkAreaWidth.Value) || s.UiMainWindowWorkAreaWidth.Value <= 0))
                s.UiMainWindowWorkAreaWidth = null;
            if (s.UiMainWindowWorkAreaHeight.HasValue && (IsBadDouble(s.UiMainWindowWorkAreaHeight.Value) || s.UiMainWindowWorkAreaHeight.Value <= 0))
                s.UiMainWindowWorkAreaHeight = null;
            if (s.UiMainWindowDpiScaleX.HasValue && (IsBadDouble(s.UiMainWindowDpiScaleX.Value) || s.UiMainWindowDpiScaleX.Value <= 0))
                s.UiMainWindowDpiScaleX = null;
            if (s.UiMainWindowDpiScaleY.HasValue && (IsBadDouble(s.UiMainWindowDpiScaleY.Value) || s.UiMainWindowDpiScaleY.Value <= 0))
                s.UiMainWindowDpiScaleY = null;

            if (s.UiEntriesRowHeight.HasValue && IsBadDouble(s.UiEntriesRowHeight.Value))
                s.UiEntriesRowHeight = null;

            if (s.UiEntriesRowHeight.HasValue)
            {
                // Keep in sync with MainWindow.xaml RowHeightSlider [18..34].
                s.UiEntriesRowHeight = Math.Clamp(s.UiEntriesRowHeight.Value, 18, 34);
            }
        }


        s.ThemeId = AppThemeCatalog.NormalizeThemeId(s.ThemeId);

        if (string.IsNullOrWhiteSpace(s.VaultPath))
            s.VaultPath = GetDefaultVaultPath();

        if (string.IsNullOrWhiteSpace(s.BackupsFolderPath))
            s.BackupsFolderPath = GetDefaultBackupsFolderPath();

        // Ensure no trailing whitespace.
        s.VaultPath = s.VaultPath.Trim();
        s.BackupsFolderPath = s.BackupsFolderPath.Trim();

        if (!string.IsNullOrWhiteSpace(s.LastExportDirectory))
            s.LastExportDirectory = s.LastExportDirectory.Trim();

        if (!string.IsNullOrWhiteSpace(s.LastImportDirectory))
            s.LastImportDirectory = s.LastImportDirectory.Trim();

        // Clamp KeepLastBackups to supported values.
        s.KeepLastBackups = s.KeepLastBackups switch
        {
            0 or 5 or 10 or 20 or 50 => s.KeepLastBackups,
            _ => 0
        };


        // Clamp LogRetentionDays to supported values.
        s.LogRetentionDays = s.LogRetentionDays switch
        {
            7 or 14 or 30 or 90 or 180 or 365 => s.LogRetentionDays,
            _ => 30
        };

        // Clamp AutoBackupIntervalHours to supported values.
        s.AutoBackupIntervalHours = s.AutoBackupIntervalHours switch
        {
            1 or 6 or 12 or 24 => s.AutoBackupIntervalHours,
            _ => 24
        };

        if (s.LastAutoBackupUtc.HasValue)
            s.LastAutoBackupUtc = DateTime.SpecifyKind(s.LastAutoBackupUtc.Value, DateTimeKind.Utc);

        if (s.LastLogsCleanupUtc.HasValue)
            s.LastLogsCleanupUtc = DateTime.SpecifyKind(s.LastLogsCleanupUtc.Value, DateTimeKind.Utc);

        if (s.LastOrphanAttachmentsCleanupUtc.HasValue)
            s.LastOrphanAttachmentsCleanupUtc = DateTime.SpecifyKind(s.LastOrphanAttachmentsCleanupUtc.Value, DateTimeKind.Utc);

        if (s.LastAttachmentsMetaSelfHealUtc.HasValue)
            s.LastAttachmentsMetaSelfHealUtc = DateTime.SpecifyKind(s.LastAttachmentsMetaSelfHealUtc.Value, DateTimeKind.Utc);

        // Clamp CloseButtonAction to known enum values.
        if (!Enum.IsDefined(typeof(CloseButtonAction), s.CloseButtonAction))
            s.CloseButtonAction = CloseButtonAction.Exit;

        return s;
    }
}
