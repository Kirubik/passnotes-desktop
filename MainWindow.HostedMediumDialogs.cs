using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace PassNotes;

public partial class MainWindow
{
    private readonly Stack<IInputElement?> _hostedDialogFocusStack = new();
    private SettingsHostedView? _hostedSettingsView;

    private void ShowHostedSettingsDialog(SettingsEditorDraft? initialDraft = null, bool replaceCurrentModal = false, Action? afterShow = null)
    {
        SettingsEditorDraft? savedDraft = null;
        var application = Application.Current;
        var originalThemeId = application != null
            ? ThemeRuntimeManager.GetActiveThemeId(application)
            : AppThemeCatalog.NormalizeThemeId(App.Settings.ThemeId);
        var view = new SettingsHostedView(this);
        _hostedSettingsView = view;

        void ApplyThemePreview(string requestedThemeId)
        {
            if (application == null)
                return;

            ThemeRuntimeManager.ApplyTheme(application, requestedThemeId);
        }

        void RollbackThemePreviewIfNeeded()
        {
            if (view.ThemePreviewCommitted || application == null)
                return;

            var activeThemeId = ThemeRuntimeManager.GetActiveThemeId(application);
            if (string.Equals(activeThemeId, originalThemeId, StringComparison.OrdinalIgnoreCase))
                return;

            ThemeRuntimeManager.ApplyTheme(application, originalThemeId);
        }

        view.ThemePreviewRequested += ApplyThemePreview;

        if (initialDraft != null)
            view.ApplyDraft(initialDraft);

        view.Saved += draft =>
        {
            savedDraft = draft;
            CloseHostedDialog();
        };
        view.Cancelled += CloseHostedDialog;

        var request = new HostedDialogRequest
        {
            Title = Loc.Instance["Settings"],
            Content = view,
            AfterShown = afterShow,
            Width = 640,
            MinWidth = 640,
            MaxWidth = 640,
            OnClosed = () =>
            {
                view.ThemePreviewRequested -= ApplyThemePreview;
                RollbackThemePreviewIfNeeded();

                if (ReferenceEquals(_hostedSettingsView, view))
                    _hostedSettingsView = null;
            }
        };

        if (replaceCurrentModal)
            ReplaceHostedDialogModal(request);
        else
            ShowHostedDialogModal(request);

        if (savedDraft != null)
            ApplySettingsFromDraft(savedDraft);
    }

    private void ShowHostedPasswordGeneratorDialog()
    {
        var view = new PasswordGeneratorHostedView(this);
        view.Cancelled += CloseHostedDialog;

        ShowHostedDialogModal(new HostedDialogRequest
        {
            Title = Loc.Instance["GeneratorTitle"],
            Content = view,
            Width = 520,
            MinWidth = 520,
            MaxWidth = 520
        });
    }

