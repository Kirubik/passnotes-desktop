using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;

namespace PassNotes;

public partial class SettingsHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    private readonly MainWindow _owner;
    private readonly SettingsViewModel _vm;
    private readonly PopupToastController _toast = new(1100);
    private bool _vmEventsAttached;

    public event Action<SettingsEditorDraft>? Saved;
    public event Action? Cancelled;
    public event Action<string>? ThemePreviewRequested;

    public bool HasChanges => _vm.HasChanges;
    public bool IsDirty => _vm.HasChanges;
    internal bool ThemePreviewCommitted { get; private set; }

    public SettingsHostedView(MainWindow owner)
    {
        InitializeComponent();

        _owner = owner;
        _vm = new SettingsViewModel(App.Settings);
        DataContext = _vm;
        AttachViewModelEvents();

        Loaded += SettingsHostedView_Loaded;
        Unloaded += SettingsHostedView_Unloaded;
        PreviewKeyDown += SettingsHostedView_PreviewKeyDown;
    }

    private void SettingsHostedView_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModelEvents();

        try
        {
            LanguageBox.Focus();
            Keyboard.Focus(LanguageBox);
        }
        catch
        {
            // ignore
        }
    }

    private void SettingsHostedView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModelEvents();
        try { _toast.CloseCurrent(); } catch { }
    }

    internal void MarkThemePreviewCommitted()
        => ThemePreviewCommitted = true;

    public SettingsEditorDraft CaptureDraft()
    {
        return new SettingsEditorDraft
        {
            SelectedCultureName = _vm.SelectedCultureName,
            SelectedThemeId = _vm.SelectedThemeId,
            UseSystemTimeZone = _vm.UseSystemTimeZone,
            SelectedTimeZoneId = _vm.SelectedTimeZoneId,
            AutoLockMinutes = _vm.AutoLockMinutes,
            ClipboardClearSeconds = _vm.ClipboardClearSeconds,
            KeepLastBackups = _vm.KeepLastBackups,
            AutoBackupEnabled = _vm.AutoBackupEnabled,
            AutoBackupIntervalHours = _vm.AutoBackupIntervalHours,
            VaultPath = _vm.VaultPath ?? string.Empty,
            BackupsFolderPath = _vm.BackupsFolderPath ?? string.Empty,
            TrayEnabled = _vm.TrayEnabled,
            MinimizeToTray = _vm.MinimizeToTray,
            CloseButtonAction = _vm.CloseButtonAction,
            StartMinimizedToTray = _vm.StartMinimizedToTray,
            TrayNotificationsEnabled = _vm.TrayNotificationsEnabled,
            CleanLogsEnabled = _vm.CleanLogsEnabled,
            LogRetentionDays = _vm.LogRetentionDays
        };
    }

    public void ApplyDraft(SettingsEditorDraft d)
    {
        if (d == null)
            return;

        if (!string.IsNullOrWhiteSpace(d.SelectedCultureName))
        {
            var lang = _vm.Languages.FirstOrDefault(x =>
                string.Equals(x.CultureName, d.SelectedCultureName, StringComparison.OrdinalIgnoreCase));
            if (lang != null)
                _vm.SelectedLanguage = lang;
        }

        var theme = _vm.Themes.FirstOrDefault(x =>
            string.Equals(x.Id, d.SelectedThemeId, StringComparison.OrdinalIgnoreCase))
            ?? _vm.Themes.FirstOrDefault(x => string.Equals(x.Id, AppThemeCatalog.StandardThemeId, StringComparison.OrdinalIgnoreCase))
            ?? _vm.Themes.FirstOrDefault();
        if (theme != null)
            _vm.SelectedTheme = theme;

        if (d.UseSystemTimeZone)
        {
            var sys = _vm.TimeZones.FirstOrDefault(x => x.IsSystem);
            if (sys != null)
                _vm.SelectedTimeZone = sys;
        }
        else
        {
            var tz = _vm.TimeZones.FirstOrDefault(x =>
                !x.IsSystem && string.Equals(x.Id, d.SelectedTimeZoneId, StringComparison.OrdinalIgnoreCase));
            _vm.SelectedTimeZone = tz ?? _vm.TimeZones.FirstOrDefault(x => x.IsSystem);
        }

        var al = _vm.AutoLockOptions.FirstOrDefault(x => x.Minutes == d.AutoLockMinutes)
                 ?? _vm.AutoLockOptions.FirstOrDefault();
        if (al != null)
            _vm.SelectedAutoLock = al;

        var cc = _vm.ClipboardClearOptions.FirstOrDefault(x => x.Seconds == d.ClipboardClearSeconds)
                 ?? _vm.ClipboardClearOptions.FirstOrDefault();
        if (cc != null)
            _vm.SelectedClipboardClear = cc;

        var kb = _vm.KeepBackupsOptions.FirstOrDefault(x => x.Count == d.KeepLastBackups)
                 ?? _vm.KeepBackupsOptions.FirstOrDefault();
        if (kb != null)
            _vm.SelectedKeepBackups = kb;

        _vm.AutoBackupEnabled = d.AutoBackupEnabled;
        var ab = _vm.AutoBackupIntervalOptions.FirstOrDefault(x => x.Hours == d.AutoBackupIntervalHours)
                 ?? _vm.AutoBackupIntervalOptions.FirstOrDefault(x => x.Hours == _vm.InitialAutoBackupIntervalHours)
                 ?? _vm.AutoBackupIntervalOptions.FirstOrDefault();
        if (ab != null)
            _vm.SelectedAutoBackupInterval = ab;

        _vm.VaultPath = d.VaultPath ?? string.Empty;
        _vm.BackupsFolderPath = d.BackupsFolderPath ?? string.Empty;

        _vm.TrayEnabled = d.TrayEnabled;
        _vm.MinimizeToTray = d.MinimizeToTray;
        _vm.StartMinimizedToTray = d.StartMinimizedToTray;
        _vm.TrayNotificationsEnabled = d.TrayNotificationsEnabled;
        var ca = _vm.CloseButtonActionOptions.FirstOrDefault(x => x.Action == d.CloseButtonAction)
                 ?? _vm.CloseButtonActionOptions.FirstOrDefault();
        if (ca != null)
            _vm.SelectedCloseButtonAction = ca;

        _vm.CleanLogsEnabled = d.CleanLogsEnabled;
        var lr = _vm.LogRetentionOptions.FirstOrDefault(x => x.Days == d.LogRetentionDays)
                 ?? _vm.LogRetentionOptions.FirstOrDefault(x => x.Days == _vm.InitialLogRetentionDays)
                 ?? _vm.LogRetentionOptions.FirstOrDefault();
        if (lr != null)
            _vm.SelectedLogRetention = lr;
    }

    public bool TryHandleHostedDialogCloseRequest()
    {
        if (!_vm.HasChanges)
        {
            Cancelled?.Invoke();
            return true;
        }

        var res = AppMessageDialogWindow.ShowYesNoCancel(
            _owner,
            Loc.Instance["UnsavedChangesTitle"],
            Loc.Instance["UnsavedChangesMessage"]);

        if (res == MessageBoxResult.Cancel)
            return true;

        if (res == MessageBoxResult.Yes)
            RequestSave();
        else
            Cancelled?.Invoke();

        return true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
        => RequestSave();

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => Cancelled?.Invoke();

    private void ClearClipboardNow_Click(object sender, RoutedEventArgs e)
    {
        ClipboardSecurity.ClearNow();
        ShowToast(ClipboardClearedToastPopup);
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = SettingsStore.GetAppDir();
            Directory.CreateDirectory(dir);
            try { DiagnosticsLog.EnsureExists(); } catch { }

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticsLog.AppendLine("OPEN_LOGS_FOLDER", ex.ToString());
            ShowToast(OpenLogsFolderFailedToastPopup);
        }
    }

    private void ChooseBackupsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var initial = string.IsNullOrWhiteSpace(_vm.BackupsFolderPath)
                ? SettingsStore.GetDefaultBackupsFolderPath()
                : _vm.BackupsFolderPath;

            var selected = PickFolder(initial);
            if (string.IsNullOrWhiteSpace(selected))
                return;

            _vm.BackupsFolderPath = selected;
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }

    private void ChooseVaultFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var current = string.IsNullOrWhiteSpace(_vm.VaultPath)
                ? SettingsStore.GetDefaultVaultPath()
                : _vm.VaultPath;

            var initialDir = string.Empty;
            try
            {
                initialDir = Path.GetDirectoryName(current) ?? string.Empty;
            }
            catch { }

            var dlg = new SaveFileDialog
            {
                Title = Loc.Instance["VaultPathChooseTitle"],
                Filter = Loc.Instance["VaultPathChooseFilter"],
                FileName = Path.GetFileName(current),
                DefaultExt = ".dat",
                AddExtension = true,
                OverwritePrompt = false
            };

            if (!string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir))
                dlg.InitialDirectory = initialDir;

            if (dlg.ShowDialog(_owner) != true)
                return;

            if (string.IsNullOrWhiteSpace(dlg.FileName))
                return;

            _vm.VaultPath = dlg.FileName;
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }

    private void RestoreDefaultStoragePaths_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.RestoreStoragePathsToDefaults();
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
        => _owner.ShowChangePasswordDialogFromHostedSettings();

    private void SettingsHostedView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            HelpWindowManager.ShowOrActivate(_owner, null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            RequestSave();
            e.Handled = true;
        }
    }

    private void RequestSave()
    {
        MarkThemePreviewCommitted();
        Saved?.Invoke(CaptureDraft());
    }

    private void AttachViewModelEvents()
    {
        if (_vmEventsAttached)
            return;

        _vm.PropertyChanged += SettingsViewModel_PropertyChanged;
        _vmEventsAttached = true;
    }

    private void DetachViewModelEvents()
    {
        if (!_vmEventsAttached)
            return;

        _vm.PropertyChanged -= SettingsViewModel_PropertyChanged;
        _vmEventsAttached = false;
    }

    private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName)
            && !string.Equals(e.PropertyName, nameof(SettingsViewModel.SelectedTheme), StringComparison.Ordinal)
            && !string.Equals(e.PropertyName, nameof(SettingsViewModel.SelectedThemeId), StringComparison.Ordinal))
            return;

        ThemePreviewRequested?.Invoke(AppThemeCatalog.NormalizeThemeId(_vm.SelectedThemeId));
    }

    private void ShowToast(Popup popup)
        => _toast.Show(popup);

    private void ShowErrorDialog(string message)
        => AppMessageDialogWindow.ShowOk(_owner, Loc.Instance["Error"], message);

    private string? PickFolder(string initialDirectory)
    {
        try
        {
            var initial = Directory.Exists(initialDirectory)
                ? initialDirectory
                : SettingsStore.GetDefaultBackupsFolderPath();

            var dlg = new OpenFileDialog
            {
                InitialDirectory = initial,
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Выберите папку",
                Filter = "Folder|*.folder|All files|*.*"
            };

            if (dlg.ShowDialog(_owner) != true)
                return null;

            var folder = Path.GetDirectoryName(dlg.FileName);
            return string.IsNullOrWhiteSpace(folder) ? null : folder;
        }
        catch
        {
            return null;
        }
    }
}
