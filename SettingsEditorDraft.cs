namespace PassNotes;

public sealed class SettingsEditorDraft
{
    public string SelectedCultureName { get; init; } = "";
    public string SelectedThemeId { get; init; } = AppThemeCatalog.StandardThemeId;
    public bool UseSystemTimeZone { get; init; }
    public string? SelectedTimeZoneId { get; init; }
    public int AutoLockMinutes { get; init; }
    public int ClipboardClearSeconds { get; init; }
    public int KeepLastBackups { get; init; }
    public bool AutoBackupEnabled { get; init; }
    public int AutoBackupIntervalHours { get; init; }
    public string VaultPath { get; init; } = "";
    public string BackupsFolderPath { get; init; } = "";
    public bool TrayEnabled { get; init; }
    public bool MinimizeToTray { get; init; }
    public CloseButtonAction CloseButtonAction { get; init; }
    public bool StartMinimizedToTray { get; init; }
    public bool TrayNotificationsEnabled { get; init; }
    public bool CleanLogsEnabled { get; init; }
    public int LogRetentionDays { get; init; }
}
