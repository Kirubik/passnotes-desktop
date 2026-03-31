using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PassNotes;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    private LogsCleanupService? _logsCleanupService;
    private AutoBackupService? _autoBackupService;

    private enum VaultOpenFailureAction
    {
        RetryPassword,
        VaultRestored,
        Exit
    }

    private enum VaultOpenFailureKind
    {
        CryptoOrAuth,
        JsonCorruption,
        IoOrAccess,
        Unknown
    }

    private static void WriteLastError(string context, Exception ex)
    {
        try
        {
            var dir = SettingsStore.GetAppDir();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "last_error.txt");
            File.AppendAllText(path,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{context}\n{ex}\n\n");
        }
        catch
        {
            // ignore
        }
    }

    private static VaultOpenFailureKind ClassifyVaultOpenFailure(Exception ex)
    {
        if (ex is CryptographicException)
            return VaultOpenFailureKind.CryptoOrAuth;

        if (ex is JsonException)
            return VaultOpenFailureKind.JsonCorruption;

        // FileNotFoundException derives from IOException.
        if (ex is IOException || ex is UnauthorizedAccessException)
            return VaultOpenFailureKind.IoOrAccess;

        return VaultOpenFailureKind.Unknown;
    }

    private static void ApplySystemFormattingCulture()
    {
        // Ensure WPF uses the OS culture for formatting (dates/numbers)
        // regardless of the UI language selection.
        var culture = CultureInfo.CurrentCulture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ApplySystemFormattingCulture();
        WindowTitleBarThemeManager.Initialize(this);

        // Important: don't auto-exit when the Login dialog closes.
        // We'll control shutdown explicitly.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Global error handler (writes %APPDATA%\PassNotes\last_error.txt)
        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var dir = SettingsStore.GetAppDir();
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "last_error.txt");
                File.WriteAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\nDispatcherUnhandledException\n{args.Exception}\n");
            }
            catch { }

            AppMessageDialogWindow.ShowOk(null, Loc.Instance["Error"], args.Exception.Message);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                var dir = SettingsStore.GetAppDir();
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "last_error.txt");
                File.WriteAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\nUnhandledException\n{args.ExceptionObject}\n");
            }
            catch { }
        };

        Settings = SettingsStore.Load();
        Settings.ThemeId = ThemeRuntimeManager.ApplyTheme(this, Settings.ThemeId);

        // Ensure %APPDATA%\PassNotes\diagnostic.log exists so the user can open it immediately.
        // (The file is otherwise created only on first write.)
        try { DiagnosticsLog.EnsureExists(); } catch { }

        Loc.Instance.SetCulture(Settings.Language);
        TimeZoneService.Initialize(Settings);

        // Track Windows time/timezone changes. When "System" timezone is selected
        // we must refresh displayed times.
        SystemEvents.TimeChanged += SystemEvents_TimeChanged;
        Exit += (_, _) => SystemEvents.TimeChanged -= SystemEvents_TimeChanged;

        // Best-effort logs cleanup (runs at startup + periodic checks, rate-limited internally).
        try
        {
            _logsCleanupService = new LogsCleanupService(TimeSpan.FromMinutes(10));
            _logsCleanupService.Start();
            Exit += (_, _) => { try { _logsCleanupService?.Dispose(); } catch { } };
        }
        catch
        {
            // best-effort
        }

        base.OnStartup(e);

        var vault = new VaultStore(Settings.VaultPath);

        while (true)
        {
            var masterPassword = ShowStartupLoginDialog(vault.Exists);
            if (string.IsNullOrWhiteSpace(masterPassword))
            {
                Shutdown();
                return;
            }

            try
            {
                if (!vault.Exists)
                    vault.Save(masterPassword, new VaultData());

                var data = vault.Load(masterPassword);
                StartMainWindow(vault, masterPassword, data);
                return;
            }
            catch (Exception ex)
            {
                var kind = ClassifyVaultOpenFailure(ex);
                if (kind == VaultOpenFailureKind.Unknown)
                {
                    // Unknown fatal startup problem — log details and exit.
                    WriteLastError("Startup exception", ex);
                    AppMessageDialogWindow.ShowOk(null, Loc.Instance["Error"], ex.Message);
                    Shutdown();
                    return;
                }

                // Known vault open failures: offer retry/restore/exit.
                var action = HandleVaultOpenFailure(vault, kind, ex);
                if (action == VaultOpenFailureAction.Exit)
                {
                    Shutdown();
                    return;
                }

                if (action == VaultOpenFailureAction.VaultRestored)
                {
                    AppMessageDialogWindow.ShowOk(null,
                        Loc.Instance["Recovery"],
                        Loc.Instance["VaultRestoredPleaseLoginAgain"]);
                }

                // Retry password (or retry after restore)
                continue;
            }
        }
    }

    private static string? ShowStartupLoginDialog(bool vaultExists)
    {
        var login = new LoginWindow(vaultExists);
        return login.ShowDialog() == true ? login.MasterPassword : null;
    }
    private void StartMainWindow(VaultStore vault, string masterPassword, VaultData data)
    {
        var main = new MainWindow(vault, masterPassword, data);
        var startMinimizedToTray = Settings.TrayEnabled && Settings.StartMinimizedToTray;

        // Best-effort auto-backup (runs after successful login, rate-limited internally).
        try
        {
            _autoBackupService = new AutoBackupService(TimeSpan.FromMinutes(10));
            _autoBackupService.Start();
            Exit += (_, _) => { try { _autoBackupService?.Dispose(); } catch { } };
        }
        catch
        {
            // best-effort
        }

        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        if (startMinimizedToTray)
        {
            main.PrepareStartupToTray();
            return;
        }

        main.Show();
    }

    private VaultOpenFailureAction HandleVaultOpenFailure(VaultStore vault, VaultOpenFailureKind kind, Exception ex)
    {
        // Best-effort logging of the actual failure cause to help troubleshoot.
        WriteLastError($"Vault open failed: {kind}", ex);

        var details = BuildVaultOpenFailureDetails(kind, ex);
        var template = Loc.Instance["VaultOpenFailedRetryRestoreExitWithDetails"];
        if (template == "VaultOpenFailedRetryRestoreExitWithDetails")
            template = Loc.Instance["VaultOpenFailedRetryRestoreExit"]; // fallback

        var message = template.Contains("{0}") ? string.Format(template, details) : template;

        var result = AppMessageDialogWindow.ShowYesNoCancel(
            null,
            Loc.Instance["Error"],
            message);

        if (result == MessageBoxResult.Yes)
            return VaultOpenFailureAction.RetryPassword;

        if (result == MessageBoxResult.Cancel)
            return VaultOpenFailureAction.Exit;

        // No => restore
        var restored = TryRestoreVaultInteractively(vault);
        return restored ? VaultOpenFailureAction.VaultRestored : VaultOpenFailureAction.RetryPassword;
    }

    private static string BuildVaultOpenFailureDetails(VaultOpenFailureKind kind, Exception ex)
    {
        var key = kind switch
        {
            VaultOpenFailureKind.CryptoOrAuth => "VaultOpenFailedReasonCrypto",
            VaultOpenFailureKind.JsonCorruption => "VaultOpenFailedReasonJson",
            VaultOpenFailureKind.IoOrAccess => "VaultOpenFailedReasonIo",
            _ => "VaultOpenFailedReasonCrypto"
        };

        var baseText = Loc.Instance[key];

        // Provide technical hint for non-crypto failures (file locked, no access, parse error, etc.).
        if (kind != VaultOpenFailureKind.CryptoOrAuth && !string.IsNullOrWhiteSpace(ex.Message))
            return $"{baseText}\n\n{ex.Message}";

        return baseText;
    }

    private bool TryRestoreVaultInteractively(VaultStore vault)
    {
        var vaultPath = vault.Path;
        var prevPath = vaultPath + ".prev";

        // 1) Prefer a one-step local previous version if available.
        if (File.Exists(prevPath))
        {
            var msg = string.Format(Loc.Instance["VaultRestorePrevOrBackup"], prevPath);
            var choice = AppMessageDialogWindow.ShowYesNoCancel(
                null,
                Loc.Instance["Recovery"],
                msg);

            if (choice == MessageBoxResult.Yes)
                return RestoreVaultFromFile(prevPath);

            if (choice == MessageBoxResult.Cancel)
                return false;

            // No => choose a backup file.
        }

        // 2) Let the user pick a backup file.
        try { Directory.CreateDirectory(BackupService.BackupsFolderPath); } catch { }

        var ext = System.IO.Path.GetExtension(vaultPath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".dat";

        var ofd = new OpenFileDialog
        {
            Title = Loc.Instance["SelectBackupFileTitle"],
            InitialDirectory = BackupService.BackupsFolderPath,
            Filter = $"Vault files (*{ext})|*{ext}|All files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
            return RestoreVaultFromFile(ofd.FileName);

        return false;
    }

    private bool RestoreVaultFromFile(string sourceFilePath)
    {
        try
        {
            // Best-effort safety snapshot before restore.
            try { BackupService.CreateBeforeRestoreBackup(); } catch { }

            BackupService.RestoreFromBackup(sourceFilePath);
            return true;
        }
        catch (Exception ex)
        {
            AppMessageDialogWindow.ShowOk(null,
                Loc.Instance["Error"],
                string.Format(Loc.Instance["VaultRestoreFailed"], ex.Message));
            return false;
        }
    }

    private void SystemEvents_TimeChanged(object? sender, EventArgs e)
    {
        if (!Settings.UseSystemTimeZone)
            return;

        // Windows can change time zone or DST rules; clear cache and refresh UI.
        TimeZoneInfo.ClearCachedData();
        Dispatcher.BeginInvoke(() => TimeZoneService.NotifyTimeZoneChanged());
    }
}