    private void ApplySettingsFromDraft(SettingsEditorDraft draft)
    {
        var selectedThemeId = AppThemeCatalog.NormalizeThemeId(draft.SelectedThemeId);
        var languageChanged = !string.Equals(App.Settings.Language, draft.SelectedCultureName, StringComparison.OrdinalIgnoreCase);
        var themeChanged = !string.Equals(AppThemeCatalog.NormalizeThemeId(App.Settings.ThemeId), selectedThemeId, StringComparison.OrdinalIgnoreCase);
        var tzChanged = App.Settings.UseSystemTimeZone != draft.UseSystemTimeZone
                        || (!draft.UseSystemTimeZone && !string.Equals(App.Settings.SelectedTimeZoneId, draft.SelectedTimeZoneId, StringComparison.OrdinalIgnoreCase));

        var autoLockChanged = App.Settings.AutoLockMinutes != draft.AutoLockMinutes;
        var clipboardClearChanged = App.Settings.ClipboardClearSeconds != draft.ClipboardClearSeconds;
        var keepLastBackupsChanged = App.Settings.KeepLastBackups != draft.KeepLastBackups;

        var autoBackupEnabledChanged = App.Settings.AutoBackupEnabled != draft.AutoBackupEnabled;
        var autoBackupIntervalChanged = App.Settings.AutoBackupIntervalHours != draft.AutoBackupIntervalHours;

        var trayEnabledChanged = App.Settings.TrayEnabled != draft.TrayEnabled;
        var minimizeToTrayChanged = App.Settings.MinimizeToTray != draft.MinimizeToTray;
        var closeButtonActionChanged = App.Settings.CloseButtonAction != draft.CloseButtonAction;
        var startMinimizedToTrayChanged = App.Settings.StartMinimizedToTray != draft.StartMinimizedToTray;
        var trayNotificationsChanged = App.Settings.TrayNotificationsEnabled != draft.TrayNotificationsEnabled;

        var cleanLogsEnabledChanged = App.Settings.CleanLogsEnabled != draft.CleanLogsEnabled;
        var logRetentionDaysChanged = App.Settings.LogRetentionDays != draft.LogRetentionDays;

        var normalizedVaultPath = string.IsNullOrWhiteSpace(_store.Path)
            ? SettingsStore.GetDefaultVaultPath()
            : _store.Path;

        var normalizedBackupsFolderPath = BackupService.BackupsFolderPath;

        var vaultPathChanged = !string.Equals(normalizedVaultPath, (draft.VaultPath ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        var backupsFolderChanged = !string.Equals(normalizedBackupsFolderPath, (draft.BackupsFolderPath ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        if (!languageChanged && !themeChanged && !tzChanged && !autoLockChanged && !clipboardClearChanged && !keepLastBackupsChanged
            && !autoBackupEnabledChanged && !autoBackupIntervalChanged
            && !trayEnabledChanged && !minimizeToTrayChanged && !closeButtonActionChanged && !startMinimizedToTrayChanged && !trayNotificationsChanged
            && !vaultPathChanged && !backupsFolderChanged
            && !cleanLogsEnabledChanged && !logRetentionDaysChanged)
            return;

        if (languageChanged)
            App.Settings.Language = draft.SelectedCultureName;

        if (themeChanged)
            App.Settings.ThemeId = selectedThemeId;

        if (tzChanged)
        {
            App.Settings.UseSystemTimeZone = draft.UseSystemTimeZone;
            if (!draft.UseSystemTimeZone)
                App.Settings.SelectedTimeZoneId = draft.SelectedTimeZoneId;
        }

        if (autoLockChanged)
            App.Settings.AutoLockMinutes = draft.AutoLockMinutes;

        if (clipboardClearChanged)
            App.Settings.ClipboardClearSeconds = draft.ClipboardClearSeconds;

        if (keepLastBackupsChanged)
            App.Settings.KeepLastBackups = draft.KeepLastBackups;

        if (autoBackupEnabledChanged)
            App.Settings.AutoBackupEnabled = draft.AutoBackupEnabled;
        if (autoBackupIntervalChanged)
            App.Settings.AutoBackupIntervalHours = draft.AutoBackupIntervalHours;

        if (trayEnabledChanged)
            App.Settings.TrayEnabled = draft.TrayEnabled;
        if (minimizeToTrayChanged)
            App.Settings.MinimizeToTray = draft.MinimizeToTray;
        if (closeButtonActionChanged)
            App.Settings.CloseButtonAction = draft.CloseButtonAction;
        if (startMinimizedToTrayChanged)
            App.Settings.StartMinimizedToTray = draft.StartMinimizedToTray;
        if (trayNotificationsChanged)
            App.Settings.TrayNotificationsEnabled = draft.TrayNotificationsEnabled;

        if (cleanLogsEnabledChanged)
            App.Settings.CleanLogsEnabled = draft.CleanLogsEnabled;
        if (logRetentionDaysChanged)
            App.Settings.LogRetentionDays = draft.LogRetentionDays;

        if (backupsFolderChanged)
            App.Settings.BackupsFolderPath = draft.BackupsFolderPath;

        if (vaultPathChanged)
        {
            var newVaultPath = (draft.VaultPath ?? string.Empty).Trim();
            var gate = GateBeforeDangerousAction(DangerousActionKind.VaultSwitch);
            if (gate == DangerousActionDecision.Proceed)
            {
                var applied = TrySwitchVaultPath(normalizedVaultPath, newVaultPath);
                if (applied)
                    App.Settings.VaultPath = newVaultPath;
            }
        }

        SettingsStore.Save(App.Settings);

        if (themeChanged && Application.Current != null)
            ThemeRuntimeManager.ApplyTheme(Application.Current, App.Settings.ThemeId);

        if (languageChanged)
        {
            Loc.Instance.SetCulture(App.Settings.Language);
            RebuildFolderTree();
        }

        if (tzChanged)
        {
            TimeZoneInfo.ClearCachedData();
            TimeZoneService.NotifyTimeZoneChanged();
        }

        if (autoLockChanged)
            UpdateAutoLockMonitoring();

        if (trayEnabledChanged || minimizeToTrayChanged || closeButtonActionChanged || startMinimizedToTrayChanged || trayNotificationsChanged || languageChanged)
            ApplyTraySettings();
    }

    private void CloseAllHostedDialogs()
    {
        while (HostedDialogHost.IsOpen)
            HostedDialogHost.Close();

        _hostedDialogFocusStack.Clear();
    }

    internal void ShowChangePasswordDialogFromHostedSettings()
        => ShowChangePasswordDialog();
}

