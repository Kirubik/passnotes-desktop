using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PassNotes;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public sealed class LanguageOption
    {
        public string CultureName { get; init; } = "en-US";
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class TimeZoneOption
    {
        public string? Id { get; init; }
        public string DisplayName { get; init; } = "";
        public bool IsSystem { get; init; }

        public override string ToString() => DisplayName;
    }

    public sealed class ThemeOption
    {
        public string Id { get; init; } = AppThemeCatalog.StandardThemeId;
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class AutoLockOption
    {
        public int Minutes { get; init; }
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class ClipboardClearOption
    {
        public int Seconds { get; init; }
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class KeepBackupsOption
    {
        public int Count { get; init; }
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class CloseButtonActionOption
    {
        public CloseButtonAction Action { get; init; }
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class LogRetentionOption
    {
        public int Days { get; init; }
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public sealed class AutoBackupIntervalOption
    {
        public int Hours { get; init; }
        public string DisplayName { get; init; } = "";

        public override string ToString() => DisplayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<LanguageOption> Languages { get; } = new();
    public ObservableCollection<ThemeOption> Themes { get; } = new();
    public ObservableCollection<TimeZoneOption> TimeZones { get; } = new();
    public ObservableCollection<AutoLockOption> AutoLockOptions { get; } = new();
    public ObservableCollection<ClipboardClearOption> ClipboardClearOptions { get; } = new();
    public ObservableCollection<KeepBackupsOption> KeepBackupsOptions { get; } = new();
    public ObservableCollection<CloseButtonActionOption> CloseButtonActionOptions { get; } = new();
    public ObservableCollection<LogRetentionOption> LogRetentionOptions { get; } = new();
    public ObservableCollection<AutoBackupIntervalOption> AutoBackupIntervalOptions { get; } = new();


    private LanguageOption? _selectedLanguage;
    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (ReferenceEquals(_selectedLanguage, value)) return;
            _selectedLanguage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCultureName));
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private ThemeOption? _selectedTheme;
    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (ReferenceEquals(_selectedTheme, value)) return;
            _selectedTheme = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedThemeId));
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private TimeZoneOption? _selectedTimeZone;
    public TimeZoneOption? SelectedTimeZone
    {
        get => _selectedTimeZone;
        set
        {
            if (ReferenceEquals(_selectedTimeZone, value)) return;
            _selectedTimeZone = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UseSystemTimeZone));
            OnPropertyChanged(nameof(SelectedTimeZoneId));
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private AutoLockOption? _selectedAutoLock;
    public AutoLockOption? SelectedAutoLock
    {
        get => _selectedAutoLock;
        set
        {
            if (ReferenceEquals(_selectedAutoLock, value)) return;
            _selectedAutoLock = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoLockMinutes));
            OnPropertyChanged(nameof(HasChanges));
        }
    }


    private ClipboardClearOption? _selectedClipboardClear;
    public ClipboardClearOption? SelectedClipboardClear
    {
        get => _selectedClipboardClear;
        set
        {
            if (ReferenceEquals(_selectedClipboardClear, value)) return;
            _selectedClipboardClear = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ClipboardClearSeconds));
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private KeepBackupsOption? _selectedKeepBackups;
    public KeepBackupsOption? SelectedKeepBackups
    {
        get => _selectedKeepBackups;
        set
        {
            if (ReferenceEquals(_selectedKeepBackups, value)) return;
            _selectedKeepBackups = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KeepLastBackups));
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private CloseButtonActionOption? _selectedCloseButtonAction;
    public CloseButtonActionOption? SelectedCloseButtonAction
    {
        get => _selectedCloseButtonAction;
        set
        {
            if (ReferenceEquals(_selectedCloseButtonAction, value)) return;
            _selectedCloseButtonAction = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CloseButtonAction));
            OnPropertyChanged(nameof(HasChanges));
        }
    }



private LogRetentionOption? _selectedLogRetention;
public LogRetentionOption? SelectedLogRetention
{
    get => _selectedLogRetention;
    set
    {
        if (ReferenceEquals(_selectedLogRetention, value)) return;
        _selectedLogRetention = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(LogRetentionDays));
        OnPropertyChanged(nameof(HasChanges));
    }
}

private AutoBackupIntervalOption? _selectedAutoBackupInterval;
public AutoBackupIntervalOption? SelectedAutoBackupInterval
{
    get => _selectedAutoBackupInterval;
    set
    {
        if (ReferenceEquals(_selectedAutoBackupInterval, value)) return;
        _selectedAutoBackupInterval = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(AutoBackupIntervalHours));
        OnPropertyChanged(nameof(HasChanges));
    }
}


private bool _autoBackupEnabled;
public bool AutoBackupEnabled
{
    get => _autoBackupEnabled;
    set
    {
        if (_autoBackupEnabled == value) return;
        _autoBackupEnabled = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(HasChanges));
    }
}

private bool _cleanLogsEnabled;
public bool CleanLogsEnabled
{
    get => _cleanLogsEnabled;
    set
    {
        if (_cleanLogsEnabled == value) return;
        _cleanLogsEnabled = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(HasChanges));
    }
}
    public string InitialLanguage { get; }
    public string InitialThemeId { get; }
    public bool InitialUseSystemTimeZone { get; }
    public string? InitialTimeZoneId { get; }
    public int InitialAutoLockMinutes { get; }
    public int InitialClipboardClearSeconds { get; }
    public int InitialKeepLastBackups { get; }

    public bool InitialTrayEnabled { get; }
    public bool InitialMinimizeToTray { get; }
    public CloseButtonAction InitialCloseButtonAction { get; }
    public bool InitialStartMinimizedToTray { get; }
    public bool InitialTrayNotificationsEnabled { get; }

    public bool InitialCleanLogsEnabled { get; }
    public int InitialLogRetentionDays { get; }

    public bool InitialAutoBackupEnabled { get; }
    public int InitialAutoBackupIntervalHours { get; }

    public string InitialVaultPath { get; }
    public string InitialBackupsFolderPath { get; }

    public string DefaultVaultPath { get; }
    public string DefaultBackupsFolderPath { get; }

    private bool _trayEnabled;
    public bool TrayEnabled
    {
        get => _trayEnabled;
        set
        {
            if (_trayEnabled == value) return;
            _trayEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private bool _minimizeToTray;
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (_minimizeToTray == value) return;
            _minimizeToTray = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private bool _startMinimizedToTray;
    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set
        {
            if (_startMinimizedToTray == value) return;
            _startMinimizedToTray = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private bool _trayNotificationsEnabled;
    public bool TrayNotificationsEnabled
    {
        get => _trayNotificationsEnabled;
        set
        {
            if (_trayNotificationsEnabled == value) return;
            _trayNotificationsEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChanges));
        }
    }

    private string _vaultPath = "";
    public string VaultPath
    {
        get => _vaultPath;
        set
        {
            value ??= "";
            if (string.Equals(_vaultPath, value, StringComparison.OrdinalIgnoreCase))
                return;
            _vaultPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(CanRestoreStoragePathsDefaults));
        }
    }

    private string _backupsFolderPath = "";
    public string BackupsFolderPath
    {
        get => _backupsFolderPath;
        set
        {
            value ??= "";
            if (string.Equals(_backupsFolderPath, value, StringComparison.OrdinalIgnoreCase))
                return;
            _backupsFolderPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(CanRestoreStoragePathsDefaults));
        }
    }

    public bool CanRestoreStoragePathsDefaults
        => !string.Equals(VaultPath, DefaultVaultPath, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(BackupsFolderPath, DefaultBackupsFolderPath, StringComparison.OrdinalIgnoreCase);

    public void RestoreStoragePathsToDefaults()
    {
        VaultPath = DefaultVaultPath;
        BackupsFolderPath = DefaultBackupsFolderPath;
        OnPropertyChanged(nameof(CanRestoreStoragePathsDefaults));
    }

    public string SelectedCultureName => SelectedLanguage?.CultureName ?? InitialLanguage;
    public string SelectedThemeId => SelectedTheme?.Id ?? InitialThemeId;
    public bool UseSystemTimeZone => SelectedTimeZone?.IsSystem ?? true;
    public string? SelectedTimeZoneId => UseSystemTimeZone ? null : SelectedTimeZone?.Id;
    public int AutoLockMinutes => SelectedAutoLock?.Minutes ?? InitialAutoLockMinutes;
    public int ClipboardClearSeconds => SelectedClipboardClear?.Seconds ?? InitialClipboardClearSeconds;
    public int KeepLastBackups => SelectedKeepBackups?.Count ?? InitialKeepLastBackups;
    public CloseButtonAction CloseButtonAction => SelectedCloseButtonAction?.Action ?? InitialCloseButtonAction;


    public int LogRetentionDays => SelectedLogRetention?.Days ?? InitialLogRetentionDays;


    public int AutoBackupIntervalHours => SelectedAutoBackupInterval?.Hours ?? InitialAutoBackupIntervalHours;

    public bool HasChanges
        => !string.Equals(SelectedCultureName, InitialLanguage, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(SelectedThemeId, InitialThemeId, StringComparison.OrdinalIgnoreCase)
           || UseSystemTimeZone != InitialUseSystemTimeZone
           || (!UseSystemTimeZone && !string.Equals(SelectedTimeZoneId, InitialTimeZoneId, StringComparison.OrdinalIgnoreCase))
           || AutoLockMinutes != InitialAutoLockMinutes
           || ClipboardClearSeconds != InitialClipboardClearSeconds
           || KeepLastBackups != InitialKeepLastBackups
           || TrayEnabled != InitialTrayEnabled
           || MinimizeToTray != InitialMinimizeToTray
           || CloseButtonAction != InitialCloseButtonAction
           || StartMinimizedToTray != InitialStartMinimizedToTray
           || TrayNotificationsEnabled != InitialTrayNotificationsEnabled
           || CleanLogsEnabled != InitialCleanLogsEnabled
           || LogRetentionDays != InitialLogRetentionDays
           || AutoBackupEnabled != InitialAutoBackupEnabled
           || AutoBackupIntervalHours != InitialAutoBackupIntervalHours
           || !string.Equals(VaultPath, InitialVaultPath, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(BackupsFolderPath, InitialBackupsFolderPath, StringComparison.OrdinalIgnoreCase);

    public SettingsViewModel(AppSettings settings)
    {
        InitialLanguage = string.IsNullOrWhiteSpace(settings.Language) ? "en-US" : settings.Language;
        InitialThemeId = AppThemeCatalog.NormalizeThemeId(settings.ThemeId);
        InitialUseSystemTimeZone = settings.UseSystemTimeZone;
        InitialTimeZoneId = settings.SelectedTimeZoneId;
        InitialAutoLockMinutes = settings.AutoLockMinutes;
        InitialClipboardClearSeconds = settings.ClipboardClearSeconds;
        InitialKeepLastBackups = settings.KeepLastBackups;

        InitialTrayEnabled = settings.TrayEnabled;
        InitialMinimizeToTray = settings.MinimizeToTray;
        InitialCloseButtonAction = settings.CloseButtonAction;
        InitialStartMinimizedToTray = settings.StartMinimizedToTray;
        InitialTrayNotificationsEnabled = settings.TrayNotificationsEnabled;


        InitialCleanLogsEnabled = settings.CleanLogsEnabled;
        InitialLogRetentionDays = settings.LogRetentionDays;

        InitialAutoBackupEnabled = settings.AutoBackupEnabled;
        InitialAutoBackupIntervalHours = settings.AutoBackupIntervalHours;

        DefaultVaultPath = SettingsStore.GetDefaultVaultPath();
        DefaultBackupsFolderPath = SettingsStore.GetDefaultBackupsFolderPath();

        InitialVaultPath = string.IsNullOrWhiteSpace(settings.VaultPath)
            ? DefaultVaultPath
            : settings.VaultPath;

        InitialBackupsFolderPath = string.IsNullOrWhiteSpace(settings.BackupsFolderPath)
            ? DefaultBackupsFolderPath
            : settings.BackupsFolderPath;

        VaultPath = InitialVaultPath;
        BackupsFolderPath = InitialBackupsFolderPath;

        TrayEnabled = InitialTrayEnabled;
        MinimizeToTray = InitialMinimizeToTray;
        StartMinimizedToTray = InitialStartMinimizedToTray;
        TrayNotificationsEnabled = InitialTrayNotificationsEnabled;


        CleanLogsEnabled = InitialCleanLogsEnabled;


        AutoBackupEnabled = InitialAutoBackupEnabled;

        // Languages (keep in sync with supported resource cultures).
        Languages.Add(new LanguageOption { CultureName = "ru-RU", DisplayName = Loc.Instance["Russian"] });
        Languages.Add(new LanguageOption { CultureName = "en-US", DisplayName = Loc.Instance["English"] });

        BuildThemes();

        SelectedLanguage = Languages.FirstOrDefault(x =>
            string.Equals(x.CultureName, InitialLanguage, StringComparison.OrdinalIgnoreCase))
            ?? Languages.LastOrDefault();

        BuildTimeZones(settings);
        BuildAutoLockOptions(settings);
        BuildClipboardClearOptions(settings);
        BuildKeepBackupsOptions(settings);
        BuildCloseButtonActionOptions(settings);
        BuildLogRetentionOptions(settings);
        BuildAutoBackupIntervalOptions(settings);
    }

    private void BuildThemes()
    {
        Themes.Clear();

        foreach (var theme in AppThemeCatalog.All)
        {
            Themes.Add(new ThemeOption
            {
                Id = theme.Id,
                DisplayName = Loc.Instance[theme.DisplayNameKey]
            });
        }

        SelectedTheme = Themes.FirstOrDefault(x =>
            string.Equals(x.Id, InitialThemeId, StringComparison.OrdinalIgnoreCase))
            ?? Themes.FirstOrDefault(x => string.Equals(x.Id, AppThemeCatalog.StandardThemeId, StringComparison.OrdinalIgnoreCase))
            ?? Themes.FirstOrDefault();
    }

    private void BuildCloseButtonActionOptions(AppSettings settings)
    {
        CloseButtonActionOptions.Clear();

        CloseButtonActionOptions.Add(new CloseButtonActionOption
        {
            Action = CloseButtonAction.Exit,
            DisplayName = Loc.Instance["CloseButtonActionExit"]
        });

        CloseButtonActionOptions.Add(new CloseButtonActionOption
        {
            Action = CloseButtonAction.MinimizeToTray,
            DisplayName = Loc.Instance["CloseButtonActionMinimize"]
        });

        SelectedCloseButtonAction = CloseButtonActionOptions.FirstOrDefault(x => x.Action == settings.CloseButtonAction)
                                   ?? CloseButtonActionOptions.FirstOrDefault();
    }


    private void BuildLogRetentionOptions(AppSettings settings)
    {
        LogRetentionOptions.Clear();

        // Supported values only.
        var days = new[] { 7, 14, 30, 90, 180, 365 };
        foreach (var d in days)
        {
            LogRetentionOptions.Add(new LogRetentionOption
            {
                Days = d,
                DisplayName = d.ToString()
            });
        }

        SelectedLogRetention = LogRetentionOptions.FirstOrDefault(x => x.Days == settings.LogRetentionDays)
                               ?? LogRetentionOptions.FirstOrDefault(x => x.Days == 30)
                               ?? LogRetentionOptions.FirstOrDefault();
    }





    private void BuildAutoBackupIntervalOptions(AppSettings settings)
    {
        AutoBackupIntervalOptions.Clear();

        var hours = new[] { 1, 6, 12, 24 };
        foreach (var h in hours)
        {
            AutoBackupIntervalOptions.Add(new AutoBackupIntervalOption
            {
                Hours = h,
                DisplayName = string.Format(Loc.Instance["AutoBackupIntervalHoursFmt"], h)
            });
        }

        SelectedAutoBackupInterval = AutoBackupIntervalOptions.FirstOrDefault(x => x.Hours == settings.AutoBackupIntervalHours)
                                   ?? AutoBackupIntervalOptions.FirstOrDefault(x => x.Hours == 24)
                                   ?? AutoBackupIntervalOptions.FirstOrDefault();
    }
    private void BuildAutoLockOptions(AppSettings settings)
    {
        AutoLockOptions.Clear();

        AutoLockOptions.Add(new AutoLockOption { Minutes = 0, DisplayName = Loc.Instance["AutoLockOff"] });
        // Allow quick auto-lock option (1 minute).
        AutoLockOptions.Add(new AutoLockOption { Minutes = 1, DisplayName = string.Format(Loc.Instance["AutoLockMinutesFmt"], 1) });
        AutoLockOptions.Add(new AutoLockOption { Minutes = 5, DisplayName = string.Format(Loc.Instance["AutoLockMinutesFmt"], 5) });
        AutoLockOptions.Add(new AutoLockOption { Minutes = 10, DisplayName = string.Format(Loc.Instance["AutoLockMinutesFmt"], 10) });
        AutoLockOptions.Add(new AutoLockOption { Minutes = 30, DisplayName = string.Format(Loc.Instance["AutoLockMinutesFmt"], 30) });
        AutoLockOptions.Add(new AutoLockOption { Minutes = 60, DisplayName = string.Format(Loc.Instance["AutoLockMinutesFmt"], 60) });

        SelectedAutoLock = AutoLockOptions.FirstOrDefault(x => x.Minutes == settings.AutoLockMinutes)
                           ?? AutoLockOptions.FirstOrDefault();
    }


    private void BuildClipboardClearOptions(AppSettings settings)
    {
        ClipboardClearOptions.Clear();

        ClipboardClearOptions.Add(new ClipboardClearOption { Seconds = 0, DisplayName = Loc.Instance["ClipboardClearOff"] });
        ClipboardClearOptions.Add(new ClipboardClearOption { Seconds = 10, DisplayName = string.Format(Loc.Instance["ClipboardClearSecondsFmt"], 10) });
        ClipboardClearOptions.Add(new ClipboardClearOption { Seconds = 30, DisplayName = string.Format(Loc.Instance["ClipboardClearSecondsFmt"], 30) });
        ClipboardClearOptions.Add(new ClipboardClearOption { Seconds = 60, DisplayName = string.Format(Loc.Instance["ClipboardClearSecondsFmt"], 60) });

        SelectedClipboardClear = ClipboardClearOptions.FirstOrDefault(x => x.Seconds == settings.ClipboardClearSeconds)
                                 ?? ClipboardClearOptions.FirstOrDefault();
    }

    private void BuildKeepBackupsOptions(AppSettings settings)
    {
        KeepBackupsOptions.Clear();

        KeepBackupsOptions.Add(new KeepBackupsOption { Count = 0, DisplayName = Loc.Instance["KeepBackupsOff"] });
        KeepBackupsOptions.Add(new KeepBackupsOption { Count = 5, DisplayName = string.Format(Loc.Instance["KeepBackupsCountFmt"], 5) });
        KeepBackupsOptions.Add(new KeepBackupsOption { Count = 10, DisplayName = string.Format(Loc.Instance["KeepBackupsCountFmt"], 10) });
        KeepBackupsOptions.Add(new KeepBackupsOption { Count = 20, DisplayName = string.Format(Loc.Instance["KeepBackupsCountFmt"], 20) });
        KeepBackupsOptions.Add(new KeepBackupsOption { Count = 50, DisplayName = string.Format(Loc.Instance["KeepBackupsCountFmt"], 50) });

        SelectedKeepBackups = KeepBackupsOptions.FirstOrDefault(x => x.Count == settings.KeepLastBackups)
                              ?? KeepBackupsOptions.FirstOrDefault();
    }

    private void BuildTimeZones(AppSettings settings)
    {
        TimeZones.Clear();

        // System option.
        TimeZones.Add(new TimeZoneOption
        {
            IsSystem = true,
            Id = null,
            DisplayName = $"{Loc.Instance["TimeZoneSystem"]} ({TimeZoneService.GetSystemOffsetLabel()})"
        });

        // Custom time zones.
        var nowUtc = DateTime.UtcNow;

        var list = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new
            {
                tz.Id,
                tz.DisplayName,
                Offset = tz.GetUtcOffset(nowUtc)
            })
            .OrderBy(x => x.Offset)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var it in list)
        {
            TimeZones.Add(new TimeZoneOption
            {
                IsSystem = false,
                Id = it.Id,
                DisplayName = $"({FormatOffset(it.Offset)}) {it.DisplayName}"
            });
        }

        // Selected time zone.
        if (settings.UseSystemTimeZone || string.IsNullOrWhiteSpace(settings.SelectedTimeZoneId))
        {
            SelectedTimeZone = TimeZones.FirstOrDefault(x => x.IsSystem);
            return;
        }

        SelectedTimeZone = TimeZones.FirstOrDefault(x =>
            !x.IsSystem && string.Equals(x.Id, settings.SelectedTimeZoneId, StringComparison.OrdinalIgnoreCase))
            ?? TimeZones.FirstOrDefault(x => x.IsSystem);
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return $"UTC{sign}{offset:hh\\:mm}";
    }
}
