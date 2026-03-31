using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using PassNotes.Behaviors;

namespace PassNotes;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly VaultStore _store;
    private string _masterPassword;
    private VaultData _vault;

    // Drafts captured from hosted editors when lock/auto-lock closed them.
    // Keep only the latest snapshot per logical entry so restore never replays stale duplicates.
    private readonly System.Collections.Generic.List<EntryEditorDraft> _pendingEntryDraftsAfterUnlock = new();

    // Settings uses only the latest snapshot because there is a single logical settings editor.
    private readonly System.Collections.Generic.List<SettingsEditorDraft> _pendingSettingsDraftsAfterUnlock = new();
    private bool _entryDraftRestorePromptDismissed;
    private bool _settingsDraftRestorePromptDismissed;
    private bool _inputManagerHookInstalled;

    // Rate-limit dangling attachments metadata self-heal (avoid repeated vault saves).
    private bool _attachmentsMetaSelfHealRanThisSession;

    // -----------------------------
    // Tray
    // -----------------------------
    private TrayService? _tray;
    private bool _trayAllowExit;
    private bool _startupUiInitialized;
    private readonly HostedDialogController _hostedDialogHost = new();
    private readonly System.Collections.Generic.Stack<DispatcherFrame> _hostedDialogModalFrames = new();
    private int _hostedDialogTransitionVersion;

    public HostedDialogController HostedDialogHost => _hostedDialogHost;

    private enum LockReason
    {
        Manual,
        Auto
    }
    public void PrepareStartupToTray()
    {
        ShowInTaskbar = false;
        EnsureStartupUiInitialized();
        ApplyTraySettings();
    }

    private void EnsureStartupUiInitialized()
    {
        if (_startupUiInitialized)
            return;

        _startupUiInitialized = true;

        ApplyRowHeightUiPreferencesBestEffort();
        BuildFolderTree();
        Grid.ItemsSource = _view;
        Grid.SelectionChanged -= Grid_SelectionChanged;
        Grid.SelectionChanged += Grid_SelectionChanged;
        RefreshGrid();
        UpdateActiveContextBindings();
        UpdateFolderActionButtons();
        UpdateFolderHandleArrow();
        AdjustFolderSearchWidth();
        UpdateEntriesGridColumnHeaders();

        // MVP-3B2 (subblock 1.3): run orphan attachments cleanup on startup (rate-limited).
        // Background priority so the UI can render first.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { CleanupOrphanAttachmentsBestEffort("Startup", force: false, toastAnchor: null); } catch { }
        }), DispatcherPriority.Background);
    }
    // -----------------------------
    // Lock / Unlock (quick lock)
    // -----------------------------
    private enum LockLifecycleState
    {
        Locked,
        UnlockRecovery,
        Unlocked
    }

    private LockLifecycleState _lockLifecycleState = LockLifecycleState.Unlocked;

    public bool IsLocked
    {
        get => _lockLifecycleState != LockLifecycleState.Unlocked;
        private set => SetLockLifecycleState(value ? LockLifecycleState.Locked : LockLifecycleState.Unlocked);
    }

    public bool IsUnlocked => _lockLifecycleState == LockLifecycleState.Unlocked;
    public bool IsUnlockRecovery => _lockLifecycleState == LockLifecycleState.UnlockRecovery;
    public bool IsSessionUnlocked => _lockLifecycleState != LockLifecycleState.Locked;
    public bool CanStartLock => _lockLifecycleState != LockLifecycleState.Locked;
    public bool CanStartUnlock => _lockLifecycleState == LockLifecycleState.Locked;

    private bool _pendingVisualUnlockCommit;
    private Action? _pendingHostedUiCommit;

    private void SetLockLifecycleState(LockLifecycleState state)
    {
        if (_lockLifecycleState == state)
            return;

        _lockLifecycleState = state;

        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsUnlockRecovery));
        OnPropertyChanged(nameof(IsSessionUnlocked));
        OnPropertyChanged(nameof(CanStartLock));
        OnPropertyChanged(nameof(CanStartUnlock));

        UpdateAutoLockMonitoring();
        RaiseAllCanExecuteChanged();

        try { _tray?.SetLockEnabled(CanStartLock); } catch { }
    }

    // -----------------------------
    // Auto-lock (idle)
    // -----------------------------
    private System.Windows.Threading.DispatcherTimer? _autoLockTimer;
    // Debounce for entry global search box (avoid filtering on every keystroke).
    private System.Windows.Threading.DispatcherTimer? _entrySearchDebounceTimer;
    private const int EntrySearchDebounceMs = 200;

    private bool _isEntrySearchActive;
    private DateTime _lastUserActivityUtc = DateTime.UtcNow;
    private bool _autoLockHooksInstalled;
    private readonly PopupToastController _copyToast = new(900);

    // Generic info/error toast (used for export/import/backup notifications).
    private readonly PopupToastController _infoToast = new(3000);
    private Popup? _infoToastPopup;
    private TextBlock? _infoToastText;

    private readonly ObservableCollection<FolderNode> _folderTreeRoots = new();
    private FolderNode? _selectedFolderNode;

    // When folder multi-select mode is enabled, the TreeView must not be selectable.
    // We temporarily suppress selection change handling when we programmatically clear selection.
    private bool _suppressFolderTreeSelectionChange;

    // Variant A: folder multi-select mode (checkboxes are shown only when enabled)
    private bool _isFolderMultiSelectMode;
    private int _checkedFoldersCount;

    // Active folder context (what entries list is currently showing). It can remain even when the tree selection is cleared.
    private FolderNode? _activeFolderNode;

    // -----------------------------
    // Bindable UI state (context + counters)
    // -----------------------------

    public sealed class BreadcrumbSegment
    {
        public string Title { get; init; } = "";
        public Guid? FolderId { get; init; }
        public bool IsNoFolder { get; init; }
        public bool IsRoot { get; init; }
        public bool IsSearchResults { get; init; }
        // Used by UI to hide trailing separator.
        public bool IsLast { get; set; }
    }

    public ObservableCollection<BreadcrumbSegment> ActiveContextBreadcrumbs { get; } = new();

    public bool IsEntrySearchActive => _isEntrySearchActive;

    // Breadcrumb area is shown either when a folder context is set or when entry search is active.
    public bool IsBreadcrumbVisible => IsEntrySearchActive || IsContextSet;

    public bool IsContextSet => _activeFolderNode != null;


    // Creating a new entry is allowed only when there is an explicit target context:
    //  - a real folder, or
    //  - the special "Р‘РµР· РїР°РїРєРё" context.
    // When context is not selected (null), the right pane shows 0 entries and creation must be disabled.
    public bool CanCreateEntry => _activeFolderNode?.Kind is FolderNodeKind.Folder or FolderNodeKind.NoFolder;

    private readonly struct FolderNodeIdentity
{
    public FolderNodeKind Kind { get; }
    public Guid Id { get; }
    public FolderNodeIdentity(FolderNodeKind kind, Guid id)
    {
        Kind = kind;
        Id = id;
    }
}

private static FolderNodeIdentity GetFolderNodeIdentity(FolderNode? node)
{
    if (node == null)
        return GetNoFolderIdentity();

    return node.Kind == FolderNodeKind.Folder
        ? new FolderNodeIdentity(FolderNodeKind.Folder, node.Id)
        : new FolderNodeIdentity(node.Kind, Guid.Empty);
}

private static FolderNodeIdentity GetNoFolderIdentity() => new FolderNodeIdentity(FolderNodeKind.NoFolder, Guid.Empty);

private static bool IsSameFolderNodeIdentity(FolderNode? left, FolderNode? right)
{
    if (left == null || right == null)
        return left == null && right == null;

    return left.Kind == right.Kind && left.Id == right.Id;
}

private FolderNode? FindFolderNodeByIdentity(FolderNodeIdentity key)
{
    // Special nodes are unique by Kind. Folder nodes match by Id.
    foreach (var root in _folderTreeRoots)
    {
        var found = FindFolderNodeByIdentityRecursive(root, key);
        if (found != null) return found;
    }
    return null;

    static FolderNode? FindFolderNodeByIdentityRecursive(FolderNode node, FolderNodeIdentity key)
    {
        if (key.Kind == FolderNodeKind.Folder)
        {
            if (node.Kind == FolderNodeKind.Folder && node.Id == key.Id)
                return node;
        }
        else
        {
            if (node.Kind == key.Kind)
                return node;
        }

        foreach (var c in node.Children)
        {
            var f = FindFolderNodeByIdentityRecursive(c, key);
            if (f != null) return f;
        }

        return null;
    }
}

private void NormalizeSelectedFolderNodeToSteadyState()
{
    // Empty-click is allowed to clear only visual selection while keeping the active context.
    if (_selectedFolderNode == null)
        return;

    if (IsSameFolderNodeIdentity(_selectedFolderNode, _activeFolderNode))
        return;

    _selectedFolderNode = _activeFolderNode;
}

string GetNoFolderDisplayName()
    {
        var custom = App.Settings.NoFolderDisplayName;
        return string.IsNullOrWhiteSpace(custom) ? Loc.Instance["FolderNone"] : custom;
    }

    private string GetFavoritesDisplayName()
    {
        var baseName = Loc.Instance["Favorites"];
        try
        {
            var n = (_vault.Entries ?? Array.Empty<VaultEntry>()).Count(x => x.IsFavorite && !x.IsDeleted);
            return n > 0 ? $"{baseName} ({n})" : baseName;
        }
        catch
        {
            return baseName;
        }
    }

    private string GetTrashDisplayName()
    {
        var baseName = Loc.Instance["Trash"];
        try
        {
            var n = (_vault.Entries ?? Array.Empty<VaultEntry>()).Count(x => x.IsDeleted);
            return n > 0 ? $"{baseName} ({n})" : baseName;
        }
        catch
        {
            return baseName;
        }
    }


    private string GetFolderPathDisplayName(Guid? folderId)
    {
        if (folderId == null)
            return GetNoFolderDisplayName();

        var folders = _vault.Folders ?? Array.Empty<VaultFolder>();
        if (folders.Length == 0)
            return GetNoFolderDisplayName();

        var byId = folders.ToDictionary(f => f.Id, f => f);
        if (!byId.TryGetValue(folderId.Value, out var cur))
            return GetNoFolderDisplayName();

        var parts = new System.Collections.Generic.List<string>();
        while (true)
        {
            parts.Add(cur.Name);
            if (cur.ParentId == null) break;
            if (!byId.TryGetValue(cur.ParentId.Value, out cur!)) break;
        }

        parts.Reverse();
        return string.Join(" / ", parts);
    }


    public string ActiveContextTitle
    {
        get
        {
            if (_activeFolderNode == null)
                return Loc.Instance["ContextNotSelected"]; // show "РќРµ РІС‹Р±СЂР°РЅРѕ" instead of "all"

            return _activeFolderNode.Kind switch
            {
                FolderNodeKind.Favorites => GetFavoritesDisplayName(),
                FolderNodeKind.Trash => GetTrashDisplayName(),
                FolderNodeKind.NoFolder => GetNoFolderDisplayName(),
                FolderNodeKind.Folder => _activeFolderNode.Name,
                _ => Loc.Instance["ContextNotSelected"],
            };
        }
    }

    public int DisplayedEntriesCount => _view.Count;

    // Multi-selection mirror for the entries DataGrid.
    // The DataGrid is the source of truth; this collection is synchronized from Grid.SelectedItems.
    public ObservableCollection<VaultEntry> SelectedEntries { get; } = new();

    public int SelectedEntriesCount
    {
        get
        {
            try { return SelectedEntries.Count; }
            catch { return 0; }
        }
    }

    public ICommand LockCommand { get; }
    public ICommand UnlockCommand { get; }

    public ICommand DeleteSelectedEntriesCommand { get; }
    public ICommand SelectAllEntriesCommand { get; }

    public ICommand DeleteSelectedOrCheckedFoldersCommand { get; }

    public ICommand DeleteCheckedFoldersCommand { get; }
    public ICommand ClearCheckedFoldersCommand { get; }

    public ICommand ClearContextCommand { get; }
    public ICommand FocusActiveContextInTreeCommand { get; }
    public ICommand NavigateBreadcrumbCommand { get; }

    // Application-level hotkeys (I1.2)
    public ICommand HotkeyFocusEntrySearchCommand { get; }
    public ICommand HotkeyFocusFolderSearchCommand { get; }
    public ICommand HotkeyAddEntryCommand { get; }
    public ICommand HotkeyAddFolderCommand { get; }
    public ICommand HotkeyToggleLockCommand { get; }
    public ICommand OpenHelpCommand { get; }


    // Hover state (used to disable actions for special nodes like "Р‘РµР· РїР°РїРєРё")
    private bool _isHoveringNoFolder;

    // (FolderRoot node removed from UI; only "Р‘РµР· РїР°РїРєРё" remains special.)

    private ObservableCollection<VaultEntry> _view = new();

    // -----------------------------
    // Drag & Drop (move entries to folder)
    // -----------------------------
    private const string DragEntryIdsFormat = "PassNotes.VaultEntryIds";
    private Point _entriesDragStartPoint;
    private bool _entriesDragArmed;
    private bool _entriesLeftMouseDownOnItem;


    // Windows-like selection behavior:
    // - When multiple rows are selected, clicking a selected row should NOT immediately collapse selection.
    // - If the user starts dragging, keep multi-selection.
    // - If the user releases the mouse without dragging, collapse to the clicked row.
    private bool _entriesDeferSingleSelectOnMouseUp;
    private VaultEntry? _entriesDeferredSingleSelectItem;

// Drag&Drop (entries -> folders): hover highlight + auto-expand
private FolderNode? _dragHoverFolderNode;
private System.Windows.Threading.DispatcherTimer? _dragAutoExpandTimer;
private readonly TimeSpan _dragAutoExpandDelay = TimeSpan.FromMilliseconds(850);


    public bool IsFolderMultiSelectMode
    {
        get => _isFolderMultiSelectMode;
        set
        {
            if (_isFolderMultiSelectMode == value) return;
            _isFolderMultiSelectMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFolderMultiSelectUiVisible));

            // UX requirement: in multi-select mode, folders must not be selectable.
            // Keep the active context on the right intact, but clear any TreeView selection highlight.
            if (_isFolderMultiSelectMode)
            {
                ClearFolderTreeSelection();
                _selectedFolderNode = null;
                UpdateFolderActionButtons();
            }

            // When leaving multi-select mode, clear checkmarks to avoid hidden "pending" selection.
            if (!_isFolderMultiSelectMode)
                ClearCheckedFolders();

            UpdateCheckedFoldersState();
        }
    }

    public int CheckedFoldersCount => _checkedFoldersCount;

    public string CheckedFoldersInfo
        => string.Format(Loc.Instance["FoldersCheckedCount"], CheckedFoldersCount);

    // Time zone selector (System + optional custom)
    private sealed class TimeZoneOption
    {
        public string? Id { get; init; }
        public string DisplayName { get; init; } = "";
        public bool IsSystem { get; init; }

        public override string ToString() => DisplayName;
    }

        private void LogException(string context, Exception ex)
    {
        try
        {
            var dir = SettingsStore.GetAppDir();
            Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "last_error.txt");
            File.WriteAllText(path,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{context}\n{ex}\n");
        }
        catch { /* ignore */ }
    }

    private void SafeUi(string context, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogException(context, ex);
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], ex.Message);
        }
    }

public MainWindow(VaultStore store, string masterPassword, VaultData data)
    {
        InitializeComponent();

        // UI prefs (minimum): window placement + entries row height.
        _uiPrefsSaveDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(UiPrefsSaveDebounceMs)
        };
        _uiPrefsSaveDebounceTimer.Tick += (_, _) =>
        {
            _uiPrefsSaveDebounceTimer.Stop();
            SaveUiPreferencesBestEffort("debounce");
        };

        // Apply stored UI prefs as early as possible to avoid startup flicker.
        SourceInitialized += (_, _) =>
        {
            ApplyWindowUiPreferencesBestEffort();
            _uiPrefsReady = true;
        };



        LocationChanged += (_, _) => MarkUiPrefsDirty();
        SizeChanged += (_, _) => { MarkUiPrefsDirty(); RefreshHostedDialogLayout(); };
        StateChanged += (_, _) => { MarkUiPrefsDirty(); RefreshHostedDialogLayout(); };

        RowHeightSlider.ValueChanged += RowHeightSlider_ValueChanged;


        // Global key handling for MainWindow (Explorer-like):
        // - Enter on selected entry -> open entry
        // - Enter on selected folder -> activate folder context
        // (Ignore typing in TextBox/PasswordBox/ComboBox; no changes for other keys)
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewGotKeyboardFocus += MainWindow_PreviewGotKeyboardFocus;

        // For lightweight bindings (context title, counters, breadcrumbs, etc.)
        DataContext = this;

        // Debounce timer for entry global search.
        _entrySearchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(EntrySearchDebounceMs)
        };
        _entrySearchDebounceTimer.Tick += (_, _) =>
        {
            _entrySearchDebounceTimer.Stop();
            RefreshGrid();
        };

        // Debounce timer for folder search (TreeView filtering).
        _folderSearchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FolderSearchDebounceMs)
        };
        _folderSearchDebounceTimer.Tick += (_, _) =>
        {
            _folderSearchDebounceTimer.Stop();
            ApplyFolderSearchFilter(_pendingFolderSearchText);
        };

        // Auto-lock (idle) monitoring uses input preview handlers on the window.
        InitializeAutoLockMonitoring();

        _store = store;
        _masterPassword = masterPassword;
        _vault = data;

        // Start/stop auto-lock timer according to current settings.
        UpdateAutoLockMonitoring();

        LockCommand = new RelayCommand(Lock, () => CanStartLock);
        UnlockCommand = new RelayCommand(Unlock, () => CanStartUnlock);

        HotkeyFocusEntrySearchCommand = new RelayCommand(FocusEntrySearchBox, () => IsUnlocked);
        HotkeyFocusFolderSearchCommand = new RelayCommand(FocusFolderSearchBox, () => IsUnlocked);

        HotkeyAddEntryCommand = new RelayCommand(() =>
        {
            if (!IsUnlocked) return;
            Add_Click(this, new RoutedEventArgs());
        }, () => IsUnlocked && CanCreateEntry);

        HotkeyAddFolderCommand = new RelayCommand(() =>
        {
            if (!IsUnlocked) return;
            AddFolder_Click(this, new RoutedEventArgs());
        }, () => IsUnlocked && CanCreateFolderHotkey());

        HotkeyToggleLockCommand = new RelayCommand(() =>
        {
            if (CanStartUnlock) Unlock();
            else if (CanStartLock) Lock();
        });

        OpenHelpCommand = new RelayCommand(() =>
        {
            HelpWindowManager.ShowOrActivate(this, null);
        });

        ClearContextCommand = new RelayCommand(ClearActiveContext, () => IsUnlocked && IsContextSet);
        FocusActiveContextInTreeCommand = new RelayCommand(FocusActiveContextInTree, () => IsUnlocked && IsContextSet);
        NavigateBreadcrumbCommand = new RelayCommand(p =>
        {
            if (p is BreadcrumbSegment seg)
                NavigateToBreadcrumb(seg);
        }, _ => IsUnlocked);

        DeleteSelectedEntriesCommand = new RelayCommand(DeleteSelectedEntries, () => IsUnlocked && SelectedEntriesCount > 0);
        SelectAllEntriesCommand = new RelayCommand(SelectAllEntries, () => IsUnlocked && DisplayedEntriesCount > 0);

        DeleteSelectedOrCheckedFoldersCommand = new RelayCommand(DeleteSelectedOrCheckedFolders, () => IsUnlocked);

        DeleteCheckedFoldersCommand = new RelayCommand(DeleteCheckedFolders, () => IsUnlocked && CheckedFoldersCount > 0);
        ClearCheckedFoldersCommand = new RelayCommand(ClearCheckedFolders, () => IsUnlocked && CheckedFoldersCount > 0);

        // Install application-level hotkeys for this window (I1.2).
        HotkeysInstaller.Apply(this, HotkeysCatalog.ForMainWindow, ResolveMainWindowHotkeyCommand);

        // Keep selection-dependent UI state in sync even when selection is modified by behaviors.
        SelectedEntries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedEntriesCount));
            (DeleteSelectedEntriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        };

        FolderTree.ItemsSource = _folderTreeRoots;

        ClipboardSecurity.AutoCleared += ClipboardSecurity_AutoCleared;

        Loaded += (_, _) =>
        {
            EnsureStartupUiInitialized();
            ApplyTraySettings();
        };

        TimeZoneService.TimeZoneChanged += (_, _) =>
        {
            // Ensure UI updates happen on the UI thread.
            Dispatcher.Invoke(() =>
            {
                // Refresh displayed times and the UTC offset label in the Updated column header.
                RefreshDisplayedTimes();
            });
        };

        Loc.Instance.PropertyChanged += (_, _) =>
        {
            UpdateEntriesGridColumnHeaders();
            Title = Loc.Instance["AppTitle"];

            UpdateFolderUiText();
            UpdateFolderActionButtons();
            RefreshGrid();
            UpdateActiveContextBindings();

            // Multi-select strip text depends on localization.
            OnPropertyChanged(nameof(CheckedFoldersInfo));

            // Tray menu and tooltip use localized strings.
            try { _tray?.UpdateTexts(); } catch { }
        };

        // Window events used by tray integration.
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    

    

    // -----------------------------
    // Lock / Unlock
    // -----------------------------

    private void RaiseAllCanExecuteChanged()
    {
        (LockCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (UnlockCommand as RelayCommand)?.RaiseCanExecuteChanged();

        (HotkeyFocusEntrySearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HotkeyFocusFolderSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HotkeyAddEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HotkeyAddFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HotkeyToggleLockCommand as RelayCommand)?.RaiseCanExecuteChanged();

        (ClearContextCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FocusActiveContextInTreeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NavigateBreadcrumbCommand as RelayCommand)?.RaiseCanExecuteChanged();

        (DeleteSelectedEntriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SelectAllEntriesCommand as RelayCommand)?.RaiseCanExecuteChanged();

        (DeleteSelectedOrCheckedFoldersCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCheckedFoldersCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearCheckedFoldersCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Lock()
        => Lock(LockReason.Manual);

    private void Lock(LockReason reason)
    {
        SafeUi("Lock", () =>
        {
            if (!CanStartLock) return;

            _pendingVisualUnlockCommit = false;
            _pendingHostedUiCommit = null;
            IsLocked = true;
            HideAdditionalWindowsForLock();
            ClearSearchUiForLock();
            ClearSensitiveUiForLock();
            ShowTrayNotificationIfHidden(reason == LockReason.Auto
                ? Loc.Instance["TrayNotificationAutoLock"]
                : Loc.Instance["TrayNotificationManualLock"]);
        });
    }

    private void Unlock()
    {
        SafeUi("Unlock", () =>
        {
            if (!CanStartUnlock) return;

            _pendingVisualUnlockCommit = false;
            string? acceptedPassword = null;
            VaultData? reloadedVault = null;
            bool unlockStateAppliedInDialog = false;

            var view = new MasterPasswordPromptHostedView(Loc.Instance["MasterPassword"]);
            view.Accepted += candidate =>
            {
                try
                {
                    reloadedVault = _store.Load(candidate);
                    acceptedPassword = candidate;
                    PrepareUnlockStateAfterSuccessfulLoad(candidate, reloadedVault);
                    unlockStateAppliedInDialog = true;

                    var unlockPromptFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;
                    if (!TryStartUnlockHostedContinuation(unlockPromptFrame))
                        CloseHostedDialog();
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    view.ShowError(Loc.Instance["BadPassword"]);
                }
                catch (Exception ex)
                {
                    LogException("Unlock.LoadVault", ex);
                    AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], ex.Message);
                    view.FocusPassword();
                }
            };
            view.Cancelled += CloseHostedDialog;

            ShowHostedDialogModal(new HostedDialogRequest
            {
                Title = Loc.Instance["UnlockVault"],
                Content = view,
                PrimaryButtonText = Loc.Instance["Ok"],
                PrimaryAction = view.RequestPrimaryAction,
                SecondaryButtonText = Loc.Instance["Cancel"],
                SecondaryAction = view.RequestSecondaryAction,
                Width = 420,
                MinWidth = 380,
                MaxWidth = 460,
                PreferContentFocus = true
            });

            TryCommitPendingVisualUnlockIfReady();

            if (unlockStateAppliedInDialog)
                return;

            if (string.IsNullOrWhiteSpace(acceptedPassword) || reloadedVault == null)
                return;

            PrepareUnlockStateAfterSuccessfulLoad(acceptedPassword, reloadedVault);

            // If auto-lock had to close the entry editor with unsaved edits, offer to restore it now.
            TryRestoreEntryDraftsAfterUnlock();

            // If lock/auto-lock closed Settings with unsaved edits, offer to restore it now.
            TryRestoreSettingsDraftsAfterUnlock();

            TryCommitPendingVisualUnlockIfReady();
        });

    }

    private void PrepareUnlockStateAfterSuccessfulLoad(string acceptedPassword, VaultData reloadedVault)
    {
        _masterPassword = acceptedPassword;

        // IMPORTANT: reload vault from disk after a successful unlock.
        // This keeps the app consistent if the vault file was changed (e.g., restore from backup).
        _vault = reloadedVault;

        // MVP-3B2 (2.3.3): self-heal dangling attachment metadata (best-effort, rate-limited, no UI).
        try { SelfHealDanglingAttachmentMetaBestEffort("Unlock", respectRateLimit: true, stage: "after_load"); } catch { }

        // Cleanup orphaned attachments (best-effort) after reloading.
        try { CleanupOrphanAttachmentsBestEffort("Unlock", force: false, toastAnchor: null); } catch { }

        SetLockLifecycleState(LockLifecycleState.UnlockRecovery);
        _pendingVisualUnlockCommit = true;
    }

    private void TryCommitPendingVisualUnlockIfReady()
    {
        if (!_pendingVisualUnlockCommit)
            return;

        if (HostedDialogHost.IsOpen)
            return;

        CommitPendingVisualUnlock();
    }

    private void QueuePendingHostedUiCommit(Action commit)
    {
        if (commit == null)
            return;

        _pendingHostedUiCommit = _pendingHostedUiCommit == null
            ? commit
            : _pendingHostedUiCommit + commit;
    }

    private void TryCommitPendingHostedUiCommitIfReady()
    {
        if (_pendingHostedUiCommit == null)
            return;

        if (HostedDialogHost.IsOpen)
            return;

        var pending = _pendingHostedUiCommit;
        _pendingHostedUiCommit = null;

        try
        {
            pending.Invoke();
        }
        catch
        {
            // ignore
        }
    }

    private void TryCommitPendingVisualUnlockForWorkingHostedDialog()
    {
        if (!_pendingVisualUnlockCommit)
            return;

        CommitPendingVisualUnlock();
    }

    private void CommitPendingVisualUnlock()
    {
        // Rebuild tree and reveal the application only after unlock is ready to enter working mode.
        try { BuildFolderTree(); } catch { }

        SetLockLifecycleState(LockLifecycleState.Unlocked);

        RefreshGrid();
        UpdateActiveContextBindings();
        UpdateFolderActionButtons();

        _pendingVisualUnlockCommit = false;
    }

    private void RestoreWorkingUnlockedUiAfterServiceLock()
    {
        SetLockLifecycleState(LockLifecycleState.Unlocked);
        RefreshGrid();
        UpdateActiveContextBindings();
        UpdateFolderActionButtons();
    }

    private bool TryStartUnlockHostedContinuation(DispatcherFrame? unlockPromptFrame)
    {
        if (TryRestoreEntryDraftsAfterUnlock(replaceCurrentModal: true, outerFrameToStop: unlockPromptFrame))
            return true;

        if (TryRestoreSettingsDraftsAfterUnlock(replaceCurrentModal: true, outerFrameToStop: unlockPromptFrame))
            return true;

        return false;
    }
    private void CleanupOrphanAttachmentsBestEffort()
        => CleanupOrphanAttachmentsBestEffort("Generic", force: false, toastAnchor: null);

    // -----------------------------
    // MVP-3B2 (2.3.3): Dangling attachment metadata self-heal
    // -----------------------------

    private void SelfHealDanglingAttachmentMetaBestEffort(string trigger, bool respectRateLimit, string? stage)
    {
        // Best-effort: never fail user flows and never show UI.
        try
        {
            try { DiagnosticsLog.EnsureExists(); } catch { }

            if (_vault == null)
            {
                try { DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_SKIP", $"trigger={trigger} reason=no_vault"); } catch { }
                return;
            }

            // Rate-limit only for Unlock-triggered self-heal.
            if (respectRateLimit)
            {
                if (_attachmentsMetaSelfHealRanThisSession)
                {
                    try { DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_SKIP", $"trigger={trigger} reason=session_ran"); } catch { }
                    return;
                }

                try
                {
                    var last = App.Settings?.LastAttachmentsMetaSelfHealUtc;
                    if (last.HasValue)
                    {
                        var delta = DateTime.UtcNow - DateTime.SpecifyKind(last.Value, DateTimeKind.Utc);
                        if (delta < TimeSpan.FromDays(1))
                        {
                            try { DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_SKIP", $"trigger={trigger} reason=rate_limit"); } catch { }
                            return;
                        }
                    }
                }
                catch { }
            }

            var attsTotal = 0;
            var entriesTotal = 0;
            try
            {
                attsTotal = _vault.Attachments?.Length ?? 0;
                entriesTotal = _vault.Entries?.Length ?? 0;
            }
            catch { }

            var stagePart = string.IsNullOrWhiteSpace(stage) ? "" : $" stage={stage}";
            try { DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_BEGIN", $"trigger={trigger}{stagePart} attachments_total={attsTotal} entries_total={entriesTotal}"); } catch { }

            var removed = 0;
            var save = "skip";
            var sampleIds = "";

            try
            {
                // Serialize with VaultIoGate so we don't race with Import/Restore/Save/Backup operations.
                // Non-blocking: if the gate is busy, skip (do not freeze UI) and log the reason.
                var ran = VaultIoGate.TryRun($"MainWindow.AttMetaSelfHeal.{trigger}", () =>
                {
                    var entries = _vault.Entries ?? Array.Empty<VaultEntry>();
                    var existingEntryIds = new HashSet<Guid>(entries.Select(e => e.Id));

                    var atts = _vault.Attachments ?? Array.Empty<VaultAttachment>();
                    var dangling = new List<VaultAttachment>();
                    foreach (var a in atts)
                    {
                        if (!existingEntryIds.Contains(a.EntryId))
                            dangling.Add(a);
                    }

                    removed = dangling.Count;
                    if (removed <= 0)
                    {
                        save = "skip";
                        return;
                    }

                    // Sample IDs only (no paths/names).
                    try
                    {
                        sampleIds = string.Join(",", dangling.Take(10).Select(a => a.Id.ToString("N")));
                    }
                    catch { sampleIds = ""; }

                    // Remove dangling meta and persist.
                    _vault.Attachments = atts.Where(a => existingEntryIds.Contains(a.EntryId)).ToArray();
                    _store.Save(_masterPassword, _vault);
                    save = "ok";
                });

                if (!ran)
                {
                    try { DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_SKIP", $"trigger={trigger} reason=io_gate_busy"); } catch { }
                    return;
                }
            }
            catch (Exception ex)
            {
                var tag = GetAttachmentMetaSelfHealErrorTag(ex);
                var exType = ex.GetType().Name;
                try { DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_ERROR", $"trigger={trigger}{stagePart} tag={tag} ex={exType}"); } catch { }
                try { LogException($"AttMetaSelfHeal.{trigger}", ex); } catch { }
                return;
            }

            try
            {
                var samplePart2 = string.IsNullOrWhiteSpace(sampleIds) ? "" : $" sample_ids={sampleIds}";
                DiagnosticsLog.AppendLine("ATT_META_SELFHEAL_END", $"trigger={trigger}{stagePart} removed_dangling={removed} save={save}{samplePart2}");
            }
            catch { }

            // Rate-limit timestamp: update after attempt (even if nothing was removed), and avoid re-running in this session.
            if (respectRateLimit)
            {
                _attachmentsMetaSelfHealRanThisSession = true;
                try
                {
                    if (App.Settings != null)
                    {
                        App.Settings.LastAttachmentsMetaSelfHealUtc = DateTime.UtcNow;
                        SettingsStore.Save(App.Settings);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static string GetAttachmentMetaSelfHealErrorTag(Exception ex)
    {
        try
        {
            if (ex is UnauthorizedAccessException)
                return "io_access_denied";

            if (ex is DirectoryNotFoundException)
                return "dir_missing";

            if (ex is FileNotFoundException)
                return "file_not_found";

            if (ex is CryptographicException)
                return "crypto_error";

            if (ex is IOException io)
            {
                if (IsSharingOrLockViolation(io))
                    return "io_in_use";
                return "io_error";
            }
        }
        catch { }

        return "unexpected";
    }

    private void CleanupOrphanAttachmentsBestEffort(string trigger, bool force, UIElement? toastAnchor)
    {
        // Orphan cleanup to keep the vault and sidecar folder consistent.
        // Best-effort: never fail user flows.
        try
        {
            // Ensure diagnostic.log exists so it can be opened even if nothing is written yet.
            try { DiagnosticsLog.EnsureExists(); } catch { }

            // Always log whether orphan cleanup was attempted, ran, or was skipped (for diagnostics).
            try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_BEGIN", $"trigger={trigger} force={force}"); } catch { }

            if (_vault == null)
            {
                try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_SKIP", $"trigger={trigger} reason=no_vault"); } catch { }
                return;
            }

            // MVP-3B2 (subblock 1.1): index + diagnostics (logs once per run; no UI changes).
            try { AttachmentsOrphanIndexDiagnostics.LogOnce(_vault); } catch { }

            // If attachments dir is not available, skip (nothing to clean).
            try
            {
                var attsDir = AttachmentsStore.GetAttachmentsDir(_store.Path);
                if (string.IsNullOrWhiteSpace(attsDir))
                {
                    try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_SKIP", $"trigger={trigger} reason=no_attachments_dir"); } catch { }
                    return;
                }
            }
            catch
            {
                try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_SKIP", $"trigger={trigger} reason=attachments_dir_error"); } catch { }
                return;
            }

            // Rate-limit to at most once per 24h unless forced.
            if (!force)
            {
                try
                {
                    var last = App.Settings?.LastOrphanAttachmentsCleanupUtc;
                    if (last.HasValue)
                    {
                        var delta = DateTime.UtcNow - DateTime.SpecifyKind(last.Value, DateTimeKind.Utc);
                        if (delta < TimeSpan.FromDays(1))
                            {
                            try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_SKIP", $"trigger={trigger} reason=rate_limit"); } catch { }
                            return;
                        }
                    }
                }
                catch { }
            }

            var nowUtc = DateTime.UtcNow;
            var res = new AttachmentsOrphanCleanupService.Result();
            var metaChanged = false;
            var phase = "unknown";

            try
            {
                // Serialize with VaultIoGate so we don't race with Import/Restore/Save/Backup operations.
                // Non-blocking: if the gate is busy, skip (do not freeze UI) and log the reason.
                var ran = VaultIoGate.TryRun($"MainWindow.OrphanCleanup.{trigger}", () =>
                {
                    phase = "run";
                    metaChanged = AttachmentsOrphanCleanupService.RunBestEffort(_vault, _store.Path, out res);
                    if (metaChanged)
                    {
                        phase = "save";
                        _store.Save(_masterPassword, _vault);
                    }
                });

                if (!ran)
                {
                    try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_SKIP", $"trigger={trigger} reason=io_gate_busy"); } catch { }
                    return;
                }
            }
            catch (Exception ex)
            {
                var (finalPhase, rootEx) = UnwrapOrphanCleanupExceptionPhase(ex, phase);
                var tag = GetOrphanCleanupErrorTag(rootEx);
                var exType = rootEx.GetType().Name;
                var msg = SanitizeOrphanCleanupMessage(rootEx.Message);

                try
                {
                    var msgPart = string.IsNullOrWhiteSpace(msg) ? "" : $" msg={msg}";
                    DiagnosticsLog.AppendLine(
                        "ATT_ORPHAN_CLEANUP_ERROR",
                        $"trigger={trigger} force={force} phase={finalPhase} tag={tag} ex={exType}{msgPart}");
                }
                catch { }

                // Also write to the centralized error file (best-effort).
                try { LogException($"OrphanCleanup.{trigger}.{finalPhase}", rootEx); } catch { }
                return;
            }


            // Log result even if it did 0 visible actions, so diagnostics are unambiguous.
            try
            {
                DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_END", $"trigger={trigger} metaChanged={metaChanged} {res.ToDiagnosticString()}");
            }
            catch { }

            // Best-effort report (IDs only, no paths). Helps investigate edge-cases without digging through long logs.
            try { WriteOrphanCleanupReportBestEffort(trigger, force, metaChanged, res); } catch { }

            // Rate-limit timestamp: update after attempt (even if nothing was moved).
            try
            {
                if (App.Settings != null)
                {
                    App.Settings.LastOrphanAttachmentsCleanupUtc = nowUtc;
                    SettingsStore.Save(App.Settings);
                }
            }
            catch { }

            // Toast only if there was visible work (quarantine move/purge).
            try
            {
                var moved = res.MovedDanglingMetaBlobs + res.MovedUnreferencedBlobs;
                var purged = res.PurgedFromQuarantine;

                if (moved > 0 || purged > 0)
                {
                    string msg;
                    if (moved > 0 && purged > 0)
                        msg = string.Format(Loc.Instance["OrphanCleanupMovedPurgedFmt"], moved, purged);
                    else if (moved > 0)
                        msg = string.Format(Loc.Instance["OrphanCleanupMovedFmt"], moved);
                    else
                        msg = string.Format(Loc.Instance["OrphanCleanupPurgedFmt"], purged);

                    ShowInfoToast(msg, toastAnchor, 5200);
                }
            }
            catch { }
        }
        catch { }
    }

    private static (string Phase, Exception RootException) UnwrapOrphanCleanupExceptionPhase(Exception ex, string fallbackPhase)
    {
        if (ex is AttachmentsOrphanCleanupService.OrphanCleanupPhaseException pex)
        {
            var phase = string.IsNullOrWhiteSpace(pex.Phase) ? (string.IsNullOrWhiteSpace(fallbackPhase) ? "unknown" : fallbackPhase) : pex.Phase;
            var root = pex.InnerException ?? pex;
            return (phase, root);
        }

        return (string.IsNullOrWhiteSpace(fallbackPhase) ? "unknown" : fallbackPhase, ex);
    }

    private static string GetOrphanCleanupErrorTag(Exception ex)
    {
        try
        {
            if (ex is UnauthorizedAccessException)
                return "io_access_denied";

            if (ex is DirectoryNotFoundException)
                return "dir_missing";

            if (ex is FileNotFoundException)
                return "file_not_found";

            if (ex is CryptographicException)
                return "crypto_error";

            if (ex is IOException io)
            {
                if (IsSharingOrLockViolation(io))
                    return "io_in_use";

                return "io_error";
            }
        }
        catch { }

        return "unexpected";
    }

    private static void WriteOrphanCleanupReportBestEffort(
        string trigger,
        bool force,
        bool metaChanged,
        AttachmentsOrphanCleanupService.Result res)
    {
        try
        {
            var dir = SettingsStore.GetAppDir();
            if (string.IsNullOrWhiteSpace(dir))
                return;

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "orphan_cleanup_report.txt");

            var lines = new System.Collections.Generic.List<string>
            {
                $"timestampUtc={DateTime.UtcNow:O} trigger={trigger} force={force} metaChanged={metaChanged}",
                $"counters: {res.ToDiagnosticString()}"
            };

            static string FormatIds(System.Collections.Generic.IEnumerable<Guid> ids)
            {
                if (ids == null)
                    return "";

                var arr = ids.Where(x => x != Guid.Empty).Select(x => x.ToString("N")).Distinct().ToArray();
                return arr.Length == 0 ? "" : string.Join(",", arr);
            }

            void AddIds(string label, System.Collections.Generic.IEnumerable<Guid> ids)
            {
                var s = FormatIds(ids);
                lines.Add(string.IsNullOrWhiteSpace(s) ? $"{label}=<none>" : $"{label}={s}");
            }

            AddIds($"sample_dangling_meta_ids(max_{AttachmentsOrphanCleanupService.Result.MaxSampleIds})", res.SampleDanglingMetaAttachmentIds);
            AddIds($"sample_missing_blob_ids(max_{AttachmentsOrphanCleanupService.Result.MaxSampleIds})", res.SampleMissingBlobAttachmentIds);
            AddIds($"sample_orphans_blob_no_meta_ids(max_{AttachmentsOrphanCleanupService.Result.MaxSampleIds})", res.SampleOrphansBlobNoMetaIds);
            AddIds($"sample_orphans_meta_no_blob_ids(max_{AttachmentsOrphanCleanupService.Result.MaxSampleIds})", res.SampleOrphansMetaNoBlobIds);
            AddIds($"sample_quarantined_blob_only_ids(max_{AttachmentsOrphanCleanupService.Result.MaxSampleIds})", res.SampleQuarantinedBlobOnlyIds);
            AddIds($"sample_quarantined_meta_only_ids(max_{AttachmentsOrphanCleanupService.Result.MaxSampleIds})", res.SampleQuarantinedMetaOnlyIds);

            File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Best-effort: report failures in diagnostic.log but never impact user flows.
            try { DiagnosticsLog.AppendLine("ATT_ORPHAN_CLEANUP_REPORT_ERROR", $"trigger={trigger} ex={ex.GetType().Name}"); } catch { }
        }
    }

    private static bool IsSharingOrLockViolation(IOException ex)
    {
        try
        {
            // Common Windows error codes:
            // - ERROR_SHARING_VIOLATION (32) -> 0x80070020
            // - ERROR_LOCK_VIOLATION    (33) -> 0x80070021
            var lo = ex.HResult & 0xFFFF;
            if (lo == 32 || lo == 33)
                return true;

            var msg = (ex.Message ?? "").ToLowerInvariant();
            if (msg.Contains("being used by another process")
                || msg.Contains("used by another process")
                || msg.Contains("cannot access the file")
                || msg.Contains("sharing violation")
                || msg.Contains("lock violation")
                || msg.Contains("РёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ РґСЂСѓРіРёРј РїСЂРѕС†РµСЃСЃРѕРј")
                || msg.Contains("РґСЂСѓРіРёРј РїСЂРѕС†РµСЃСЃРѕРј")
                || msg.Contains("Р·Р°РЅСЏС‚")
                || msg.Contains("РѕР±С‰РёР№ РґРѕСЃС‚СѓРї"))
                return true;
        }
        catch { }

        return false;
    }

    private static string SanitizeOrphanCleanupMessage(string? message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
                return "";

            var s = message.Trim();
            // Replace anything path-looking inside quotes.
            s = Regex.Replace(s, @"'([^']*[:\\/][^']*)'", "'<path>'");
            s = Regex.Replace(s, "\"([^\"]*[:\\/][^\"]*)\"", "\"<path>\"");

            // Replace common unquoted Windows/Unix paths.
            s = Regex.Replace(s, @"\b[A-Za-z]:\\\S+", "<path>");
            s = Regex.Replace(s, @"(?<!\w)/\S+", "<path>");

            // Keep diagnostic.log one-line, tab-safe.
            s = s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            s = s.Replace("\"", "").Replace("'", "");
            s = s.Replace('=', ':');
            s = Regex.Replace(s, @"\s+", "_");

            if (s.Length > 80)
                s = s.Substring(0, 80);

            return s;
        }
        catch
        {
            return "";
        }
    }

    // -----------------------------
    // Tray
    // -----------------------------

    private void ApplyTraySettings()
    {
        try
        {
            if (!App.Settings.TrayEnabled)
            {
                // Disable tray.
                if (_tray != null)
                {
                    try { _tray.SetVisible(false); } catch { }
                    try { _tray.Dispose(); } catch { }
                    _tray = null;
                }

                // Ensure the main window is discoverable.
                try { ShowInTaskbar = true; } catch { }
                try
                {
                    if (!IsVisible)
                        Show();
                    if (WindowState == WindowState.Minimized)
                        WindowState = WindowState.Normal;
                }
                catch { }
                return;
            }

            // Enable tray.
            if (_tray == null)
            {
                _tray = new TrayService(
                    onOpen: ShowFromTray,
                    onLock: Lock,
                    onExit: ExitFromTray);
            }

            _tray.NotificationsEnabled = App.Settings.TrayNotificationsEnabled;
            _tray.UpdateTexts();
            _tray.SetLockEnabled(CanStartLock);
            _tray.SetVisible(true);
        }
        catch
        {
            // Tray should never crash the app.
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (!App.Settings.TrayEnabled)
                return;

            if (App.Settings.MinimizeToTray && WindowState == WindowState.Minimized)
            {
                HideToTray();
            }
        }
        catch { }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Flush UI prefs on any close attempt (including "minimize to tray" path).
            SaveUiPreferencesBestEffort("closing");


            if (App.Settings.TrayEnabled
                && App.Settings.CloseButtonAction == CloseButtonAction.MinimizeToTray
                && !_trayAllowExit)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
        }
        catch { }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        ClipboardSecurity.AutoCleared -= ClipboardSecurity_AutoCleared;

        // Ensure tray icon is disposed when the app exits.
        try
        {
            if (_tray != null)
            {
                _tray.SetVisible(false);
                _tray.Dispose();
                _tray = null;
            }
        }
        catch { }
    }

    private void HideToTray()
    {
        try
        {
            if (!App.Settings.TrayEnabled)
                return;

            // Keep as little UI as possible; the app remains running.
            ShowInTaskbar = false;
            Hide();
        }
        catch { }
    }

    private void ShowFromTray()
    {
        try
        {
            EnsureStartupUiInitialized();
            ShowInTaskbar = true;

            // If the window was hidden, Show() brings it back.
            if (!IsVisible)
                Show();

            // Restore state.
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            Topmost = true; // ensure focus
            Topmost = false;
            Focus();
        }
        catch { }
    }

    private bool ShouldUseTrayNotificationChannel()
        => _tray != null
           && App.Settings.TrayEnabled
           && App.Settings.TrayNotificationsEnabled
           && !IsVisible;

    private void ShowTrayNotificationIfHidden(string message)
    {
        if (!ShouldUseTrayNotificationChannel())
            return;

        try { _tray!.ShowInfo(Loc.Instance["AppTitle"], message ?? string.Empty); } catch { }
    }

    private void ClipboardSecurity_AutoCleared(object? sender, EventArgs e)
    {
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowTrayNotificationIfHidden(Loc.Instance["TrayNotificationClipboardAutoCleared"]);
            }), DispatcherPriority.Background);
        }
        catch { }
    }

    private void ExitFromTray()
    {
        try
        {
            _trayAllowExit = true;
            ShowInTaskbar = true;
            try { _tray?.SetVisible(false); } catch { }
            Close();
        }
        catch
        {
            try { Application.Current.Shutdown(); } catch { }
        }
    }


    private bool TryRestoreEntryDraftsAfterUnlock(bool replaceCurrentModal = false, DispatcherFrame? outerFrameToStop = null)
    {
        if (_pendingEntryDraftsAfterUnlock.Count == 0)
            return false;

        if (_entryDraftRestorePromptDismissed)
            return false;

        ShowHostedAppMessageDialog(
            Loc.Instance["RestoreUnsavedEditsTitle"],
            Loc.Instance["RestoreUnsavedEditsMessage"],
            MessageBoxButton.YesNo,
            dialogResult =>
            {
                var promptFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;

                if (dialogResult != MessageBoxResult.Yes)
                {
                    // Keep drafts in memory so higher-level gates (e.g., before import/restore) can handle them later.
                    _entryDraftRestorePromptDismissed = true;
                    if (outerFrameToStop != null)
                        outerFrameToStop.Continue = false;
                    return false;
                }

                var drafts = CoalescePendingEntryDrafts(_pendingEntryDraftsAfterUnlock);
                _pendingEntryDraftsAfterUnlock.Clear();
                _entryDraftRestorePromptDismissed = true;

                if (drafts.Count == 0)
                {
                    if (outerFrameToStop != null)
                        outerFrameToStop.Continue = false;
                    return false;
                }

                if (promptFrame != null)
                    promptFrame.Continue = false;
                if (outerFrameToStop != null)
                    outerFrameToStop.Continue = false;

                RestoreEntryDraftsNow(
                    drafts,
                    replaceFirstHostedDialog: true,
                    afterFirstShow: TryCommitPendingVisualUnlockForWorkingHostedDialog);
                return true;
            },
            replaceCurrentModal: replaceCurrentModal);

        return true;
    }

    private void RestoreEntryDraftsNow(
        System.Collections.Generic.IReadOnlyList<EntryEditorDraft> drafts,
        bool replaceFirstHostedDialog = false,
        Action? afterFirstShow = null)
    {
        if (drafts == null || drafts.Count == 0)
            return;

        var isFirstDialog = true;

        foreach (var d in drafts)
        {
            // Re-open the editor with the captured values. Save behavior stays the same:
            // the user explicitly decides to save or cancel.
            VaultEntry? existing = null;

            try
            {
                if (!d.IsNew)
                    existing = (_vault.Entries ?? Array.Empty<VaultEntry>()).FirstOrDefault(x => x.Id == d.EntryId);
            }
            catch { }

            // Folder location display under "Comment".
            string locName;
            bool missing = false;
            if (d.FolderId == null)
            {
                locName = GetNoFolderDisplayName();
            }
            else
            {
                var node = FindNodeById(d.FolderId.Value);
                if (node == null)
                {
                    locName = Loc.Instance["FolderNotFound"];
                    missing = true;
                }
                else
                {
                    locName = GetFolderPathDisplayName(d.FolderId);
                }
            }

            var restored = ShowHostedEntryDialog(
                existing,
                (locName, d.FolderId, missing),
                d,
                replaceCurrentModal: replaceFirstHostedDialog && isFirstDialog,
                afterShow: isFirstDialog ? afterFirstShow : null);
            isFirstDialog = false;
            if (restored != null)
            {
                try
                {
                    var list = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();
                    var idx = list.FindIndex(x => x.Id == restored.Id);

                    if (idx >= 0)
                    {
                        list[idx] = restored;
                    }
                    else
                    {
                        // If the original entry no longer exists (or it was a new one), add it.
                        restored.FolderId = d.FolderId;
                        list.Add(restored);
                    }

                    _vault.Entries = list.ToArray();
                    _store.Save(_masterPassword, _vault);
                }
                catch (Exception ex)
                {
                    LogException("RestoreUnsavedEdits.Save", ex);
                    AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], ex.Message);
                }

                RefreshGrid();
                UpdateActiveContextBindings();
            }
        }
    }

    private void StorePendingEntryDraftForUnlock(EntryEditorDraft draft)
    {
        if (draft == null)
            return;

        try
        {
            var existingIndex = _pendingEntryDraftsAfterUnlock.FindIndex(x => x != null && x.EntryId == draft.EntryId);
            if (existingIndex >= 0)
                _pendingEntryDraftsAfterUnlock[existingIndex] = draft;
            else
                _pendingEntryDraftsAfterUnlock.Add(draft);

            _entryDraftRestorePromptDismissed = false;
        }
        catch
        {
            try
            {
                _pendingEntryDraftsAfterUnlock.Add(draft);
                _entryDraftRestorePromptDismissed = false;
            }
            catch { }
        }
    }

    private void StorePendingSettingsDraftForUnlock(SettingsEditorDraft draft)
    {
        if (draft == null)
            return;

        try
        {
            _pendingSettingsDraftsAfterUnlock.Clear();
            _pendingSettingsDraftsAfterUnlock.Add(draft);
            _settingsDraftRestorePromptDismissed = false;
        }
        catch { }
    }

    private static System.Collections.Generic.IReadOnlyList<EntryEditorDraft> CoalescePendingEntryDrafts(
        System.Collections.Generic.IEnumerable<EntryEditorDraft> drafts)
    {
        var latestByEntryId = new System.Collections.Generic.Dictionary<Guid, (int order, EntryEditorDraft draft)>();
        var index = 0;

        foreach (var draft in drafts ?? Array.Empty<EntryEditorDraft>())
        {
            if (draft == null)
                continue;

            latestByEntryId[draft.EntryId] = (index, draft);
            index++;
        }

        return latestByEntryId
            .OrderBy(x => x.Value.order)
            .Select(x => x.Value.draft)
            .ToList();
    }




    private bool TryRestoreSettingsDraftsAfterUnlock(bool replaceCurrentModal = false, DispatcherFrame? outerFrameToStop = null)
    {
        if (_pendingSettingsDraftsAfterUnlock.Count == 0)
            return false;

        if (_settingsDraftRestorePromptDismissed)
            return false;

        // Keep only the latest snapshot (settings are global).
        SettingsEditorDraft? d = null;
        try { d = _pendingSettingsDraftsAfterUnlock.LastOrDefault(); } catch { }

        if (d == null)
        {
            _pendingSettingsDraftsAfterUnlock.Clear();
            _settingsDraftRestorePromptDismissed = true;
            return false;
        }

        ShowHostedAppMessageDialog(
            Loc.Instance["RestoreUnsavedSettingsTitle"],
            Loc.Instance["RestoreUnsavedSettingsMessage"],
            MessageBoxButton.YesNo,
            dialogResult =>
            {
                var promptFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;

                if (dialogResult != MessageBoxResult.Yes)
                {
                    // Keep drafts in memory so higher-level gates (e.g., before import/restore) can handle them later.
                    _settingsDraftRestorePromptDismissed = true;
                    if (outerFrameToStop != null)
                        outerFrameToStop.Continue = false;
                    return false;
                }

                _pendingSettingsDraftsAfterUnlock.Clear();
                _settingsDraftRestorePromptDismissed = true;

                if (promptFrame != null)
                    promptFrame.Continue = false;
                if (outerFrameToStop != null)
                    outerFrameToStop.Continue = false;

                RestoreSettingsDraftNow(
                    d,
                    replaceCurrentModal: true,
                    afterShow: TryCommitPendingVisualUnlockForWorkingHostedDialog);
                return true;
            },
            replaceCurrentModal: replaceCurrentModal);

        return true;
    }

    private void RestoreSettingsDraftNow(SettingsEditorDraft d, bool replaceCurrentModal = false, Action? afterShow = null)
    {
        if (d == null)
            return;

        ShowHostedSettingsDialog(d, replaceCurrentModal: replaceCurrentModal, afterShow: afterShow);
    }

    private enum DangerousActionKind
    {
        Import,
        Restore,
        VaultSwitch
    }

    private enum DangerousActionDecision
    {
        Proceed,
        Cancel,
        RestoreFirst
    }

    private string GetDangerousActionName(DangerousActionKind kind)
    {
        return kind switch
        {
            DangerousActionKind.Import => Loc.Instance["DangerousActionImport"],
            DangerousActionKind.Restore => Loc.Instance["DangerousActionRestore"],
            DangerousActionKind.VaultSwitch => Loc.Instance["DangerousActionVaultSwitch"],
            _ => ""
        };
    }

    /// <summary>
    /// Unified gate that must run before operations that can replace/reload vault data (Import/Restore/Vault switch).
    /// This method is intentionally side-effectful: it may close dirty dialogs, restore drafts, or clear drafts.
    /// </summary>
    private DangerousActionDecision GateBeforeDangerousAction(DangerousActionKind kind)
    {
        if (!IsUnlocked)
            return DangerousActionDecision.Cancel;

        var actionName = GetDangerousActionName(kind);

        // 1) Open dirty windows (rare, but handled defensively).
        try
        {
            var dirtyHostedEntry = _hostedEntryView != null && _hostedEntryView.IsDirty
                ? _hostedEntryView
                : null;
            var dirtyHostedSettings = _hostedSettingsView != null && _hostedSettingsView.IsDirty
                ? _hostedSettingsView
                : null;

            if (dirtyHostedEntry != null || dirtyHostedSettings != null)
            {
                var msg = string.Format(Loc.Instance["DangerousActionUnsavedMessageFmt"], actionName);
                var res = AppMessageDialogWindow.ShowYesNoCancel(this, Loc.Instance["DangerousActionUnsavedTitle"], msg);

                if (res == MessageBoxResult.Cancel)
                    return DangerousActionDecision.Cancel;

                if (res == MessageBoxResult.Yes)
                {
                    // Save and continue. If any save cannot be triggered, cancel the operation.
                    if (dirtyHostedEntry != null)
                    {
                        try
                        {
                            if (!dirtyHostedEntry.TrySaveAndCloseForGate())
                                return DangerousActionDecision.Cancel;
                        }
                        catch { return DangerousActionDecision.Cancel; }
                    }

                    if (dirtyHostedSettings != null)
                    {
                        try
                        {
                            dirtyHostedSettings.MarkThemePreviewCommitted();
                            ApplySettingsFromDraft(dirtyHostedSettings.CaptureDraft());
                            CloseHostedDialog();
                        }
                        catch { return DangerousActionDecision.Cancel; }
                    }

                    return DangerousActionDecision.Proceed;
                }

                // Don't save: close dialogs silently and continue.
                if (dirtyHostedEntry != null)
                {
                    try { dirtyHostedEntry.ForceCloseDiscardChangesForGate(); } catch { }
                }

                if (dirtyHostedSettings != null)
                {
                    try { CloseHostedDialog(); } catch { }
                }

                return DangerousActionDecision.Proceed;
            }
        }
        catch { }

        // 2) Drafts after lock/auto-lock (can represent unsaved state even when dialogs are not open).
        if (_pendingEntryDraftsAfterUnlock.Count > 0 || _pendingSettingsDraftsAfterUnlock.Count > 0)
        {
            var msg = string.Format(Loc.Instance["DangerousActionDraftsMessageFmt"], actionName);
            var res = ShowHostedAppMessageDialog(
                Loc.Instance["DangerousActionDraftsTitle"],
                msg,
                MessageBoxButton.YesNoCancel,
                dialogResult =>
                {
                    if (dialogResult != MessageBoxResult.Yes)
                        return false;

                    var promptFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;

                    // Restore drafts now; do NOT execute the operation automatically.
                    var entryDrafts = CoalescePendingEntryDrafts(_pendingEntryDraftsAfterUnlock);
                    SettingsEditorDraft? settingsDraft = null;
                    try { settingsDraft = _pendingSettingsDraftsAfterUnlock.LastOrDefault(); } catch { }

                    _pendingEntryDraftsAfterUnlock.Clear();
                    _pendingSettingsDraftsAfterUnlock.Clear();
                    _entryDraftRestorePromptDismissed = true;
                    _settingsDraftRestorePromptDismissed = true;

                    if (entryDrafts.Count > 0)
                    {
                        try { RestoreEntryDraftsNow(entryDrafts, replaceFirstHostedDialog: true); } catch { }
                        if (promptFrame != null)
                            promptFrame.Continue = false;
                        return true;
                    }

                    if (settingsDraft != null)
                    {
                        try { RestoreSettingsDraftNow(settingsDraft, replaceCurrentModal: true); } catch { }
                        if (promptFrame != null)
                            promptFrame.Continue = false;
                        return true;
                    }

                    return false;
                });

            if (res == MessageBoxResult.Cancel)
                return DangerousActionDecision.Cancel;

            if (res == MessageBoxResult.Yes)
            {
                return DangerousActionDecision.RestoreFirst;
            }

            // Continue without restoration: explicitly discard drafts and proceed.
            try { _pendingEntryDraftsAfterUnlock.Clear(); } catch { }
            try { _pendingSettingsDraftsAfterUnlock.Clear(); } catch { }
            _entryDraftRestorePromptDismissed = true;
            _settingsDraftRestorePromptDismissed = true;

            return DangerousActionDecision.Proceed;
        }

        return DangerousActionDecision.Proceed;
    }



    private bool TryVerifyMasterPassword(string candidate)
    {
        return VaultIoGate.Run("TryVerifyMasterPassword", () =>
        {
            try
            {
                var path = _store.Path;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return false;

                var blob = File.ReadAllBytes(path);
                byte[] plaintext = System.Array.Empty<byte>();
                try
                {
                    plaintext = VaultCrypto.Decrypt(candidate, blob);
                    return true;
                }
                finally
                {
                    if (plaintext.Length > 0)
                        System.Array.Clear(plaintext, 0, plaintext.Length);
                    System.Array.Clear(blob, 0, blob.Length);
                }
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogException("TryVerifyMasterPassword", ex);
                AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], ex.Message);
                return false;
            }
        });
    }

    private void NotifySelectedEntriesChanged()
    {
        OnPropertyChanged(nameof(SelectedEntriesCount));
    }

    private void SyncSelectedEntriesFromGrid()
    {
        try
        {
            SelectedEntries.Clear();

            if (Grid?.SelectedItems != null)
            {
                foreach (var item in Grid.SelectedItems)
                {
                    if (item is VaultEntry entry)
                        SelectedEntries.Add(entry);
                }
            }

            if (SelectedEntries.Count == 0 && Grid?.SelectedItem is VaultEntry single)
                SelectedEntries.Add(single);
        }
        catch
        {
            try { SelectedEntries.Clear(); } catch { }
        }

        NotifySelectedEntriesChanged();
        (DeleteSelectedEntriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RefreshActiveContextHeaders()
    {
        UpdateFolderUiText();
        OnPropertyChanged(nameof(ActiveContextTitle));
    }

    private void RefreshActiveContextUi()
    {
        RefreshActiveContextHeaders();
        RebuildActiveContextBreadcrumbs();
    }

    private void ClearEntriesSelectionSafe(bool clearCurrentItem = false)
    {
        var grid = Grid;
        if (grid != null)
        {
            try { grid.UnselectAll(); } catch { }
            try { grid.UnselectAllCells(); } catch { }

            if (clearCurrentItem)
            {
                try
                {
                    grid.CurrentCell = new DataGridCellInfo();
                    grid.SetCurrentValue(System.Windows.Controls.DataGrid.SelectedItemProperty, null);
                }
                catch { }
            }
        }

        SyncSelectedEntriesFromGrid();
    }

    private void ClearSensitiveUiForLock()
    {
        // Clear any selected entries and wipe the visible list (safer default).
        ClearEntriesSelectionSafe(clearCurrentItem: true);

        try { _view.Clear(); } catch { }

        OnPropertyChanged(nameof(DisplayedEntriesCount));
        RaiseAllCanExecuteChanged();
    }

    private void ClearSearchUiForLock()
    {
        // While locked we must not keep search queries or filtered views.
        // Clear both entry global search and folder search so nothing sensitive remains visible after unlock.
        try { _entrySearchDebounceTimer?.Stop(); } catch { }
        try { SearchBox?.Clear(); } catch { }
        try { SetEntrySearchActive(false); } catch { }

        try { _pendingFolderSearchText = string.Empty; } catch { }
        try { _folderSearchDebounceTimer?.Stop(); } catch { }
        try { FolderSearchBox?.Clear(); } catch { }
        try { ApplyFolderSearchFilter(string.Empty); } catch { }
    }

    private void HideAdditionalWindowsForLock()
    {
        try
        {
            var app = Application.Current;
            if (app == null)
                return;

            // Copy the collection first because Close() mutates Application.Current.Windows.
            var windows = app.Windows.Cast<Window>().ToList();

            if (_hostedSettingsView != null)
            {
                try
                {
                    if (_hostedSettingsView.IsDirty)
                    {
                        StorePendingSettingsDraftForUnlock(_hostedSettingsView.CaptureDraft());
                    }
                }
                catch { }
            }

            if (_hostedEntryView != null)
            {
                try
                {
                    if (_hostedEntryView.IsDirty)
                    {
                        StorePendingEntryDraftForUnlock(_hostedEntryView.CaptureDraft());
                    }
                }
                catch { }

                try { _hostedEntryView.PrepareForLockClose(); } catch { }
            }

            if (HostedDialogHost.IsOpen)
            {
                try { CloseAllHostedDialogs(); } catch { }
            }
            foreach (var w in windows)
            {
                if (w == null || ReferenceEquals(w, this))
                    continue;

                // Remove potentially sensitive windows from the screen.
                try { w.Hide(); } catch { }

                // If the window was shown modally (ShowDialog), hiding it is not enough
                // because the owner will stay disabled. Cancel and close it.
                try { w.DialogResult = false; } catch { }
                try { w.Close(); } catch { }
            }
        }
        catch
        {
            // Ignore any errors while hiding windows during lock.
        }
    }

    private void InitializeAutoLockMonitoring()
    {
        if (_autoLockHooksInstalled)
            return;

        _autoLockHooksInstalled = true;
        _lastUserActivityUtc = DateTime.UtcNow;

        // Track only meaningful events to avoid high-frequency noise (no MouseMove).
        // NOTE: MainWindow receives no input while a modal dialog (ShowDialog) is open.
        // Hook InputManager to count activity in child dialogs as well.
        AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, __) => MarkUserActivity()), true);
        AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, __) => MarkUserActivity()), true);
        AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler((_, __) => MarkUserActivity()), true);
        AddHandler(UIElement.PreviewTouchDownEvent, new EventHandler<TouchEventArgs>((_, __) => MarkUserActivity()), true);

        TryInstallGlobalInputHook();

        Activated += (_, _) => MarkUserActivity();

        _autoLockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _autoLockTimer.Tick += (_, _) => AutoLockTimerTick();

        Closed += (_, _) =>
        {
            try { _autoLockTimer?.Stop(); } catch { }
            TryUninstallGlobalInputHook();
        };
    }

    private void TryInstallGlobalInputHook()
    {
        if (_inputManagerHookInstalled)
            return;

        try
        {
            InputManager.Current.PreProcessInput += InputManager_PreProcessInput;
            _inputManagerHookInstalled = true;
        }
        catch
        {
            // Best-effort only.
        }
    }

    private void TryUninstallGlobalInputHook()
    {
        if (!_inputManagerHookInstalled)
            return;

        try
        {
            InputManager.Current.PreProcessInput -= InputManager_PreProcessInput;
        }
        catch { }

        _inputManagerHookInstalled = false;
    }

    private void InputManager_PreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        try
        {
            var input = e?.StagingItem?.Input;
            if (input is KeyEventArgs)
                MarkUserActivity();
            else if (input is MouseButtonEventArgs)
                MarkUserActivity();
            else if (input is MouseWheelEventArgs)
                MarkUserActivity();
            else if (input is TouchEventArgs)
                MarkUserActivity();
        }
        catch
        {
            // Never allow input hook errors to crash the app.
        }
    }

    private void MarkUserActivity()
    {
        _lastUserActivityUtc = DateTime.UtcNow;
    }

    private void AutoLockTimerTick()
    {
        try
        {
            if (!IsUnlocked)
            {
                UpdateAutoLockMonitoring();
                return;
            }

            var minutes = App.Settings.AutoLockMinutes;
            if (minutes <= 0)
            {
                UpdateAutoLockMonitoring();
                return;
            }

            var idle = DateTime.UtcNow - _lastUserActivityUtc;
            if (idle >= TimeSpan.FromMinutes(minutes))
            {
                Lock(LockReason.Auto);
                MarkUserActivity();
            }
        }
        catch
        {
            // Never allow auto-lock timer errors to crash the app.
        }
    }

    private void UpdateAutoLockMonitoring()
    {
        if (_autoLockTimer == null)
            return;

        var enabled = App.Settings.AutoLockMinutes > 0 && IsUnlocked;

        if (enabled)
        {
            if (!_autoLockTimer.IsEnabled)
            {
                MarkUserActivity();
                _autoLockTimer.Start();
            }
        }
        else
        {
            if (_autoLockTimer.IsEnabled)
                _autoLockTimer.Stop();
        }
    }


    private void RefreshDisplayedTimes()
    {
        UpdateUpdatedColumnHeader();
        Grid.Items.Refresh();
    }

    private void UpdateEntriesGridColumnHeaders()
    {
        if (TitleColumn != null)
            TitleColumn.Header = Loc.Instance["TitleCol"];

        if (FolderPathColumn != null)
            FolderPathColumn.Header = Loc.Instance["FolderCol"];

        if (UsernameColumn != null)
            UsernameColumn.Header = Loc.Instance["UsernameCol"];

        if (UrlColumn != null)
            UrlColumn.Header = Loc.Instance["UrlCol"];

        UpdateUpdatedColumnHeader();
    }

    private bool IsUpdatedColumnVisible()
    {
        return UpdatedUtcColumn != null && UpdatedUtcColumn.Visibility == Visibility.Visible;
    }

    private void SetUpdatedColumnVisible(bool isVisible)
    {
        if (UpdatedUtcColumn == null)
            return;

        UpdatedUtcColumn.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateUpdatedColumnHeader()
    {
        if (UpdatedUtcColumn == null)
            return;

        var baseHeader = Loc.Instance["UpdatedCol"];
        var offset = TimeZoneService.GetCurrentOffsetLabel();

        // Put the tooltip on the header itself (DataGridColumn has no ToolTip property).
        UpdatedUtcColumn.Header = new TextBlock
        {
            Text = $"{baseHeader} ({offset})",
            ToolTip = TimeZoneService.CurrentTimeZone.DisplayName
        };
    }
    // -----------------------------
    // Active context helpers (no "all entries" mode)
    // -----------------------------

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncSelectedEntriesFromGrid();
    }

    private void UpdateActiveContextBindings()
    {
        UpdateActiveContextMarker();
        RebuildActiveContextBreadcrumbs();

        OnPropertyChanged(nameof(IsContextSet));
        OnPropertyChanged(nameof(IsBreadcrumbVisible));
        OnPropertyChanged(nameof(CanCreateEntry));
        OnPropertyChanged(nameof(ActiveContextTitle));
        OnPropertyChanged(nameof(DisplayedEntriesCount));
        OnPropertyChanged(nameof(SelectedEntriesCount));

        (ClearContextCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FocusActiveContextInTreeCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Keep entry actions in sync with the active context (e.g., disable "Add" when context is not selected).
        UpdateEntryActionButtons();
    }

    private void SetEntrySearchActive(bool value)
    {
        if (_isEntrySearchActive == value)
            return;

        _isEntrySearchActive = value;

        OnPropertyChanged(nameof(IsEntrySearchActive));
        OnPropertyChanged(nameof(IsBreadcrumbVisible));

        // Breadcrumbs change depending on search state.
        try { RebuildActiveContextBreadcrumbs(); } catch { }
    }

    private void UpdateActiveContextMarker()
    {
        // Marker is independent from TreeView selection.
        // Clear existing flags and set the active one.
        foreach (var r in _folderTreeRoots)
            ClearActiveFlagRecursive(r);

        if (_activeFolderNode == null)
            return;

        var node = FindNodeByIdentity(_activeFolderNode);
        if (node != null)
            node.IsActiveContext = true;
    }

    private static void ClearActiveFlagRecursive(FolderNode node)
    {
        if (node.IsActiveContext)
            node.IsActiveContext = false;

        foreach (var c in node.Children)
            ClearActiveFlagRecursive(c);
    }

    private void RebuildActiveContextBreadcrumbs()
    {
        ActiveContextBreadcrumbs.Clear();

        // When entry search is active, breadcrumbs must clearly indicate that
        // the right pane shows global search results (not a folder view).
        if (IsEntrySearchActive)
        {
            ActiveContextBreadcrumbs.Add(new BreadcrumbSegment
            {
                Title = Loc.Instance["SearchResults"],
                IsSearchResults = true,
                IsLast = true
            });
            return;
        }

        if (_activeFolderNode == null)
            return;

        // Build into a temp list first so we can mark last segment
        // (and avoid a trailing "/" in UI).
        var segs = new System.Collections.Generic.List<BreadcrumbSegment>();

        if (_activeFolderNode.Kind == FolderNodeKind.Favorites)
        {
            segs.Add(new BreadcrumbSegment
            {
                Title = GetFavoritesDisplayName(),
                IsRoot = true
            });
        }
        else if (_activeFolderNode.Kind == FolderNodeKind.Trash)
        {
            segs.Add(new BreadcrumbSegment
            {
                Title = GetTrashDisplayName(),
                IsRoot = true
            });
        }
        else if (_activeFolderNode.Kind == FolderNodeKind.NoFolder)
        {
            segs.Add(new BreadcrumbSegment
            {
                Title = GetNoFolderDisplayName(),
                IsNoFolder = true
            });
        }
        else if (_activeFolderNode.Kind == FolderNodeKind.Folder)
        {
            // IMPORTANT UX: do NOT show synthetic root like "РџР°РїРєРё".
            // Breadcrumbs must start from the actual top-level folder (e.g. "Р Р°Р±РѕС‚Р°").
            var chain = GetAncestorChain(_activeFolderNode);
            foreach (var n in chain)
            {
                segs.Add(new BreadcrumbSegment
                {
                    Title = n.Name,
                    FolderId = n.Id
                });
            }

            segs.Add(new BreadcrumbSegment
            {
                Title = _activeFolderNode.Name,
                FolderId = _activeFolderNode.Id
            });
        }

        if (segs.Count == 0)
            return;

        segs[^1].IsLast = true;

        foreach (var s in segs)
            ActiveContextBreadcrumbs.Add(s);
    }

    private void ClearActiveContext()
    {
        // IMPORTANT: do NOT fall back to "all entries".
        _activeFolderNode = null;

        // Clear selection highlight (selection and context are separate, but a cleared context is clearer with no selection).
        _selectedFolderNode = null;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var selectedTvi = FindSelectedTreeViewItem(FolderTree);
                if (selectedTvi != null)
                    selectedTvi.IsSelected = false;
            }
            catch { }
        }));

        UpdateActiveContextBindings();
        UpdateFolderActionButtons();
        UpdateEntryActionButtons();
        RefreshGrid();
    }

    private TreeViewItem? FindSelectedTreeViewItem(ItemsControl container)
    {
        foreach (var item in container.Items)
        {
            if (container.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem tvi)
                continue;

            if (tvi.IsSelected)
                return tvi;

            var child = FindSelectedTreeViewItem(tvi);
            if (child != null)
                return child;
        }

        return null;
    }

    private void FocusActiveContextInTree()
    {
        if (_activeFolderNode == null)
            return;

        // Scroll to the active context node and visually select it.
        // IMPORTANT: this does not change active context (it is already the same node),
        // but makes it obvious in the tree what folder the right side is showing.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var node = FindNodeByIdentity(_activeFolderNode);
                if (node == null)
                    return;

                ExpandParents(node);
                var item = FindTreeViewItem(FolderTree, node);
                if (item != null)
                {
                    item.BringIntoView();
                    item.IsSelected = true;
                    // Do NOT steal keyboard focus from search/grid.
                    // Selection highlight is enough to indicate the active context in the tree.
                }
            }
            catch { }
        }));
    }

    private void NavigateToBreadcrumb(BreadcrumbSegment seg)
    {
        if (seg.IsSearchResults)
        {
            SearchBox?.Focus();
            return;
        }

        // Root segment is informational; clicking it just focuses the tree.
        if (seg.IsRoot)
        {
            FocusActiveContextInTree();
            return;
        }

        if (seg.IsNoFolder)
        {
            var noFolder = _folderTreeRoots.FirstOrDefault(x => x.Kind == FolderNodeKind.NoFolder);
            _activeFolderNode = noFolder;
            UpdateActiveContextBindings();
            RefreshGrid();
            FocusActiveContextInTree();
            return;
        }

        if (seg.FolderId is Guid id)
        {
            var node = FindNodeById(id);
            _activeFolderNode = node;
            UpdateActiveContextBindings();
            RefreshGrid();
            FocusActiveContextInTree();
        }
    }

    // Rebuild the folder tree after changes that affect node captions (e.g., language).
    private void RebuildFolderTree()
    {
        BuildFolderTree();
    }

    

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce to keep typing responsive for large vaults.
        if (_entrySearchDebounceTimer == null)
        {
            RefreshGrid();
            return;
        }

        _entrySearchDebounceTimer.Stop();
        _entrySearchDebounceTimer.Start();
    }

    private void ClearSearchBox_Click(object sender, RoutedEventArgs e)
    {
        // Convenience: clear search text without changing folder context.
        if (SearchBox == null)
            return;

        SearchBox.Clear();
        SearchBox.Focus();
        e.Handled = true;
    }

    private void EntriesMenuPopup_Closed(object sender, EventArgs e)
    {
        // When popup closes (click outside / Esc), uncheck the toggle button.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;
    }


    private void LockFromMenu_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup and lock immediately.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        Lock();
        e.Handled = true;
    }

    private void UnlockFromMenu_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup and ask for master password to unlock.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        Unlock();
        e.Handled = true;
    }

    private void CreateBackupNow_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before doing work.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        if (!IsUnlocked)
            return;

        // MVP-3B2 (2.3.3): best-effort self-heal dangling attachment metadata before backup.
        // No rate-limit here: user explicitly requested a backup action.
        try { SelfHealDanglingAttachmentMetaBestEffort("BackupNow", respectRateLimit: false, stage: "before_backup"); } catch { }

        try
        {
            var backupPath = BackupService.CreateBackupNowValidated(_masterPassword);
            var fileName = Path.GetFileName(backupPath);
            AppMessageDialogWindow.ShowOk(this,
                Loc.Instance["BackupTitle"],
                string.Format(Loc.Instance["BackupCreatedFmt"], fileName));
        }
        catch (BackupCreateFailedException ex)
        {
            string msg;
            if (string.Equals(ex.Tag, "missing_attachments_folder", StringComparison.OrdinalIgnoreCase))
            {
                msg = Loc.Instance["BackupCreateMissingAttachmentsFolder"];
            }
            else if (string.Equals(ex.Tag, "missing_attachment_blob", StringComparison.OrdinalIgnoreCase))
            {
                var name = string.IsNullOrWhiteSpace(ex.AttachmentDisplayName) ? "?" : ex.AttachmentDisplayName;
                msg = string.Format(Loc.Instance["BackupCreateMissingAttachmentBlobFmt"], name);
            }
            else
            {
                msg = string.Format(Loc.Instance["BackupCreateAttachmentsCopyFailedFmt"], ex.Tag);
            }

            AppMessageDialogWindow.ShowOk(this,
                Loc.Instance["BackupTitle"],
                msg);
        }
        catch (FileNotFoundException)
        {
            AppMessageDialogWindow.ShowOk(this,
                Loc.Instance["BackupTitle"],
                Loc.Instance["BackupSourceMissing"]);
        }
        catch (Exception ex)
        {
            AppMessageDialogWindow.ShowOk(this,
                Loc.Instance["BackupTitle"],
                string.Format(Loc.Instance["BackupCreateErrorFmt"], ex.Message));
        }

        e.Handled = true;
    }

    private void RestoreFromBackup_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before doing work.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        if (!IsUnlocked)
            return;

        var dlg = new OpenFileDialog
        {
            Title = Loc.Instance["BackupRestoreTitle"],
            Filter = Loc.Instance["BackupRestoreFilter"],
            CheckFileExists = true,
            Multiselect = false
        };

        try
        {
            if (Directory.Exists(BackupService.BackupsFolderPath))
                dlg.InitialDirectory = BackupService.BackupsFolderPath;
        }
        catch { }

        if (dlg.ShowDialog(this) != true)
            return;

        var confirm = AppMessageDialogWindow.ShowYesNo(this,
            Loc.Instance["BackupTitle"],
            Loc.Instance["BackupRestoreConfirm"]);

        if (confirm != MessageBoxResult.Yes)
            return;

        var backupPath = dlg.FileName;
        var toastAnchor = sender as UIElement;
        var wasUnlocked = IsUnlocked;

        // Pre-validate that the backup can be opened with the current master password
        // and contains all required attachment blobs (if any).
        try
        {
	            try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_BEGIN", $"backup={Path.GetFileName(backupPath)}"); } catch { }

            var previewStore = new VaultStore(backupPath);
            var preview = previewStore.Load(_masterPassword);

	            if (preview.Attachments is { Length: > 0 })
	            {
	                // Validate only attachments that belong to existing entries in the backup.
	                // Dangling attachment metadata (EntryId not found in Entries) must not block restore.
	                var entries = preview.Entries ?? Array.Empty<VaultEntry>();
	                var existingEntryIds = new HashSet<Guid>(entries.Select(e => e.Id));
	                var required = new List<VaultAttachment>();
	                var skippedDangling = 0;
	                foreach (var a in preview.Attachments)
	                {
	                    if (existingEntryIds.Contains(a.EntryId))
	                        required.Add(a);
	                    else
	                        skippedDangling++;
	                }

	                var attDir = backupPath + ".attachments";
	                if (required.Count > 0 && !Directory.Exists(attDir))
	                {
	                    try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_END", $"backup={Path.GetFileName(backupPath)} required={required.Count} missing_required={required.Count} skipped_dangling={skippedDangling}"); } catch { }
	                    try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_FAIL", $"backup={Path.GetFileName(backupPath)} missing_required={required.Count}"); } catch { }
	                    ShowInfoToast(Loc.Instance["BackupRestoreMissingAttachmentsFolder"], toastAnchor, 6500);
	                    return;
	                }

	                foreach (var a in required)
	                {
	                    var blobPath = System.IO.Path.Combine(attDir, $"{a.Id:N}.pna");
	                    if (!File.Exists(blobPath))
	                    {
	                        var name = string.IsNullOrWhiteSpace(a.FileName) ? $"{a.Id:N}.pna" : a.FileName;
	                        try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_END", $"backup={Path.GetFileName(backupPath)} required={required.Count} missing_required=1 skipped_dangling={skippedDangling}"); } catch { }
	                        try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_FAIL", $"backup={Path.GetFileName(backupPath)} missing_required=1"); } catch { }
	                        ShowInfoToast(string.Format(Loc.Instance["BackupRestoreMissingAttachmentBlobFmt"], name), toastAnchor, 6500);
	                        return;
	                    }
	                }

	                try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_END", $"backup={Path.GetFileName(backupPath)} required={required.Count} missing_required=0 skipped_dangling={skippedDangling}"); } catch { }
	            }
	            else
	            {
	                try { DiagnosticsLog.AppendLine("RESTORE_ATTACHMENTS_VERIFY_END", $"backup={Path.GetFileName(backupPath)} required=0 missing_required=0 skipped_dangling=0"); } catch { }
	            }
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            ShowInfoToast(Loc.Instance["BackupRestorePasswordMismatchToast"], toastAnchor, 6500);
            return;
        }
        catch (Exception ex)
        {
            ShowInfoToast(string.Format(Loc.Instance["BackupRestoreErrorFmt"], ex.Message), toastAnchor, 6500);
            return;
        }

        
// Preserve current context identity (best-effort) so we can restore it after reload.
var activeKey = GetFolderNodeIdentity(_activeFolderNode);
var selectedKey = GetFolderNodeIdentity(_selectedFolderNode);


        // Safer default: lock UI and hide all additional windows before replacing the vault file.
Lock();

// While locked during import, avoid showing stale folder names from the previous vault (best-effort).
try { _folderTreeRoots.Clear(); } catch { }


        try
        {
            var before = BackupService.CreateBeforeRestoreBackup();
            BackupService.RestoreFromBackupTransactional(backupPath);

            // Try to reload vault right away (no app restart).
            try
            {
                
_vault = _store.Load(_masterPassword);
// MVP-3B2 (2.3.3): best-effort self-heal dangling attachment metadata in the restored vault.
try { SelfHealDanglingAttachmentMetaBestEffort("RestoreBackup", respectRateLimit: false, stage: "after_load"); } catch { }
try { CleanupOrphanAttachmentsBestEffort("RestoreBackup", force: true, toastAnchor: toastAnchor); } catch { }

// Rebuild folder tree from the imported vault.
BuildFolderTree();

// Best-effort restore the previously active/selected context (by identity, not by instance).
_activeFolderNode = FindFolderNodeByIdentity(activeKey) ?? FindFolderNodeByIdentity(GetNoFolderIdentity());
_selectedFolderNode = FindFolderNodeByIdentity(selectedKey) ?? _activeFolderNode;
NormalizeSelectedFolderNodeToSteadyState();
SelectFolderNodeInTree(_selectedFolderNode);
UpdateActiveContextBindings();

                if (wasUnlocked)
                    RestoreWorkingUnlockedUiAfterServiceLock();

                var okMsg = string.IsNullOrWhiteSpace(before)
                    ? Loc.Instance["BackupRestoreSuccessToast"]
                    : string.Format(Loc.Instance["BackupRestoreSuccessToastWithBeforeFmt"], Path.GetFileName(before));
                ShowInfoToast(okMsg, toastAnchor, 5200);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Restored vault uses a different password; keep the app locked.
                ShowInfoToast(Loc.Instance["BackupRestorePasswordMismatchToast"], toastAnchor, 6500);
            }
        }
        catch (Exception ex)
        {
            // If restore fails, return to the previous state to avoid trapping the user in Locked.
            if (wasUnlocked)
                RestoreWorkingUnlockedUiAfterServiceLock();

            ShowInfoToast(string.Format(Loc.Instance["BackupRestoreErrorFmt"], ex.Message), toastAnchor, 6500);
        }

        e.Handled = true;
    }

    private void OpenBackupsFolder_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before doing work.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        if (!IsUnlocked)
            return;

        try
        {
            BackupService.OpenBackupsFolder();
        }
        catch (Exception ex)
        {
            var toastAnchor = sender as UIElement;
            ShowInfoToast(string.Format(Loc.Instance["BackupOpenFolderErrorFmt"], ex.Message), toastAnchor, 5200);
        }

        e.Handled = true;
    }

    
    private void ImportVault_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before doing work.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        if (!IsUnlocked)
            return;

        var dlg = new OpenFileDialog
        {
            Title = Loc.Instance["ImportVaultTitle"],
            Filter = Loc.Instance["ImportVaultFilter"],
            CheckFileExists = true,
            Multiselect = false
        };

        try
        {
            var last = (App.Settings.LastImportDirectory ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(last) && Directory.Exists(last))
                dlg.InitialDirectory = last;
            else
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        catch { }

        if (dlg.ShowDialog(this) != true)
            return;

        var exportPath = dlg.FileName;

        var toastAnchor = sender as UIElement;

        // Remember last used import directory (best-effort).
        try
        {
            var dir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                App.Settings.LastImportDirectory = dir;
                SettingsStore.Save(App.Settings);
            }
        }
        catch { }

        // Extract vault from .pnexp to a temporary file.
        var tempDir = Path.Combine(Path.GetTempPath(), "PassNotes", "Import", Guid.NewGuid().ToString("N"));
        var extractedVaultPath = Path.Combine(tempDir, "vault.pnvault");
        var extractedAttachmentsDir = Path.Combine(tempDir, "attachments");

        try
        {
            Directory.CreateDirectory(tempDir);

            
// Read and validate export container.
const int SupportedExportVersion = 3;
var exportMetaVersion = 0;
bool isNewerVersion = false;
string? expectedChecksumSha256 = null;
long? expectedVaultSize = null;

// We always compute the actual checksum of the extracted vault entry.
string actualChecksumSha256 = "";
long actualVaultSize = 0;

try
{
using (var fs = new FileStream(exportPath, FileMode.Open, FileAccess.Read, FileShare.Read))
using (var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false))
{
    // Validate meta and required entry.
    var metaEntry = zip.GetEntry("meta.json");
    if (metaEntry == null)
    {
        ShowInfoToast(Loc.Instance["ImportVaultInvalidFile"], toastAnchor, 5200);
        return;
    }

    try
    {
        using var ms = metaEntry.Open();
        using var sr = new StreamReader(ms);
        var metaJson = sr.ReadToEnd();
        using var doc = JsonDocument.Parse(metaJson);

        if (!doc.RootElement.TryGetProperty("format", out var fmt))
            throw new InvalidDataException("Missing format");

        var format = (fmt.GetString() ?? "").Trim();
        if (!string.Equals(format, "PassNotesExport", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid format");

        if (doc.RootElement.TryGetProperty("version", out var verEl) && verEl.TryGetInt32(out var ver))
            exportMetaVersion = ver;

                    isNewerVersion = exportMetaVersion > SupportedExportVersion;
        if (doc.RootElement.TryGetProperty("checksumSha256", out var csEl))
            expectedChecksumSha256 = (csEl.GetString() ?? "").Trim();

        if (doc.RootElement.TryGetProperty("vaultSize", out var sizeEl) && sizeEl.TryGetInt64(out var sz))
            expectedVaultSize = sz;

        // Since export meta v2 we require checksum (integrity check).
        if (exportMetaVersion >= 2 && string.IsNullOrWhiteSpace(expectedChecksumSha256))
            throw new InvalidDataException("Missing checksum");
    }
    catch
    {
        // If meta is unreadable, treat as invalid to avoid importing random zip content.
        ShowInfoToast(Loc.Instance["ImportVaultInvalidFile"], toastAnchor, 5200);
        return;
    }

    var vaultEntry = zip.GetEntry("vault.pnvault");
    if (vaultEntry == null)
    {
        ShowInfoToast(Loc.Instance["ImportVaultInvalidFile"], toastAnchor, 5200);
        return;
    }

    // Extract while computing SHA-256 (streaming).
    using var vs = vaultEntry.Open();
    using var outFs = new FileStream(extractedVaultPath, FileMode.Create, FileAccess.Write, FileShare.None);
    using var sha = SHA256.Create();

    var buffer = new byte[81920];
    int read;
    while ((read = vs.Read(buffer, 0, buffer.Length)) > 0)
    {
        outFs.Write(buffer, 0, read);
        sha.TransformBlock(buffer, 0, read, null, 0);
        actualVaultSize += read;
    }
    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    actualChecksumSha256 = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());

    // Extract attachments (encrypted .pna blobs). Best-effort.
    try
    {
        var attEntries = zip.Entries
            .Where(zipEntry => zipEntry.FullName.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase)
                        && zipEntry.FullName.EndsWith(".pna", StringComparison.OrdinalIgnoreCase));

        var any = false;
        foreach (var att in attEntries)
        {
            var name = Path.GetFileName(att.FullName);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!any)
            {
                Directory.CreateDirectory(extractedAttachmentsDir);
                any = true;
            }

            var dest = Path.Combine(extractedAttachmentsDir, name);
            using var ins = att.Open();
            using var outs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            ins.CopyTo(outs);
        }
    }
    catch { }
}
}
catch (InvalidDataException)
{
    // Not a zip or invalid container structure.
    ShowInfoToast(Loc.Instance["ImportVaultInvalidFile"], toastAnchor, 5200);
    return;
}
catch (Exception ex)
{
    // Any unexpected errors while reading the export file should not crash the app.
    ShowInfoToast(string.Format(Loc.Instance["ImportVaultErrorFmt"], ex.Message), toastAnchor, 6500);
    return;
}

// Validate integrity if checksum is present (v2+ exports include it).
if (expectedVaultSize.HasValue && expectedVaultSize.Value != actualVaultSize)
{
    ShowInfoToast(Loc.Instance["ImportVaultChecksumMismatch"], toastAnchor, 6200);
    return;
}

if (!string.IsNullOrWhiteSpace(expectedChecksumSha256)
    && !string.Equals(actualChecksumSha256, expectedChecksumSha256, StringComparison.OrdinalIgnoreCase))
{
    ShowInfoToast(Loc.Instance["ImportVaultChecksumMismatch"], toastAnchor, 6200);
    return;
}

// Validate that the imported vault can be opened with the current master password
// BEFORE replacing any files (all-or-nothing import).
VaultData? importedData = null;
try
{
    importedData = new VaultStore(extractedVaultPath).Load(_masterPassword);
}
catch (System.Security.Cryptography.CryptographicException)
{
    ShowInfoToast(Loc.Instance["ImportVaultPasswordMismatch"], toastAnchor, 6500);
    return;
}
catch (Exception ex)
{
    ShowInfoToast(string.Format(Loc.Instance["ImportVaultErrorFmt"], ex.Message), toastAnchor, 6500);
    return;
}

// Validate attachment blobs presence. If metadata references blobs, they must be present in the export.
try
{
    importedData.Attachments ??= Array.Empty<VaultAttachment>();
    if (importedData.Attachments.Length > 0)
    {
        if (!Directory.Exists(extractedAttachmentsDir))
        {
            ShowInfoToast(string.Format(Loc.Instance["ImportVaultMissingAttachmentBlobFmt"], "(attachments folder)"), toastAnchor, 6500);
            return;
        }

        foreach (var a in importedData.Attachments)
        {
            var blobName = $"{a.Id:N}.pna";
            var blobPath = Path.Combine(extractedAttachmentsDir, blobName);
            if (!File.Exists(blobPath))
            {
                ShowInfoToast(string.Format(Loc.Instance["ImportVaultMissingAttachmentBlobFmt"], blobName), toastAnchor, 6500);
                return;
            }
        }
    }
}
catch (Exception ex)
{
    ShowInfoToast(string.Format(Loc.Instance["ImportVaultErrorFmt"], ex.Message), toastAnchor, 6500);
    return;
}

            

var integrityNote = string.IsNullOrWhiteSpace(expectedChecksumSha256)
    ? (Environment.NewLine + Environment.NewLine + Loc.Instance["ImportVaultIntegrityUnknown"])
    : "";

var newerVersionNote = isNewerVersion
    ? (Environment.NewLine + Environment.NewLine + string.Format(Loc.Instance["ImportVaultNewerVersionWarningFmt"], exportMetaVersion))
    : "";

var confirm = AppMessageDialogWindow.ShowYesNo(this,
    Loc.Instance["ImportVaultTitle"],
    Loc.Instance["ImportVaultConfirm"] + integrityNote + newerVersionNote);
if (confirm != MessageBoxResult.Yes)
                return;

            var wasUnlocked = IsUnlocked;

            // Preserve current context identity (best-effort) so we can restore it after reload.
            var activeIdentity = _activeFolderNode;
            var selectedIdentity = _selectedFolderNode;

            // Safer default: lock UI and hide all additional windows before replacing the vault file.
            Lock();

            // Create a safety backup of the current vault (if exists).
            string? safetyBackup = null;
            try
            {
                var currentPath = _store.Path;
                safetyBackup = BackupService.CreateBeforeVaultSwitchBackup(currentPath);
            }
            catch { /* ignore */ }

            var safetyInfo = string.IsNullOrWhiteSpace(safetyBackup)
                ? ""
                : (Environment.NewLine + string.Format(Loc.Instance["ImportVaultSafetyBackupCreatedFmt"], Path.GetFileName(safetyBackup)));

            // Local backups for true all-or-nothing: if anything fails after we start replacing files,
            // we roll back to the exact previous vault and attachments.
            string? localVaultBackup = null;
            string? localAttachmentsBackup = null;
            var rollbackNeeded = false;

            try
            {
                // Replace current vault file with the imported one (safe temp + atomic replace where possible).
                // Also swap attachments folder in the same guarded section.
                VaultIoGate.Run("MainWindow.ImportVault.ReplaceVault", () =>
                {
                    var dstVaultPath = _store.Path;
                    var dstAtt = AttachmentsStore.GetAttachmentsDir(dstVaultPath);

                    // Create local backups next to the vault to allow rollback.
                    if (File.Exists(dstVaultPath))
                    {
                        localVaultBackup = dstVaultPath + $".preimport_{Guid.NewGuid():N}.bak";
                        File.Copy(dstVaultPath, localVaultBackup, overwrite: true);
                    }

                    if (Directory.Exists(dstAtt))
                    {
                        localAttachmentsBackup = dstAtt + $".preimport_{Guid.NewGuid():N}";
                        Directory.Move(dstAtt, localAttachmentsBackup);
                    }

                    // From this point, rollback is required on any failure.
                    rollbackNeeded = true;

                    ReplaceFileSafely(extractedVaultPath, dstVaultPath);
                    AttachmentsStore.ReplaceDirectorySafely(extractedAttachmentsDir, dstAtt);
                });

                // Reload immediately (no app restart). If reload fails, we roll back.
                _vault = _store.Load(_masterPassword);

                try { CleanupOrphanAttachmentsBestEffort("ImportVault", force: true, toastAnchor: toastAnchor); } catch { }

                // Restore context identity then rebuild tree (BuildFolderTree remaps to new instances).
                _activeFolderNode = activeIdentity;
                _selectedFolderNode = selectedIdentity;

                BuildFolderTree();

                if (wasUnlocked)
                    RestoreWorkingUnlockedUiAfterServiceLock();

                ShowInfoToast(Loc.Instance["ImportVaultSuccess"] + safetyInfo, toastAnchor, 5200);

                // Commit succeeded. We can remove local backups.
                rollbackNeeded = false;
            }
            catch (Exception ex)
            {
                // Roll back to the exact previous vault/attachments if we already started replacing.
                if (rollbackNeeded)
                {
                    try
                    {
                        VaultIoGate.Run("MainWindow.ImportVault.Rollback", () =>
                        {
                            var dstVaultPath = _store.Path;
                            var dstAtt = AttachmentsStore.GetAttachmentsDir(dstVaultPath);

                            // Restore attachments first (best effort).
                            try
                            {
                                if (Directory.Exists(dstAtt))
                                    Directory.Delete(dstAtt, recursive: true);
                            }
                            catch { }

                            if (!string.IsNullOrWhiteSpace(localAttachmentsBackup) && Directory.Exists(localAttachmentsBackup))
                            {
                                try { Directory.Move(localAttachmentsBackup, dstAtt); } catch { }
                            }

                            // Restore vault.
                            if (!string.IsNullOrWhiteSpace(localVaultBackup) && File.Exists(localVaultBackup))
                            {
                                try { ReplaceFileSafely(localVaultBackup, dstVaultPath); } catch { }
                            }
                        });

                        // Reload previous vault to restore UI.
                        try
                        {
                            _vault = _store.Load(_masterPassword);
                            _activeFolderNode = activeIdentity;
                            _selectedFolderNode = selectedIdentity;
                            BuildFolderTree();
                        }
                        catch { /* best-effort */ }
                    }
                    catch { /* best-effort */ }
                }

                // If import fails, return to the previous state to avoid trapping the user in Locked.
                if (wasUnlocked)
                    RestoreWorkingUnlockedUiAfterServiceLock();

                ShowInfoToast(string.Format(Loc.Instance["ImportVaultErrorFmt"], ex.Message), toastAnchor, 6500);
            }
            finally
            {
                // Cleanup local backups when commit succeeded; keep on failure for forensic (best-effort).
                try
                {
                    if (!rollbackNeeded)
                    {
                        if (!string.IsNullOrWhiteSpace(localVaultBackup) && File.Exists(localVaultBackup))
                            File.Delete(localVaultBackup);
                        if (!string.IsNullOrWhiteSpace(localAttachmentsBackup) && Directory.Exists(localAttachmentsBackup))
                            Directory.Delete(localAttachmentsBackup, recursive: true);
                    }
                }
                catch { }
            }
        }
        finally
        {
            // Best-effort cleanup.
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { }
        }

        e.Handled = true;
    }

private void ExportVault_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before doing work.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        if (!IsUnlocked)
            return;

        var vaultPath = _store?.Path;
        if (string.IsNullOrWhiteSpace(vaultPath) || !File.Exists(vaultPath))
        {
            ShowInfoToast(Loc.Instance["BackupSourceMissing"], sender as UIElement, 3200);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = Loc.Instance["ExportVaultTitle"],
            Filter = Loc.Instance["ExportVaultFilter"],
            DefaultExt = "pnexp",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"PassNotesExport_{DateTime.Now:yyyyMMdd_HHmmss}.pnexp"
        };

        try
        {
            var last = (App.Settings.LastExportDirectory ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(last) && Directory.Exists(last))
                dlg.InitialDirectory = last;
            else
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        catch { }

        if (dlg.ShowDialog(this) != true)
            return;

        var exportPath = dlg.FileName;

        // Remember last used export directory (best-effort).
        try
        {
            var dir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                App.Settings.LastExportDirectory = dir;
                SettingsStore.Save(App.Settings);
            }
        }
        catch { }

        try
        {
            // Read the encrypted vault bytes under the process-wide I/O gate,
            // so we don't overlap with Save/Load/Backup/Restore operations.
            byte[] vaultBytes = Array.Empty<byte>();
            try
            {
                VaultIoGate.Run("MainWindow.ExportVault.ReadVault", () =>
                {
                    vaultBytes = File.ReadAllBytes(vaultPath);
                });

                var checksumSha256 = Convert.ToHexString(SHA256.HashData(vaultBytes));

                var exportDir = Path.GetDirectoryName(exportPath);
                if (string.IsNullOrWhiteSpace(exportDir))
                    exportDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                Directory.CreateDirectory(exportDir);

                // Write export to a temp file in the same directory, then atomically replace/move.
                var tempPath = Path.Combine(exportDir, $".pnexp_{Guid.NewGuid():N}.tmp");
                var backupPath = exportPath + ".bak";

                // Attachments (encrypted sidecar files). We export them alongside the vault.
                var attachmentsDir = AttachmentsStore.GetAttachmentsDir(vaultPath);
                var attachmentFiles = Array.Empty<string>();
                long attachmentsTotalSize = 0;
                try
                {
                    if (Directory.Exists(attachmentsDir))
                    {
                        attachmentFiles = Directory.GetFiles(attachmentsDir, "*.pna", SearchOption.TopDirectoryOnly);
                        foreach (var f in attachmentFiles)
                        {
                            try { attachmentsTotalSize += new FileInfo(f).Length; } catch { }
                        }
                    }
                }
                catch
                {
                    attachmentFiles = Array.Empty<string>();
                    attachmentsTotalSize = 0;
                }

                try
                {
                    using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
                    {
                        var meta = new
                        {
                            format = "PassNotesExport",
                            version = 3,
                            createdUtc = DateTime.UtcNow.ToString("o"),
                            app = "PassNotes",
                            vaultSize = vaultBytes.Length,
                            checksumSha256 = checksumSha256,
                            attachmentsCount = attachmentFiles.Length,
                            attachmentsTotalSize = attachmentsTotalSize,
                        };

                        var metaEntry = zip.CreateEntry("meta.json", CompressionLevel.Optimal);
                        using (var s = metaEntry.Open())
                        using (var sw = new StreamWriter(s))
                        {
                            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
                            sw.Write(json);
                        }

                        var vaultEntry = zip.CreateEntry("vault.pnvault", CompressionLevel.Optimal);
                        using (var s = vaultEntry.Open())
                        {
                            s.Write(vaultBytes, 0, vaultBytes.Length);
                        }

                        // Attachments: encrypted .pna blobs.
                        // For a reliable export, we include all readable blobs; if any blob can't be read, fail the export.
                        foreach (var filePath in attachmentFiles)
                        {
                            var name = Path.GetFileName(filePath);
                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            var attEntry = zip.CreateEntry("attachments/" + name, CompressionLevel.Optimal);
                            // Use permissive sharing to tolerate concurrent readers/writers.
                            using var src = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var dst = attEntry.Open();
                            src.CopyTo(dst);
                        }
                    }

                    // Atomic-ish finalize: Replace when destination exists, otherwise Move.
                    // If finalize fails after creating a backup, best-effort restore the original file.
                    try
                    {
                        if (File.Exists(exportPath))
                        {
                            File.Replace(tempPath, exportPath, backupPath, ignoreMetadataErrors: true);
                        }
                        else
                        {
                            File.Move(tempPath, exportPath);
                        }
                    }
                    catch
                    {
                        // Best-effort restore previous export file.
                        try
                        {
                            if (File.Exists(backupPath))
                                File.Move(backupPath, exportPath, overwrite: true);
                        }
                        catch { }

                        throw;
                    }
                }
                finally
                {
                    // Best-effort cleanup if temp file still exists (e.g., failed before Replace/Move).
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch { }

                    // Best-effort cleanup of backup.
                    try
                    {
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                    }
                    catch { }
                }

                var ask = AppMessageDialogWindow.ShowYesNo(this,
                    Loc.Instance["ExportVaultTitle"],
                    string.Format(Loc.Instance["ExportVaultCreatedOpenFolderFmt"], Path.GetFileName(exportPath)));

                if (ask == MessageBoxResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exportPath}\"")
                        {
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Best-effort only.
                    }
                }
            }
            finally
            {
                // Best-effort clear of sensitive data from memory.
                try
                {
                    if (vaultBytes.Length > 0)
                        Array.Clear(vaultBytes, 0, vaultBytes.Length);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ShowInfoToast(string.Format(Loc.Instance["ExportVaultErrorFmt"], ex.Message), sender as UIElement, 5200);
        }

        e.Handled = true;
    }

    private void EnsureInfoToast()
    {
        if (_infoToastPopup != null)
            return;

        try
        {
            _infoToastText = new TextBlock();
            _infoToastText.SetResourceReference(FrameworkElement.StyleProperty, "BaselineToastText");

            var border = new Border
            {
                Child = _infoToastText
            };
            border.SetResourceReference(FrameworkElement.StyleProperty, "BaselineToastBorder");

            _infoToastPopup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = false,
                Placement = PlacementMode.Bottom,
                Child = border
            };
        }
        catch
        {
            _infoToastPopup = null;
            _infoToastText = null;
        }
    }

    private void ShowInfoToast(string message, UIElement? placementTarget = null, int? durationMs = null, Point? cursorPoint = null)
    {
        // Important: many actions originate from MenuItem inside a ContextMenu/Popup.
        // After click, those visuals are often unloaded, and a Popup anchored to them may not appear.
        // So we fall back to a stable anchor (в° menu button, then the window).
        try
        {
            EnsureInfoToast();
            if (_infoToastPopup == null || _infoToastText == null)
                return;

            _infoToastText.Text = message ?? "";

            if (cursorPoint is Point point)
            {
                var root = GetToastAnchorRoot();
                var rootW = root.ActualWidth;
                var rootH = root.ActualHeight;

                if (rootW > 1 && rootH > 1)
                {
                    var desired = MeasurePopupDesiredSize(_infoToastPopup);
                    var w = Math.Max(40, desired.Width);
                    var h = Math.Max(24, desired.Height);

                    const double offset = 12;
                    const double pad = 8;

                    var x = point.X + offset;
                    var y = point.Y + offset;

                    if (x + w > rootW - pad)
                        x = point.X - offset - w;
                    if (y + h > rootH - pad)
                        y = point.Y - offset - h;

                    x = Math.Max(pad, Math.Min(x, rootW - w - pad));
                    y = Math.Max(pad, Math.Min(y, rootH - h - pad));

                    var snap = new PopupPlacementSnapshot(_infoToastPopup);

                    try
                    {
                        _infoToastPopup.PlacementTarget = root;
                        _infoToastPopup.Placement = PlacementMode.Relative;
                        _infoToastPopup.HorizontalOffset = x;
                        _infoToastPopup.VerticalOffset = y;
                        _infoToastPopup.PlacementRectangle = Rect.Empty;
                        _infoToast.Show(_infoToastPopup, durationMs, onClose: () => snap.Restore(_infoToastPopup));
                        return;
                    }
                    catch
                    {
                        snap.Restore(_infoToastPopup);
                    }
                }
            }

            UIElement? anchor = placementTarget;
            try
            {
                if (anchor == null || PresentationSource.FromVisual(anchor) == null)
                    anchor = null;
            }
            catch
            {
                anchor = null;
            }

            anchor ??= (EntriesMenuBtn as UIElement);
            anchor ??= (UIElement)this;

            _infoToastPopup.PlacementTarget = anchor;
            _infoToast.Show(_infoToastPopup, durationMs);
        }
        catch
        {
            // best-effort
        }
    }

    

    private void OpenHelp_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before opening Help.
        try { if (EntriesMenuBtn != null) EntriesMenuBtn.IsChecked = false; } catch { }

        HelpWindowManager.ShowOrActivate(this, "index.md");
    }

    private void OpenSupportAuthor_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        ShowHostedSupportAuthorDialog();
    }

    private void ShowHostedSupportAuthorDialog()
    {
        ShowHostedDialog(new HostedDialogRequest
        {
            Title = Loc.Instance["SupportAuthorTitle"],
            Content = new SupportAuthorHostedView(),
            PrimaryButtonText = Loc.Instance["Close"],
            PrimaryAction = CloseHostedDialog,
            Width = 580,
            MinWidth = 580,
            MaxWidth = 580
        });
    }

    public bool IsHostedDialogOpen => HostedDialogHost.IsOpen;

    public MessageBoxResult ShowHostedAppMessageDialog(string title, string message, MessageBoxButton mode, Func<MessageBoxResult, bool>? completionInterceptor = null, bool replaceCurrentModal = false)
    {
        var result = mode switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };

        bool TryComplete(MessageBoxResult dialogResult)
        {
            result = dialogResult;

            try
            {
                if (completionInterceptor?.Invoke(dialogResult) == true)
                    return true;
            }
            catch
            {
                // Fall back to normal close below.
            }

            CloseHostedDialog();
            return false;
        }

        var request = new HostedDialogRequest
        {
            Title = title,
            Content = new AppMessageContentHostedView(message),
            Width = 460,
            MinWidth = 420,
            MaxWidth = 520,
            PrimaryButtonText = mode == MessageBoxButton.OK ? Loc.Instance["Ok"] : Loc.Instance["Yes"],
            PrimaryAction = () => TryComplete(mode == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Yes),
            SecondaryButtonText = mode == MessageBoxButton.OK ? string.Empty : Loc.Instance["No"],
            SecondaryAction = mode == MessageBoxButton.OK ? null : () => TryComplete(MessageBoxResult.No),
            TertiaryButtonText = mode == MessageBoxButton.YesNoCancel ? Loc.Instance["Cancel"] : string.Empty,
            TertiaryAction = mode == MessageBoxButton.YesNoCancel ? () => TryComplete(MessageBoxResult.Cancel) : null
        };

        if (replaceCurrentModal)
            ReplaceHostedDialogModal(request);
        else
            ShowHostedDialogModal(request);

        return result;
    }

    private string? ShowHostedFolderDialog(string title, string label, string initial = "")
    {
        string? folderName = null;

        var view = new FolderDialogHostedView(label, initial);
        view.Accepted += value =>
        {
            folderName = value;
            CloseHostedDialog();
        };
        view.Cancelled += CloseHostedDialog;

        ShowHostedDialogModal(new HostedDialogRequest
        {
            Title = title,
            Content = view,
            PrimaryButtonText = Loc.Instance["Save"],
            PrimaryAction = view.RequestPrimaryAction,
            SecondaryButtonText = Loc.Instance["Cancel"],
            SecondaryAction = view.RequestSecondaryAction,
            Width = 420,
            MinWidth = 380,
            MaxWidth = 460,
            PreferContentFocus = true
        });

        return folderName;
    }

    private string? ShowHostedMasterPasswordPromptDialog(string title, string prompt, Func<string, string?>? validationErrorFactory = null)
        => ShowHostedMasterPasswordPromptDialog(title, prompt, validationErrorFactory, null, false);

    private string? ShowHostedMasterPasswordPromptDialog(
        string title,
        string prompt,
        Func<string, string?>? validationErrorFactory,
        Func<string, bool>? completionInterceptor,
        bool replaceCurrentModal)
    {
        string? password = null;

        var view = new MasterPasswordPromptHostedView(prompt);
        view.Accepted += value =>
        {
            try
            {
                var validationError = validationErrorFactory?.Invoke(value);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    view.ShowError(validationError);
                    return;
                }
            }
            catch
            {
                // Fall back to normal close below.
            }

            password = value;

            try
            {
                if (completionInterceptor?.Invoke(value) == true)
                    return;
            }
            catch
            {
                // Fall back to normal close below.
            }

            CloseHostedDialog();
        };
        view.Cancelled += CloseHostedDialog;

        var request = new HostedDialogRequest
        {
            Title = title,
            Content = view,
            PrimaryButtonText = Loc.Instance["Ok"],
            PrimaryAction = view.RequestPrimaryAction,
            SecondaryButtonText = Loc.Instance["Cancel"],
            SecondaryAction = view.RequestSecondaryAction,
            Width = 420,
            MinWidth = 380,
            MaxWidth = 460,
            PreferContentFocus = true
        };

        if (replaceCurrentModal)
            ReplaceHostedDialogModal(request);
        else
            ShowHostedDialogModal(request);

        return password;
    }

    private (string OldPassword, string NewPassword)? ShowHostedChangePasswordDialog(Func<(string OldPassword, string NewPassword), bool>? completionInterceptor = null)
    {
        (string OldPassword, string NewPassword)? result = null;

        var view = new ChangePasswordHostedView();
        view.Accepted += (oldPassword, newPassword) =>
        {
            result = (oldPassword, newPassword);

            try
            {
                if (completionInterceptor?.Invoke(result.Value) == true)
                    return;
            }
            catch
            {
                // Fall back to normal close below.
            }

            CloseHostedDialog();
        };
        view.Cancelled += CloseHostedDialog;

        ShowHostedDialogModal(new HostedDialogRequest
        {
            Title = Loc.Instance["ChangePasswordTitle"],
            Content = view,
            PrimaryButtonText = Loc.Instance["Save"],
            PrimaryAction = view.RequestPrimaryAction,
            SecondaryButtonText = Loc.Instance["Cancel"],
            SecondaryAction = view.RequestSecondaryAction,
            Width = 500,
            MinWidth = 480,
            MaxWidth = 520,
            PreferContentFocus = true
        });

        return result;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        // Close the в° popup before opening another window.
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        if (!IsUnlocked)
            return;

        ShowHostedSettingsDialog();
    }

    private bool TrySwitchVaultPath(string currentVaultPath, string newVaultPath)
    {
        try
        {
            newVaultPath = (newVaultPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(newVaultPath))
                return false;

            if (!System.IO.Path.IsPathRooted(newVaultPath))
            {
                AppMessageDialogWindow.ShowOk(
                    this,
                    Loc.Instance["VaultSwitchTitle"],
                    string.Format(Loc.Instance["VaultSwitchErrorFmt"], Loc.Instance["VaultSwitchPathMustBeAbsolute"]));
                return false;
            }

            currentVaultPath = (currentVaultPath ?? "").Trim();
            if (string.Equals(currentVaultPath, newVaultPath, StringComparison.OrdinalIgnoreCase))
                return false;

            // Determine how to handle existing target.
            bool copyCurrentToTarget = false;

            if (File.Exists(newVaultPath))
            {
                var r = AppMessageDialogWindow.ShowYesNoCancel(
                    this,
                    Loc.Instance["VaultSwitchTitle"],
                    Loc.Instance["VaultSwitchExistingConfirm"]);

                if (r == MessageBoxResult.Cancel)
                    return false;

                // Yes: use existing vault file. No: overwrite it with current vault.
                copyCurrentToTarget = (r == MessageBoxResult.No);
            }
            else
            {
                // New file path: copy current vault to the new location.
                copyCurrentToTarget = true;
            }

            if (copyCurrentToTarget && !File.Exists(currentVaultPath))
            {
                AppMessageDialogWindow.ShowOk(
                    this,
                    Loc.Instance["VaultSwitchTitle"],
                    string.Format(Loc.Instance["VaultSwitchNoCurrentVaultFmt"], currentVaultPath));
                return false;
            }

            // Preserve context identity (best-effort) to restore after reload.
            var activeIdentity = _activeFolderNode;
            var selectedIdentity = _selectedFolderNode;
            var wasUnlocked = IsUnlocked;

            // Safer default: lock UI and hide all additional windows before switching vault file.
            Lock();

            // Create a safety backup of the current vault (if exists).
            string? safetyBackup = null;
            try
            {
                safetyBackup = BackupService.CreateBeforeVaultSwitchBackup(currentVaultPath);
            }
            catch { /* ignore */ }

            // Ensure target directory exists.
            try
            {
                var dir = System.IO.Path.GetDirectoryName(newVaultPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }

            // Copy current vault to target if requested.
            if (copyCurrentToTarget)
            {
                File.Copy(currentVaultPath, newVaultPath, overwrite: true);
            }

            // Switch store path.
            _store.SetPath(newVaultPath);

            // Try to reload immediately (no app restart).
            try
            {
                _vault = _store.Load(_masterPassword);

                try { CleanupOrphanAttachmentsBestEffort("VaultSwitch", force: true, toastAnchor: null); } catch { }

                _activeFolderNode = activeIdentity;
                _selectedFolderNode = selectedIdentity;
                BuildFolderTree();

                if (wasUnlocked)
                    RestoreWorkingUnlockedUiAfterServiceLock();

                var backupInfo = string.IsNullOrWhiteSpace(safetyBackup)
                    ? ""
                    : (Environment.NewLine + string.Format(Loc.Instance["VaultSwitchBackupCreatedFmt"], System.IO.Path.GetFileName(safetyBackup)));

                AppMessageDialogWindow.ShowOk(
                    this,
                    Loc.Instance["VaultSwitchTitle"],
                    string.Format(Loc.Instance["VaultSwitchSuccessFmt"], newVaultPath) + backupInfo);

                return true;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // New vault uses a different password; keep the app locked.
                var backupInfo = string.IsNullOrWhiteSpace(safetyBackup)
                    ? ""
                    : (Environment.NewLine + string.Format(Loc.Instance["VaultSwitchBackupCreatedFmt"], System.IO.Path.GetFileName(safetyBackup)));

                AppMessageDialogWindow.ShowOk(
                    this,
                    Loc.Instance["VaultSwitchTitle"],
                    Loc.Instance["VaultSwitchPasswordMismatch"] + backupInfo);

                return true; // path switch happened; user can unlock with another password.
            }
            catch (Exception ex)
            {
                LogException("VaultSwitch.Reload", ex);
                AppMessageDialogWindow.ShowOk(
                    this,
                    Loc.Instance["VaultSwitchTitle"],
                    string.Format(Loc.Instance["VaultSwitchErrorFmt"], ex.Message));

                // Try to revert the store path to the previous one to keep the app usable.
                try { _store.SetPath(currentVaultPath); } catch { }
                try
                {
                    _vault = _store.Load(_masterPassword);
                    try { CleanupOrphanAttachmentsBestEffort(); } catch { }
                    _activeFolderNode = activeIdentity;
                    _selectedFolderNode = selectedIdentity;
                    BuildFolderTree();
                    if (wasUnlocked)
                        RestoreWorkingUnlockedUiAfterServiceLock();
                }
                catch { }

                return false;
            }
        }
        catch (Exception ex)
        {
            LogException("VaultSwitch", ex);
            AppMessageDialogWindow.ShowOk(
                this,
                Loc.Instance["VaultSwitchTitle"],
                string.Format(Loc.Instance["VaultSwitchErrorFmt"], ex.Message));
            return false;
        }
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesMenuBtn != null)
            EntriesMenuBtn.IsChecked = false;

        Application.Current.Shutdown();
    }
private void EnsureDefaultEntrySort()
    {
        // Default sort: by Title. User can change it manually by clicking column headers.
        if (Grid.Items.SortDescriptions.Count == 0)
            Grid.Items.SortDescriptions.Add(new SortDescription(nameof(VaultEntry.Title), ListSortDirection.Ascending));
    }

    private void SelectEntriesByIds(System.Collections.Generic.HashSet<Guid> ids)
    {
        if (ids == null || ids.Count == 0)
            return;

        var grid = Grid;
        if (grid == null)
            return;

        grid.UnselectAll();

        VaultEntry? last = null;
        foreach (var it in _view)
        {
            if (!ids.Contains(it.Id))
                continue;

            try
            {
                grid.SelectedItems.Add(it);
                last = it;
            }
            catch { /* ignore */ }
        }

        if (last != null)
        {
            grid.SelectedItem = last;
            grid.ScrollIntoView(last);
        }
    }


    private void RefreshGrid()
    {
        if (!IsUnlocked)
        {
            SetEntrySearchActive(false);
            // While locked, never show any entries (safer default).
            _view.Clear();
            OnPropertyChanged(nameof(DisplayedEntriesCount));
            (SelectAllEntriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            return;
        }

        // Preserve multi-selection when refreshing.
        var selectedIds = new System.Collections.Generic.HashSet<Guid>();
        try
        {
            foreach (var obj in Grid.SelectedItems)
                if (obj is VaultEntry ve)
                    selectedIds.Add(ve.Id);
        }
        catch { /* ignore */ }

        var rawSearch = (SearchBox.Text ?? "").Trim();
        var searchTokens = SplitSearchTokens(rawSearch);
        var isSearchActive = searchTokens.Length > 0;

        SetEntrySearchActive(isSearchActive);

        var items = _vault.Entries ?? Array.Empty<VaultEntry>();

        var ctx = _activeFolderNode;
        bool inTrashContext = ctx?.Kind == FolderNodeKind.Trash;

        // Normal views/search must never show trashed entries.
        // Trash context shows ONLY trashed entries.
        items = inTrashContext
            ? items.Where(x => x.IsDeleted).ToArray()
            : items.Where(x => !x.IsDeleted).ToArray();

        // Global search is explicit: when query is non-empty we search across all entries,
        // while keeping the current folder context intact (context is restored when query is cleared).
        var showFolderPathColumn = inTrashContext;

        if (isSearchActive)
        {
            showFolderPathColumn = true;
            items = ApplySearch(items, searchTokens).ToArray();
        }
        else
        {
            if (ctx == null)
            {
                // No context selected => show 0 entries (never show "all entries" implicitly).
                items = Array.Empty<VaultEntry>();
            }
            else if (ctx.Kind == FolderNodeKind.Favorites)
            {
                showFolderPathColumn = true;
                items = items.Where(x => x.IsFavorite).ToArray();
            }
            else if (ctx.Kind == FolderNodeKind.Trash)
            {
                // Trash context already filtered to deleted entries above.
                showFolderPathColumn = true;
            }
            else if (ctx.Kind == FolderNodeKind.NoFolder)
            {
                items = items.Where(x => x.FolderId == null).ToArray();
            }
            else if (ctx.Kind == FolderNodeKind.Folder)
            {
                var folderId = ctx.Id;
                items = items.Where(x => x.FolderId != null && x.FolderId.Value == folderId).ToArray();
            }
            else
            {
                items = Array.Empty<VaultEntry>();
            }
        }

        // Toggle optional "Folder" column.
        try
        {
            if (FolderPathColumn != null)
                FolderPathColumn.Visibility = showFolderPathColumn ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { /* ignore */ }

        // Compute UI-only folder path for aggregated contexts.
        if (showFolderPathColumn)
        {
            foreach (var it in items)
                it.UiFolderPath = inTrashContext ? GetFolderPathDisplayName(it.DeletedFromFolderId) : GetFolderPathDisplayName(it.FolderId);
        }
        else
        {
            // Keep it empty to avoid stale values when switching views.
            foreach (var it in items)
                it.UiFolderPath = "";
        }

        // Keep stable ItemsSource (_view) so manual sorting (column header clicks) is preserved.
        _view.Clear();
        foreach (var it in items)
            _view.Add(it);

        EnsureDefaultEntrySort();

        if (selectedIds.Count > 0)
            SelectEntriesByIds(selectedIds);

        SyncSelectedEntriesFromGrid();
        OnPropertyChanged(nameof(DisplayedEntriesCount));

        (SelectAllEntriesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private static string[] SplitSearchTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static System.Collections.Generic.IEnumerable<VaultEntry> ApplySearch(
        System.Collections.Generic.IEnumerable<VaultEntry> items,
        string[] tokens)
    {
        if (tokens == null || tokens.Length == 0)
            return items;

        return items.Where(x => EntryMatchesTokens(x, tokens));
    }

private static bool EntryMatchesTokens(VaultEntry x, string[] tokens)
{
    foreach (var t in tokens)
    {
        if (string.IsNullOrWhiteSpace(t))
            continue;

        if (!ContainsCI(x.Title, t)
            && !ContainsCI(x.Username, t)
            && !ContainsCI(x.Url, t)
            && !ContainsCI(x.Comment, t))
            return false;
    }

    return true;
}

private static bool ContainsCI(string? haystack, string needle)
{
    if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
        return false;

    return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
}

private VaultEntry? Selected() => Grid.SelectedItem as VaultEntry;

    // -----------------------------
    // Keyboard:
    //  - Left/Right arrows switch focus between folder tree and entries grid (pane navigation)
    //  - Up/Down arrows should keep navigation inside entries grid (never land on toolbar/slider/etc.)
    //  - Enter opens selected entry / activates selected folder
    // -----------------------------

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HostedDialogHost.IsOpen)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (HostedDialogHost.Content is EntryHostedView entryView)
                {
                    e.Handled = true;
                    RunHostedKeyboardActionDeferred("HostedDialogCtrlS", entryView.RequestPrimaryAction);
                }

                return;
            }

            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (HasInteractiveHostedPopupOpen())
                    return;

                e.Handled = true;
                RunHostedKeyboardActionDeferred("HostedDialogEscape", () =>
                {
                    if (!HostedDialogHost.IsOpen)
                        return;

                    if (HostedDialogHost.Content is IHostedDialogCloseRequestHandler closeHandler)
                        closeHandler.TryHandleHostedDialogCloseRequest();
                    else
                        CloseHostedDialog();
                });

                return;
            }

            if (IsMainWindowBackgroundTarget(Keyboard.FocusedElement))
            {
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        EnsureHostedDialogFocusIsolation();
                    }
                    catch
                    {
                        // ignore
                    }
                }), DispatcherPriority.Input);
            }

            return;
        }

        if (e.Key != Key.Enter && e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down)
            return;

        // Do not interfere with modified shortcuts (Ctrl/Shift/Alt+Enter) or OS key combos.
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows)) != ModifierKeys.None)
            return;

        // Do not steal keys when the user is typing in an input control.
        var src = e.OriginalSource as DependencyObject;
        if (IsTextInputSource(src))
            return;

        // Do not interfere with menu/context menu navigation.
        if (src != null)
        {
            try
            {
                if (FindVisualParent<MenuItem>(src) != null || FindVisualParent<ContextMenu>(src) != null)
                    return;
            }
            catch
            {
                // ignore
            }
        }

        // Up/Down: keep arrow navigation in entries list. This prevents focus from jumping to toolbar buttons,
        // the row-height slider, breadcrumbs, etc. (Windows-like behavior: arrows move selection in the list).
        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            // If the user is in the folder tree, let TreeView handle arrows.
            if (FolderTree != null && FolderTree.IsKeyboardFocusWithin)
                return;

            // If entries grid is missing, nothing to do.
            if (Grid == null)
                return;

            SafeUi("ArrowStayInEntries", () =>
            {
                try
                {
                    if (Grid.Items.Count <= 0)
                        return;

                    // If multi-select is active (more than one selected), do NOT change selection here
                    // (setting SelectedIndex would clear it). Just restore focus to the grid.
                    int selCount;
                    try { selCount = Grid.SelectedItems?.Count ?? 0; }
                    catch { selCount = Grid.SelectedItem != null ? 1 : 0; }

                    Grid.Focus();

                    if (selCount > 1)
                    {
                        try { Grid.ScrollIntoView(Grid.SelectedItem); } catch { }
                        return;
                    }

                    int delta = e.Key == Key.Up ? -1 : 1;
                    int idx = Grid.SelectedIndex;
                    if (idx < 0)
                        idx = 0;
                    else
                    {
                        int next = idx + delta;
                        if (next < 0) next = 0;
                        if (next >= Grid.Items.Count) next = Grid.Items.Count - 1;
                        idx = next;
                    }

                    // Assign only when changed (avoid unnecessary selection churn).
                    if (Grid.SelectedIndex != idx)
                        Grid.SelectedIndex = idx;

                    try { Grid.ScrollIntoView(Grid.SelectedItem); } catch { }
                }
                catch
                {
                    // best-effort
                }
            });

            e.Handled = true;
            return;
        }

        // Left/Right: pane navigation between folder tree and entries list.
        // We intentionally keep folder expand/collapse on Enter to avoid breaking this behavior.
        if (e.Key == Key.Right)
        {
            if (FolderTree != null && FolderTree.IsKeyboardFocusWithin)
            {
                // In folder multi-select mode arrows are used inside the tree; don't override them.
                if (IsFolderMultiSelectMode)
                    return;

                SafeUi("ArrowFocusToEntries", () =>
                {
                    try
                    {
                        if (Grid == null)
                            return;

                        Grid.Focus();

                        // Make sure arrow navigation works immediately.
                        if (Grid.Items.Count > 0)
                        {
                            bool hasSelection;
                            try { hasSelection = (Grid.SelectedItems?.Count ?? 0) > 0 || Grid.SelectedItem != null; }
                            catch { hasSelection = Grid.SelectedItem != null; }

                            if (!hasSelection)
                                Grid.SelectedIndex = 0;

                            try { Grid.ScrollIntoView(Grid.SelectedItem); } catch { }
                        }
                    }
                    catch
                    {
                        // best-effort
                    }
                });

                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Left)
        {
            if (Grid != null && Grid.IsKeyboardFocusWithin)
            {
                SafeUi("ArrowFocusToFolders", () =>
                {
                    try
                    {
                        if (FolderTree == null)
                            return;

                        FolderTree.Focus();
                    }
                    catch
                    {
                        // best-effort
                    }
                });

                e.Handled = true;
            }
            return;
        }

        // Enter on folder tree: activate the selected folder as the right-pane context.
        if (FolderTree != null && FolderTree.IsKeyboardFocusWithin)
        {
            // In folder multi-select mode Enter must do nothing.
            if (IsFolderMultiSelectMode)
                return;

            if (_selectedFolderNode == null)
                return;


            // If the selected folder has children, Enter toggles expand/collapse.
            try
            {
                var tvi = FindVisualParent<TreeViewItem>(src)
                          ?? FolderTree.ItemContainerGenerator.ContainerFromItem(_selectedFolderNode) as TreeViewItem;

                bool hasChildren = false;
                try { hasChildren = tvi?.HasItems == true; } catch { /* ignore */ }
                if (!hasChildren)
                {
                    try { hasChildren = _selectedFolderNode.Children != null && _selectedFolderNode.Children.Count > 0; } catch { /* ignore */ }
                }

                if (tvi != null && hasChildren)
                    tvi.IsExpanded = !tvi.IsExpanded;
            }
            catch
            {
                // best-effort: expanding/collapsing is optional; never break Enter activation
            }

            SafeUi("EnterActivateFolder", () =>
            {
                _activeFolderNode = _selectedFolderNode;
                UpdateFolderActionButtons();
                UpdateEntryActionButtons();
                RefreshGrid();
                UpdateActiveContextBindings();
                FolderTree?.Focus();
            });

            e.Handled = true;
            return;
        }

        // Enter on entries grid: open the selected entry (single-select only).
        if (Grid != null && Grid.IsKeyboardFocusWithin)
        {
            int selectedCount;
            try { selectedCount = Grid.SelectedItems?.Count ?? 0; }
            catch { selectedCount = Grid.SelectedItem != null ? 1 : 0; }

            if (selectedCount != 1)
                return;

            var entry = Grid.SelectedItem as VaultEntry;
            if (entry == null)
            {
                try
                {
                    if (Grid.SelectedItems != null && Grid.SelectedItems.Count == 1)
                        entry = Grid.SelectedItems[0] as VaultEntry;
                }
                catch { /* ignore */ }
            }

            if (entry == null)
                return;

            e.Handled = true;
            RunHostedKeyboardActionDeferred("EnterOpenEntry", () =>
            {
                if (HostedDialogHost.IsOpen)
                    return;

                // Keep behavior consistent with double-click: in Trash we restore instead of editing.
                if (entry.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
                    TryRestoreFromTrashByDoubleClick(entry);
                else
                    OpenEntryEditor(entry);
            });
        }
    }

    private VaultEntry? ResolveUrlActionEntry(object? sender)
    {
        if (sender is FrameworkElement fe)
        {
            if (fe.Tag is VaultEntry taggedEntry)
                return taggedEntry;

            if (fe.DataContext is VaultEntry dataContextEntry)
                return dataContextEntry;
        }

        var selected = Selected();
        if (selected != null)
            return selected;

        try
        {
            if (SelectedEntries.Count == 1)
                return SelectedEntries[0];
        }
        catch
        {
            // ignore
        }

        return Grid.SelectedItem as VaultEntry;
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        var entry = ResolveUrlActionEntry(sender);
        if (entry == null || entry.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
            return;

        var source = sender is MenuItem ? "context_menu" : "inline_button";
        var cursorPoint = sender is MenuItem ? Mouse.GetPosition(GetToastAnchorRoot()) : (Point?)null;

        DiagnosticsLog.AppendLine("ENTRY_URL_COPY_BEGIN", $"source={source}");
        var ok = EntryUrlActions.TryCopy(entry.Url, out var failureReason);

        if (ok)
        {
            DiagnosticsLog.AppendLine("ENTRY_URL_COPY_END", $"source={source} result=ok");
            ShowInfoToast(Loc.Instance["Copied"], sender as UIElement, 2200, cursorPoint);
        }
        else
        {
            DiagnosticsLog.AppendLine("ENTRY_URL_COPY_END", $"source={source} result=fail reason={(failureReason ?? "unknown")}");
            ShowInfoToast(Loc.Instance["CopyFailed"], sender as UIElement, 2600, cursorPoint);
        }
    }

    private void OpenUrlInBrowser_Click(object sender, RoutedEventArgs e)
    {
        var entry = ResolveUrlActionEntry(sender);
        if (entry == null || entry.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
            return;

        EntryUrlActions.TryOpenInBrowser(entry.Url, "ENTRY_URL_OPEN");
    }

    private static bool IsTextInputSource(DependencyObject? src)
    {
        if (src == null)
            return false;

        // TextBoxBase covers TextBox and RichTextBox.
        if (FindVisualParent<TextBoxBase>(src) != null)
            return true;

        if (FindVisualParent<PasswordBox>(src) != null)
            return true;

        // Any ComboBox (even non-editable) should keep its default Enter behavior.
        if (FindVisualParent<ComboBox>(src) != null)
            return true;

        return false;
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Open ONLY when double-clicking a real row/cell, not empty space.
        var src = ResolveGridEventSource(Grid, e);
        var row = FindDataGridRowFromSource(Grid, src);
        var cell = FindDataGridCellFromSource(Grid, src);
        if (row == null && cell == null)
            return;

        // Resolve the clicked row item explicitly (robust when we defer selection changes for multi-select).
        if (row?.Item is VaultEntry clicked)
        {
            try
            {
                SetSingleGridSelection(clicked);
            }
            catch
            {
                // ignore
            }

            if (clicked.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
            {
                TryRestoreFromTrashByDoubleClick(clicked);
                e.Handled = true;
                return;
            }

            OpenEntryEditor(clicked);
            e.Handled = true;
            return;
        }

        if (Grid.SelectedItem is VaultEntry sel)
        {
            if (sel.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
                TryRestoreFromTrashByDoubleClick(sel);
            else
                OpenEntryEditor(sel);

            e.Handled = true;
        }
    }

    // -----------------------------
    // Drag & Drop: start dragging selected entries from the grid
    // -----------------------------

    private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _entriesDragArmed = false;
        _entriesDeferSingleSelectOnMouseUp = false;
        _entriesDeferredSingleSelectItem = null;
    }

    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = ResolveGridEventSource(Grid, e);

        if (FindVisualParent<ScrollBar>(src) != null)
            return;

        if (FindVisualParent<DataGridColumnHeader>(src) != null)
            return;

        if (FindDataGridRowFromSource(Grid, src) != null || FindDataGridCellFromSource(Grid, src) != null)
            return;

        ClearEntriesSelectionSafe(clearCurrentItem: true);
        try { Grid.Focus(); } catch { /* ignore */ }
    }

    private void EntriesGridRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _entriesDragStartPoint = e.GetPosition(null);
        _entriesDragArmed = false;
        _entriesDeferSingleSelectOnMouseUp = false;
        _entriesDeferredSingleSelectItem = null;
        _entriesLeftMouseDownOnItem = true;

        if (sender is not DataGridRow row || row.Item is not VaultEntry clickedEntry)
            return;

        if (IsGridEmbeddedButtonSource(e.OriginalSource as DependencyObject))
            return;

        _entriesDragArmed = true;

        if (e.ClickCount >= 2)
        {
            SetSingleGridSelection(clickedEntry);
            try { Grid.Focus(); } catch { /* ignore */ }

            if (clickedEntry.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
                TryRestoreFromTrashByDoubleClick(clickedEntry);
            else
                OpenEntryEditor(clickedEntry);

            e.Handled = true;
            return;
        }

        // Windows-like behavior:
        // If there is a multi-selection and the user presses LMB on one of the already-selected rows
        // (without Ctrl/Shift), do not collapse selection immediately.
        // If the user starts dragging -> keep multi-select; if they release without dragging -> collapse to that row.
        if (row.IsSelected
            && Grid.SelectedItems != null
            && Grid.SelectedItems.Count > 1
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
        {
            _entriesDeferSingleSelectOnMouseUp = true;
            _entriesDeferredSingleSelectItem = clickedEntry;

            try { Grid.Focus(); } catch { /* ignore */ }

            // Prevent DataGrid from collapsing selection on mouse down.
            e.Handled = true;
            return;
        }

        try { Grid.Focus(); } catch { /* ignore */ }

        // For a plain single click, let DataGrid own the normal selection path.
        // Custom code is kept only for scenarios that DataGrid does not handle the way PassNotes needs.
    }

    private void EntriesGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.Item is not VaultEntry clickedEntry)
            return;

        if (IsGridEmbeddedButtonSource(e.OriginalSource as DependencyObject))
            return;

        bool alreadySelected = false;
        try
        {
            alreadySelected = Grid.SelectedItems?.Contains(clickedEntry) == true;
        }
        catch { /* ignore */ }

        if (!alreadySelected)
        {
            SetSingleGridSelection(clickedEntry);
        }

        try { Grid.Focus(); } catch { /* ignore */ }
    }

    private void Grid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // If we deferred single-selection and no drag was initiated, collapse to the clicked row now.
        if (_entriesDeferSingleSelectOnMouseUp && _entriesDeferredSingleSelectItem is not null)
        {
            // If the user is holding Ctrl/Shift on mouse-up, do nothing (they intended multi-selection changes).
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None)
            {
                _entriesDeferSingleSelectOnMouseUp = false;
                _entriesDeferredSingleSelectItem = null;
                return;
            }

            try
            {
                SetSingleGridSelection(_entriesDeferredSingleSelectItem);
                Grid.ScrollIntoView(_entriesDeferredSingleSelectItem);
            }
            catch
            {
                // ignore
            }
            finally
            {
                _entriesDeferSingleSelectOnMouseUp = false;
                _entriesDeferredSingleSelectItem = null;
                _entriesLeftMouseDownOnItem = false;
            }

            return;
        }

        if (_entriesLeftMouseDownOnItem)
        {
            _entriesLeftMouseDownOnItem = false;
            return;
        }

        var src = ResolveGridEventSource(Grid, e);

        if (FindVisualParent<ScrollBar>(src) != null)
            return;

        if (FindVisualParent<DataGridColumnHeader>(src) != null)
            return;

        if (FindDataGridRowFromSource(Grid, src) != null || FindDataGridCellFromSource(Grid, src) != null)
            return;

        ClearEntriesSelectionSafe(clearCurrentItem: true);
        try { Grid.Focus(); } catch { /* ignore */ }
        _entriesLeftMouseDownOnItem = false;
    }

    private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_entriesDragArmed)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _entriesDragArmed = false;
            return;
        }

        // Suppress the built-in DataGrid sweep-selection route:
        // moving the mouse with pressed LMB over rows must no longer extend selection.
        // We still keep Ctrl/Shift/Ctrl+A and our own drag&drop route below.
        e.Handled = true;

        var pos = e.GetPosition(null);
        var dx = Math.Abs(pos.X - _entriesDragStartPoint.X);
        var dy = Math.Abs(pos.Y - _entriesDragStartPoint.Y);

        if (dx < SystemParameters.MinimumHorizontalDragDistance && dy < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Trash entries: dragging is not a move operation; for multi-select we offer restore (with master-password).
        if (_activeFolderNode?.Kind == FolderNodeKind.Trash)
        {
            _entriesDragArmed = false;

            var selectedIds = new HashSet<Guid>();
            try
            {
                foreach (var obj in Grid.SelectedItems)
                {
                    if (obj is VaultEntry entry)
                        selectedIds.Add(entry.Id);
                }
            }
            catch { /* ignore */ }

            if (selectedIds.Count == 0 && Grid.SelectedItem is VaultEntry singleEntry)
                selectedIds.Add(singleEntry.Id);

            if (selectedIds.Count >= 1)
            {
                try
                {
                    var ask = selectedIds.Count == 1
                        ? Loc.Instance["TrashRestoreAsk"]
                        : string.Format(Loc.Instance["TrashRestoreAskMany"], selectedIds.Count);

                    var msg = $"{Loc.Instance["TrashOnlyRestore"]}\n\n{ask}";
                    StartHostedTrashRestoreConfirmationFlow(selectedIds, msg);
                }
                catch
                {
                    // ignore
                }
                return;
            }

            // No selected entries - nothing to drag.
            return;
        }
        // Collect selected entry ids.
        var ids = new System.Collections.Generic.List<Guid>();
        bool anyDeleted = false;
        try
        {
            foreach (var obj in Grid.SelectedItems)
            {
                if (obj is VaultEntry ve)
                {
                    ids.Add(ve.Id);
                    if (ve.IsDeleted)
                        anyDeleted = true;
                }
            }
        }
        catch { /* ignore */ }

        if (ids.Count == 0)
        {
            if (Grid.SelectedItem is VaultEntry single)
            {
                ids.Add(single.Id);
                if (single.IsDeleted)
                    anyDeleted = true;
            }
        }

        if (anyDeleted)
        {
            _entriesDragArmed = false;
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return;
        }

        if (ids.Count == 0)
        {
            _entriesDragArmed = false;
            return;
        }

        _entriesDragArmed = false;

        // Drag starts -> keep the multi-selection (do not collapse on mouse-up).
        _entriesDeferSingleSelectOnMouseUp = false;
        _entriesDeferredSingleSelectItem = null;

        // Start drag with a lightweight payload (only ids).
        var data = new DataObject();
        data.SetData(DragEntryIdsFormat, ids.ToArray());

        try
        {
            DragDrop.DoDragDrop(Grid, data, DragDropEffects.Move);
        }
        catch
        {
            // Ignore drag exceptions (e.g., during shutdown).
        }
        finally
        {
            // Ensure any hover state on the folders tree is cleared when drag operation ends anywhere.
            ClearDragDropHover();
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        // Creating new entries is only allowed when a target context is chosen.
        // Context is separate from selection: when context is not selected, we show 0 entries and must not allow Add.
        if (!CanCreateEntry)
            return;

        // Show the folder location under "Comment" in the entry dialog.
        // For a new entry, use the current ACTIVE context (where the right list is).
        var addLoc = GetLocationForActiveContext();
        var result = ShowHostedEntryDialog(null, addLoc);
        if (result == null)
            return;

        // Assign folder based on ACTIVE context (what entries list is currently showing)
        if (_activeFolderNode?.Kind == FolderNodeKind.Folder)
            result.FolderId = _activeFolderNode.Id;
        else if (_activeFolderNode?.Kind == FolderNodeKind.NoFolder)
            result.FolderId = null;

        var list = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();
        list.Add(result);
        _vault.Entries = list.ToArray();

        _store.Save(_masterPassword, _vault);
        RefreshGrid();
    }

    private bool EnsureEntryAccessible(VaultEntry entry)
    {
        if (entry == null)
            return false;

        if (entry.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return false;
        }

        return true;
    }


    private static void StopHostedDialogFrames(params DispatcherFrame?[] frames)
    {
        foreach (var frame in frames)
        {
            if (frame != null)
                frame.Continue = false;
        }
    }

    private static Func<MessageBoxResult, bool> CreateHostedDialogFrameStopper(params DispatcherFrame?[] frames)
        => _ =>
        {
            StopHostedDialogFrames(frames);
            return false;
        };

    private Action CreateTrashUiCommitAction()
        => () =>
        {
            RefreshActiveContextUi();
            RefreshGrid();
            ClearEntriesSelectionSafe();
        };

    private static VaultEntry CloneVaultEntry(VaultEntry source)
        => new()
        {
            Id = source.Id,
            Title = source.Title,
            Username = source.Username,
            Password = source.Password,
            Url = source.Url,
            Comment = source.Comment,
            IsFavorite = source.IsFavorite,
            IsDeleted = source.IsDeleted,
            DeletedAtUtc = source.DeletedAtUtc,
            DeletedFromFolderId = source.DeletedFromFolderId,
            FolderId = source.FolderId,
            UpdatedUtc = source.UpdatedUtc,
            UiFolderPath = source.UiFolderPath
        };

    private (bool Changed, string? ErrorMessage, Action? UiCommit, string? InfoMessage) TryRestoreTrashEntriesToNoFolder(HashSet<Guid> selectedIds)
    {
        if (selectedIds == null || selectedIds.Count == 0)
            return (false, null, null, null);

        var currentEntries = _vault.Entries ?? Array.Empty<VaultEntry>();
        var updatedEntries = new List<VaultEntry>(currentEntries.Length);
        var now = DateTime.UtcNow;
        bool changed = false;

        foreach (var source in currentEntries)
        {
            if (!selectedIds.Contains(source.Id) || !source.IsDeleted)
            {
                updatedEntries.Add(source);
                continue;
            }

            var restored = CloneVaultEntry(source);
            restored.IsDeleted = false;
            restored.DeletedAtUtc = null;
            restored.DeletedFromFolderId = null;
            restored.FolderId = null;
            restored.UpdatedUtc = now;

            updatedEntries.Add(restored);
            changed = true;
        }

        if (!changed)
            return (false, null, null, null);

        var updatedVault = new VaultData
        {
            Version = _vault.Version,
            Entries = updatedEntries.ToArray(),
            Folders = _vault.Folders,
            Attachments = _vault.Attachments
        };

        try
        {
            _store.Save(_masterPassword, updatedVault);
            _vault = updatedVault;
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null, null);
        }

        var noFolderName = GetNoFolderDisplayName();
        var infoMessage = selectedIds.Count == 1
            ? string.Format(Loc.Instance["TrashRestoredToNoFolder"], noFolderName)
            : string.Format(Loc.Instance["TrashRestoredToNoFolderMany"], selectedIds.Count, noFolderName);

        return (true, null, CreateTrashUiCommitAction(), infoMessage);
    }

    private (bool Changed, string? ErrorMessage, Action? UiCommit) TryDeleteTrashEntriesForever(HashSet<Guid> selectedIds)
    {
        if (selectedIds == null || selectedIds.Count == 0)
            return (false, null, null);

        var currentEntries = _vault.Entries ?? Array.Empty<VaultEntry>();
        var updatedEntries = currentEntries.Where(x => !selectedIds.Contains(x.Id)).ToArray();
        if (updatedEntries.Length == currentEntries.Length)
            return (false, null, null);

        var currentAttachments = _vault.Attachments ?? Array.Empty<VaultAttachment>();
        var attachmentsToDelete = currentAttachments
            .Where(a => selectedIds.Contains(a.EntryId))
            .ToList();
        var updatedAttachments = currentAttachments
            .Where(a => !selectedIds.Contains(a.EntryId))
            .ToArray();

        var updatedVault = new VaultData
        {
            Version = _vault.Version,
            Entries = updatedEntries,
            Folders = _vault.Folders,
            Attachments = updatedAttachments
        };

        try
        {
            _store.Save(_masterPassword, updatedVault);
            _vault = updatedVault;
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }

        foreach (var attachment in attachmentsToDelete)
        {
            try
            {
                var path = AttachmentsStore.GetAttachmentBlobPath(_store.Path, attachment.Id);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // best-effort
            }
        }

        return (true, null, CreateTrashUiCommitAction());
    }

    private void StartHostedTrashRestoreConfirmationFlow(HashSet<Guid> selectedIds, string message)
    {
        ShowHostedAppMessageDialog(
            Loc.Instance["Info"],
            message,
            MessageBoxButton.YesNo,
            dialogResult =>
            {
                if (dialogResult != MessageBoxResult.Yes)
                    return false;

                var confirmFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;
                StartHostedTrashRestoreFlow(selectedIds, replaceCurrentModal: true, outerFrameToStop: confirmFrame);
                return true;
            });
    }

    private void StartHostedTrashRestoreFlow(HashSet<Guid> selectedIds, bool replaceCurrentModal = false, DispatcherFrame? outerFrameToStop = null)
    {
        ShowHostedMasterPasswordPromptDialog(
            Loc.Instance["TrashRestoreTitle"],
            Loc.Instance["TrashRestorePrompt"],
            value => TryVerifyMasterPassword(value) ? null : Loc.Instance["BadPassword"],
            _ =>
            {
                var passwordFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;
                var outcome = TryRestoreTrashEntriesToNoFolder(selectedIds);

                if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
                {
                    ShowHostedAppMessageDialog(
                        Loc.Instance["Error"],
                        outcome.ErrorMessage,
                        MessageBoxButton.OK,
                        CreateHostedDialogFrameStopper(passwordFrame),
                        replaceCurrentModal: true);
                    return true;
                }

                if (!outcome.Changed)
                    return false;

                if (outcome.UiCommit != null)
                    QueuePendingHostedUiCommit(outcome.UiCommit);

                ShowHostedAppMessageDialog(
                    Loc.Instance["Info"],
                    outcome.InfoMessage ?? string.Empty,
                    MessageBoxButton.OK,
                    CreateHostedDialogFrameStopper(passwordFrame),
                    replaceCurrentModal: true);
                return true;
            },
            replaceCurrentModal);

        StopHostedDialogFrames(outerFrameToStop);
    }

    private void StartHostedTrashDeleteForeverFlow(HashSet<Guid> selectedIds, string confirmText)
    {
        ShowHostedAppMessageDialog(
            Loc.Instance["AppTitle"],
            confirmText,
            MessageBoxButton.YesNo,
            dialogResult =>
            {
                if (dialogResult != MessageBoxResult.Yes)
                    return false;

                var confirmFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;

                ShowHostedMasterPasswordPromptDialog(
                    Loc.Instance["TrashRestoreTitle"],
                    Loc.Instance["TrashRestorePrompt"],
                    value => TryVerifyMasterPassword(value) ? null : Loc.Instance["BadPassword"],
                    _ =>
                    {
                        var passwordFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;
                        var outcome = TryDeleteTrashEntriesForever(selectedIds);

                        if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
                        {
                            ShowHostedAppMessageDialog(
                                Loc.Instance["Error"],
                                outcome.ErrorMessage,
                                MessageBoxButton.OK,
                                CreateHostedDialogFrameStopper(passwordFrame),
                                replaceCurrentModal: true);
                            return true;
                        }

                        if (outcome.UiCommit != null)
                            QueuePendingHostedUiCommit(outcome.UiCommit);

                        return false;
                    },
                    replaceCurrentModal: true);

                StopHostedDialogFrames(confirmFrame);
                return true;
            });
    }

    private void TryRestoreFromTrashByDoubleClick(VaultEntry entry)
    {
        if (entry == null)
            return;

        try
        {
            var msg = $"{Loc.Instance["TrashOnlyRestore"]}\n\n{Loc.Instance["TrashRestoreAsk"]}";
            StartHostedTrashRestoreConfirmationFlow(new HashSet<Guid> { entry.Id }, msg);
        }
        catch
        {
            // ignore
        }
    }


    private void OpenEntryEditor(VaultEntry sel)
    {
        if (!EnsureEntryAccessible(sel))
            return;

        // Show the folder location under "Comment" in the entry dialog.
        var editLoc = GetLocationForEntry(sel);
        var result = ShowHostedEntryDialog(sel, editLoc);
        if (result == null)
            return;

        var list = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();
        var idx = list.FindIndex(x => x.Id == sel.Id);
        if (idx >= 0) list[idx] = result;
        _vault.Entries = list.ToArray();

        _store.Save(_masterPassword, _vault);
        RefreshGrid();
    }

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel is null)
        {
            // Avoid MessageBox here (it steals focus and can cause UI flicker).
            ShowCopyToast(NeedSelectCopyLoginToastPopup);
            return;
        }

        OpenEntryEditor(sel);
    }

    /// <summary>
    /// Called from the entry editor when user clicks the folder name under "Comment".
    /// This navigates the right side to that folder context and highlights it in the tree.
    /// </summary>
    public void NavigateToFolderContextFromEntry(Guid? folderId)
    {
        SafeUi(nameof(NavigateToFolderContextFromEntry), () =>
        {
            if (folderId == null)
            {
                _activeFolderNode = _folderTreeRoots.FirstOrDefault(x => x.Kind == FolderNodeKind.NoFolder);
            }
            else
            {
                var node = FindNodeById(folderId.Value);
                if (node == null)
                    return;
                _activeFolderNode = node;
            }

            UpdateActiveContextBindings();
            RefreshGrid();
            FocusActiveContextInTree();
        });
    }

    private (string displayName, Guid? folderId, bool isMissing) GetLocationForEntry(VaultEntry entry)
    {
        if (entry.FolderId == null)
            return (GetNoFolderDisplayName(), null, false);

        var node = FindNodeById(entry.FolderId.Value);
        if (node != null)
            return (node.Name, node.Id, false);

        // Folder was removed or is missing.
        return (Loc.Instance["FolderNotFound"], entry.FolderId, true);
    }

    private (string displayName, Guid? folderId, bool isMissing) GetLocationForActiveContext()
    {
        if (_activeFolderNode == null)
            return ("", null, false);

        if (_activeFolderNode.Kind == FolderNodeKind.NoFolder)
            return (GetNoFolderDisplayName(), null, false);

        if (_activeFolderNode.Kind == FolderNodeKind.Folder)
            return (_activeFolderNode.Name, _activeFolderNode.Id, false);

        return ("", null, false);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedEntries();
    }

    

    private void TrashRestore_Click(object sender, RoutedEventArgs e)
    {
        RestoreSelectedTrashEntries();
    }


    void TrashDeleteForever_Click(object sender, RoutedEventArgs e)
    {
        DeleteForeverSelectedTrashEntries();
    }

    void TrashEmpty_Click(object sender, RoutedEventArgs e)
    {
        EmptyTrashForever();
    }
private void DeleteSelectedEntries()
    {
        // Use the MVVM-synced list (SelectedEntries) first; fall back to DataGrid if needed.
        var selected = SelectedEntries.Count > 0
            ? SelectedEntries.ToList()
            : (Grid.SelectedItems?.Cast<VaultEntry>().ToList() ?? new System.Collections.Generic.List<VaultEntry>());

        if (selected.Count == 0)
        {
            // Avoid MessageBox here (it steals focus and can cause UI flicker).
            ShowCopyToast(NeedSelectCopyPasswordToastPopup);
            return;
        }

        bool inTrashContext = _activeFolderNode?.Kind == FolderNodeKind.Trash;

        // In Trash, Delete means "Delete forever" (with master-password confirmation).
        if (inTrashContext)
        {
            DeleteForeverSelectedTrashEntries();
            return;
        }

        // Trash entries are protected in normal context (should not happen, but keep as guard).
        if (selected.Any(x => x.IsDeleted))
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return;
        }

        string confirmText = selected.Count == 1
            ? Loc.Instance["ConfirmDelete"]
            : string.Format(Loc.Instance["ConfirmDeleteMany"], selected.Count);

        if (AppMessageDialogWindow.ShowYesNo(this, Loc.Instance["AppTitle"], confirmText) != MessageBoxResult.Yes)
            return;
        var selectedIds = selected.Select(x => x.Id).ToHashSet();
        var list = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();

        bool changed = false;
        foreach (var it in list)
        {
            if (!selectedIds.Contains(it.Id))
                continue;

            if (it.IsDeleted)
                continue;

            it.IsDeleted = true;
            it.DeletedAtUtc = DateTime.UtcNow;
            it.DeletedFromFolderId = it.FolderId;
            // Remove from normal folder context immediately.
            it.FolderId = null;
            it.UpdatedUtc = DateTime.UtcNow;

            changed = true;
        }

        if (!changed)
            return;

        _vault.Entries = list.ToArray();

        _store.Save(_masterPassword, _vault);

        // Update special captions ("Favorites (N)") and active context title.
        RefreshActiveContextHeaders();

        RefreshGrid();

        Grid?.UnselectAll();
    }

    private void RestoreSelectedTrashEntries()
    {
        // Use the MVVM-synced list (SelectedEntries) first; fall back to DataGrid if needed.
        var selected = SelectedEntries.Count > 0
            ? SelectedEntries.ToList()
            : (Grid.SelectedItems?.Cast<VaultEntry>().ToList() ?? new System.Collections.Generic.List<VaultEntry>());

        if (selected.Count == 0)
        {
            // Avoid MessageBox here (it steals focus and can cause UI flicker).
            ShowCopyToast(NeedSelectCopyLoginToastPopup);
            return;
        }

        // Restore is only meaningful for deleted entries.
        if (!selected.Any(x => x.IsDeleted) && _activeFolderNode?.Kind != FolderNodeKind.Trash)
            return;

        if (!IsUnlocked)
            return;

        var selectedIds = selected.Select(x => x.Id).ToHashSet();
        StartHostedTrashRestoreFlow(selectedIds);
    }

    private void DeleteForeverSelectedTrashEntries()
    {
        // Use the MVVM-synced list (SelectedEntries) first; fall back to DataGrid selection if needed.
        var selected = SelectedEntries.Count > 0
            ? SelectedEntries.ToList()
            : (Grid.SelectedItems?.Cast<VaultEntry>().ToList() ?? new System.Collections.Generic.List<VaultEntry>());

        if (selected.Count == 0)
        {
            // Avoid MessageBox here (it steals focus and can cause UI flicker).
            ShowCopyToast(NeedSelectCopyLoginToastPopup);
            return;
        }

        // Only deleted entries can be removed forever.
        var selectedDeleted = selected.Where(x => x.IsDeleted).ToList();
        if (selectedDeleted.Count == 0)
            return;

        if (!IsUnlocked)
            return;

        string confirmText = selectedDeleted.Count == 1
            ? Loc.Instance["ConfirmDeleteForever"]
            : string.Format(Loc.Instance["ConfirmDeleteForeverMany"], selectedDeleted.Count);

        var selectedIds = selectedDeleted.Select(x => x.Id).ToHashSet();
        StartHostedTrashDeleteForeverFlow(selectedIds, confirmText);
    }

    private void EmptyTrashForever()
    {
        if (!IsUnlocked)
            return;

        var trash = (_vault.Entries ?? Array.Empty<VaultEntry>())
            .Where(x => x.IsDeleted)
            .ToList();
        if (trash.Count == 0)
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["EmptyTrashNothing"]);
            return;
        }

        string confirmText = string.Format(Loc.Instance["ConfirmEmptyTrash"], trash.Count);

        var trashIds = trash.Select(x => x.Id).ToHashSet();
        StartHostedTrashDeleteForeverFlow(trashIds, confirmText);
    }



    
    private void FavAdd_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedEntriesFavorite(true);
    }

    private void FavRemove_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedEntriesFavorite(false);
    }

    private void SetSelectedEntriesFavorite(bool isFavorite)
    {
        var selected = SelectedEntries.Count > 0
            ? SelectedEntries.ToList()
            : (Grid.SelectedItems?.Cast<VaultEntry>().ToList() ?? new System.Collections.Generic.List<VaultEntry>());

        if (selected.Count == 0)
        {
            // Avoid MessageBox here (it steals focus and can cause UI flicker).
            ShowCopyToast(NeedSelectCopyLoginToastPopup);
            return;
        }

        if (_activeFolderNode?.Kind == FolderNodeKind.Trash || selected.Any(x => x.IsDeleted))
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return;
        }

        var ids = selected.Select(x => x.Id).ToHashSet();
        var list = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();

        bool changed = false;
        foreach (var it in list)
        {
            if (!ids.Contains(it.Id))
                continue;

            if (it.IsFavorite != isFavorite)
            {
                it.IsFavorite = isFavorite;
                changed = true;
            }
        }

        if (!changed)
            return;

        _vault.Entries = list.ToArray();
        _store.Save(_masterPassword, _vault);

        // Update "Favorites (N)" caption and active context title.
        RefreshActiveContextUi();

        RefreshGrid();
    }

    private void FavoriteStar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Handle toggling manually so selection stays stable and we don't depend on Button's Click
        // (we mark MouseDown handled).
        e.Handled = true;

        if (sender is Button btn && btn.DataContext is VaultEntry entry)
            ToggleFavorite(entry);
    }

    private void ToggleFavorite(VaultEntry entry)
    {
        if (entry == null)
            return;

        if (entry.IsDeleted || _activeFolderNode?.Kind == FolderNodeKind.Trash)
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return;
        }

        entry.IsFavorite = !entry.IsFavorite;

        try
        {
            _store.Save(_masterPassword, _vault);
        }
        catch (Exception ex)
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], ex.Message);
            return;
        }

        RefreshActiveContextUi();

        RefreshGrid();
    }

    private void FavoriteStar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent DataGrid from changing selection when clicking the star.
        // This keeps multi-selection stable.
        e.Handled = true;

        Grid?.Focus();
    }

private void SelectAllEntries()
    {
        try
        {
            Grid.SelectAll();
            Grid.Focus();
        }
        catch { /* ignore */ }
    }

    private enum ClipboardCopyType
    {
        Text,
        Secret,
        Login
    }

    private void CopyUsername_Click(object sender, RoutedEventArgs e)
    {
        var source = sender is MenuItem ? "context_menu" : "toolbar";
        var cursorPoint = sender is MenuItem ? Mouse.GetPosition(GetToastAnchorRoot()) : (Point?)null;

        TryCopySelectedEntry(
            sel => sel.Username,
            copyType: ClipboardCopyType.Login,
            okPopup: CopyLoginToastPopup,
            needSelectPopup: NeedSelectCopyLoginToastPopup,
            failedPopup: CopyLoginFailedToastPopup,
            source: source,
            cursorPoint: cursorPoint);
    }


    private void EnsureSelectionVisible(VaultEntry sel)
    {
        // Restore selection & focus (MessageBox and toolbar buttons can steal focus)
        Grid.SelectedItem = sel;
        Grid.ScrollIntoView(sel);
        Grid.Focus();
    }


    private readonly struct PopupPlacementSnapshot
    {
        public readonly UIElement? PlacementTarget;
        public readonly PlacementMode Placement;
        public readonly double HorizontalOffset;
        public readonly double VerticalOffset;
        public readonly Rect PlacementRectangle;

        public PopupPlacementSnapshot(Popup popup)
        {
            PlacementTarget = popup.PlacementTarget;
            Placement = popup.Placement;
            HorizontalOffset = popup.HorizontalOffset;
            VerticalOffset = popup.VerticalOffset;
            PlacementRectangle = popup.PlacementRectangle;
        }

        public void Restore(Popup popup)
        {
            try
            {
                popup.PlacementTarget = PlacementTarget;
                popup.Placement = Placement;
                popup.HorizontalOffset = HorizontalOffset;
                popup.VerticalOffset = VerticalOffset;
                popup.PlacementRectangle = PlacementRectangle;
            }
            catch { }
        }
    }

    private FrameworkElement GetToastAnchorRoot()
        => Content as FrameworkElement ?? this;

    private static Size MeasurePopupDesiredSize(Popup popup)
    {
        try
        {
            if (popup.Child is FrameworkElement fe)
            {
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return fe.DesiredSize;
            }
        }
        catch { }

        return new Size(220, 44); // defensive fallback
    }

    private void ShowCopyToast(Popup popup, Point? cursorPoint = null)
    {
        if (cursorPoint is null)
        {
            _copyToast.Show(popup);
            return;
        }

        // Defer to allow context menu to close before showing the toast.
        Dispatcher.BeginInvoke(() =>
        {
            var root = GetToastAnchorRoot();
            var rootW = root.ActualWidth;
            var rootH = root.ActualHeight;

            // If window not yet measured (rare), fall back to the default anchored placement.
            if (rootW <= 1 || rootH <= 1)
            {
                _copyToast.Show(popup);
                return;
            }

            var desired = MeasurePopupDesiredSize(popup);
            var w = Math.Max(40, desired.Width);
            var h = Math.Max(24, desired.Height);

            var p = cursorPoint.Value;

            const double offset = 12;
            const double pad = 8;

            // Default: bottom-right of cursor.
            var x = p.X + offset;
            var y = p.Y + offset;

            // Flip if near right/bottom edge.
            if (x + w > rootW - pad)
                x = p.X - offset - w;
            if (y + h > rootH - pad)
                y = p.Y - offset - h;

            // Clamp to visible bounds.
            x = Math.Max(pad, Math.Min(x, rootW - w - pad));
            y = Math.Max(pad, Math.Min(y, rootH - h - pad));

            var snap = new PopupPlacementSnapshot(popup);

            try
            {
                popup.PlacementTarget = root;
                popup.Placement = PlacementMode.Relative;
                popup.HorizontalOffset = x;
                popup.VerticalOffset = y;
                popup.PlacementRectangle = Rect.Empty;
            }
            catch
            {
                // If we can't override placement, fall back to default.
                _copyToast.Show(popup);
                return;
            }

            _copyToast.Show(popup, onClose: () => snap.Restore(popup));

        }, DispatcherPriority.Background);
    }

    private void TryCopySelectedEntry(
        Func<VaultEntry, string?> textFactory,
        ClipboardCopyType copyType,
        Popup okPopup,
        Popup needSelectPopup,
        Popup failedPopup,
        string source,
        Point? cursorPoint)
    {
        var sel = Selected();
        if (sel is null)
        {
            // Defensive fallback: normally UI disables the action when there is no single selection.
            ShowCopyToast(needSelectPopup, cursorPoint);
            return;
        }

        if (!EnsureEntryAccessible(sel))
            return;

        var text = textFactory(sel) ?? "";

        DiagnosticsLog.AppendLine("CLIPBOARD_COPY_BEGIN", $"type={copyType.ToString().ToLowerInvariant()} source={source}");

        bool ok;
        string? failure;

        if (copyType == ClipboardCopyType.Secret)
            ok = ClipboardSecurity.TryCopySecret(text, out failure);
        else if (copyType == ClipboardCopyType.Login)
            ok = ClipboardSecurity.TryCopyLogin(text, out failure);
        else
            ok = ClipboardSecurity.TryCopyText(text, out failure);

        EnsureSelectionVisible(sel);

        if (ok)
        {
            DiagnosticsLog.AppendLine("CLIPBOARD_COPY_END", $"type={copyType.ToString().ToLowerInvariant()} source={source} result=ok");
            ShowCopyToast(okPopup, cursorPoint);
        }
        else
        {
            DiagnosticsLog.AppendLine("CLIPBOARD_COPY_END", $"type={copyType.ToString().ToLowerInvariant()} source={source} result=fail reason={(failure ?? "unknown")}");
            ShowCopyToast(failedPopup, cursorPoint);
        }
    }

    private void CopyPassword_Click(object sender, RoutedEventArgs e)
    {
        var source = sender is MenuItem ? "context_menu" : "toolbar";
        var cursorPoint = sender is MenuItem ? Mouse.GetPosition(GetToastAnchorRoot()) : (Point?)null;

        TryCopySelectedEntry(
            sel => sel.Password,
            copyType: ClipboardCopyType.Secret,
            okPopup: CopyPasswordToastPopup,
            needSelectPopup: NeedSelectCopyPasswordToastPopup,
            failedPopup: CopyPasswordFailedToastPopup,
            source: source,
            cursorPoint: cursorPoint);
    }

    private void ToggleUpdatedColumn_Click(object sender, RoutedEventArgs e)
    {
        if (UpdatedUtcColumn == null)
            return;

        SetUpdatedColumnVisible(!IsUpdatedColumnVisible());

        // Keep check state consistent even if something else changed the column.
        if (sender is MenuItem mi)
            mi.IsChecked = IsUpdatedColumnVisible();
    }

    private void UpdatedColumnHeaderMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu cm)
            return;

        bool isVisible = IsUpdatedColumnVisible();

        foreach (var obj in cm.Items)
        {
            if (obj is MenuItem mi && string.Equals(mi.Tag as string, "ToggleUpdatedColumn", StringComparison.OrdinalIgnoreCase))
            {
                mi.IsChecked = isVisible;
                mi.IsEnabled = true;
            }
        }
    }


    private void GoToEntryFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!IsUnlocked)
            return;

        VaultEntry? sel = null;
        try
        {
            sel = Selected();
            if (sel == null && SelectedEntries.Count == 1)
                sel = SelectedEntries[0];
            if (sel == null && Grid.SelectedItem is VaultEntry ve)
                sel = ve;
        }
        catch
        {
            // ignore
        }

        if (sel is null)
        {
            // Avoid MessageBox here (it steals focus and can cause UI flicker).
            ShowCopyToast(NeedSelectCopyLoginToastPopup);
            return;
        }

        // "Go to folder" is meant to leave global search results and show the actual folder contents.
        // Clear search query first so RefreshGrid will not keep showing search results.
        try
        {
            if (SearchBox != null && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                _entrySearchDebounceTimer?.Stop();
                SearchBox.Clear();
            }
        }
        catch
        {
            // ignore
        }

        // Validate missing folder early to provide a clear message.
        if (sel.FolderId != null)
        {
            var node = FindNodeById(sel.FolderId.Value);
            if (node == null)
            {
                AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["FolderNotFound"]);
                return;
            }
        }

        NavigateToFolderContextFromEntry(sel.FolderId);
    }


    private void GridContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (Grid.ContextMenu == null) return;

        var rawSearch = (SearchBox?.Text ?? "").Trim();
        var isSearchActive = SplitSearchTokens(rawSearch).Length > 0;

        // Use MVVM-synced selection first; fall back to DataGrid selection if needed.
        var selected = SelectedEntries.Count > 0
            ? SelectedEntries.ToList()
            : (Grid.SelectedItems?.Cast<VaultEntry>().ToList() ?? new System.Collections.Generic.List<VaultEntry>());

        int selCount = selected.Count;
        bool hasAny = selCount > 0;
        bool hasSingle = selCount == 1;

        bool anyFav = hasAny && selected.Any(x => x.IsFavorite);
        bool anyNotFav = hasAny && selected.Any(x => !x.IsFavorite);

        // Add is allowed only when there is an explicit target context.
        bool canAdd = CanCreateEntry;

        // Keep context menu behavior aligned with toolbar: entry actions require unlocked state.
        bool canOperate = IsUnlocked;

        bool inTrashContext = _activeFolderNode?.Kind == FolderNodeKind.Trash;


        foreach (var obj in Grid.ContextMenu.Items)
        {
            // Toggle "Go to folder" item and its separators only for global-search results.
            if (obj is FrameworkElement fe && fe.Tag is string feTag)
            {
                if (string.Equals(feTag, "GoToEntryFolder", StringComparison.OrdinalIgnoreCase))
                {
                    if (obj is MenuItem go)
                    {
                        go.Visibility = isSearchActive ? Visibility.Visible : Visibility.Collapsed;
                        go.IsEnabled = isSearchActive && hasSingle && IsUnlocked;
                    }
                    continue;
                }

                if (string.Equals(feTag, "GoToEntryFolderSepBefore", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(feTag, "GoToEntryFolderSepAfter", StringComparison.OrdinalIgnoreCase))
                {
                    fe.Visibility = isSearchActive ? Visibility.Visible : Visibility.Collapsed;
                    continue;
                }
            }

            if (obj is MenuItem mi)
            {
                var tag = mi.Tag as string;

                if (string.Equals(tag, "ToggleUpdatedColumn", StringComparison.OrdinalIgnoreCase))
                {
                    mi.IsEnabled = true;
                    mi.IsChecked = IsUpdatedColumnVisible();
                    continue;
                }


                if (string.Equals(tag, "TrashRestore", StringComparison.OrdinalIgnoreCase))
                {
                    mi.Visibility = inTrashContext ? Visibility.Visible : Visibility.Collapsed;
                    mi.IsEnabled = inTrashContext && hasAny && IsUnlocked;
                    continue;
                }

                if (string.Equals(tag, "TrashDeleteForever", StringComparison.OrdinalIgnoreCase))
                {
                    mi.Visibility = inTrashContext ? Visibility.Visible : Visibility.Collapsed;
                    mi.IsEnabled = inTrashContext && hasAny && IsUnlocked;
                    continue;
                }

                if (string.Equals(tag, "TrashEmpty", StringComparison.OrdinalIgnoreCase))
                {
                    mi.Visibility = inTrashContext ? Visibility.Visible : Visibility.Collapsed;

                    int trashCount = 0;
                    try { trashCount = (_vault.Entries ?? Array.Empty<VaultEntry>()).Count(x => x.IsDeleted); } catch { }
                    mi.IsEnabled = inTrashContext && IsUnlocked && trashCount > 0;
                    continue;
                }

                // In trash context, most actions are disabled: entries are not viewable/modifiable.
                if (inTrashContext)
                {
                    if (string.Equals(tag, "Add", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "Edit", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "Delete", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "FavAdd", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "FavRemove", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "CopyUsername", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "CopyPassword", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "CopyUrl", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tag, "OpenUrlInBrowser", StringComparison.OrdinalIgnoreCase))
                    {
                        mi.IsEnabled = false;
                        continue;
                    }
                }

                if (string.Equals(tag, "Add", StringComparison.OrdinalIgnoreCase))
                {
                    mi.IsEnabled = canAdd && canOperate;
                }
                else if (string.Equals(tag, "Delete", StringComparison.OrdinalIgnoreCase))
                {
                    mi.IsEnabled = hasAny && canOperate;
                }
                else if (string.Equals(tag, "FavAdd", StringComparison.OrdinalIgnoreCase))
                {
                    mi.IsEnabled = anyNotFav && canOperate;
                }
                else if (string.Equals(tag, "FavRemove", StringComparison.OrdinalIgnoreCase))
                {
                    mi.IsEnabled = anyFav && canOperate;
                }
                else if (string.Equals(tag, "Edit", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(tag, "CopyUsername", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(tag, "CopyPassword", StringComparison.OrdinalIgnoreCase))
                {
                    mi.IsEnabled = hasSingle && canOperate;
                }
                else if (string.Equals(tag, "CopyUrl", StringComparison.OrdinalIgnoreCase))
                {
                    var selectedEntry = hasSingle ? selected[0] : null;
                    mi.IsEnabled = hasSingle && canOperate && selectedEntry != null && !selectedEntry.IsDeleted && EntryUrlActions.CanCopy(selectedEntry.Url);
                }
                else if (string.Equals(tag, "OpenUrlInBrowser", StringComparison.OrdinalIgnoreCase))
                {
                    var selectedEntry = hasSingle ? selected[0] : null;
                    mi.IsEnabled = hasSingle && canOperate && selectedEntry != null && !selectedEntry.IsDeleted && EntryUrlActions.CanOpenInBrowser(selectedEntry.Url);
                }
                else
                {
                    mi.IsEnabled = hasAny && canOperate;
                }
            }
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        ShowChangePasswordDialog();
    }

    private void ShowChangePasswordDialog()
    {
        var handledInDialog = false;
        var hostedResult = ShowHostedChangePasswordDialog(passwords =>
        {
            handledInDialog = true;
            var changePasswordFrame = _hostedDialogModalFrames.Count > 0 ? _hostedDialogModalFrames.Peek() : null;

            bool StopChangePasswordFrame(MessageBoxResult _)
            {
                if (changePasswordFrame != null)
                    changePasswordFrame.Continue = false;

                return false;
            }

            if (passwords.OldPassword != _masterPassword)
            {
                ShowHostedAppMessageDialog(
                    Loc.Instance["Error"],
                    Loc.Instance["OldPasswordWrong"],
                    MessageBoxButton.OK,
                    StopChangePasswordFrame,
                    replaceCurrentModal: true);
                return true;
            }

            try
            {
                // Re-encrypt the current in-memory vault with the new password
                _store.Save(passwords.NewPassword, _vault);
                _masterPassword = passwords.NewPassword;

                ShowHostedAppMessageDialog(
                    Loc.Instance["Info"],
                    Loc.Instance["PasswordChanged"],
                    MessageBoxButton.OK,
                    StopChangePasswordFrame,
                    replaceCurrentModal: true);
                return true;
            }
            catch (Exception ex)
            {
                ShowHostedAppMessageDialog(
                    Loc.Instance["Error"],
                    ex.Message,
                    MessageBoxButton.OK,
                    StopChangePasswordFrame,
                    replaceCurrentModal: true);
                return true;
            }
        });

        if (handledInDialog)
            return;

        if (hostedResult == null)
            return;

        var oldPassword = hostedResult.Value.OldPassword;
        var newPassword = hostedResult.Value.NewPassword;

        if (oldPassword != _masterPassword)
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], Loc.Instance["OldPasswordWrong"]);
            return;
        }

        try
        {
            // Re-encrypt the current in-memory vault with the new password
            _store.Save(newPassword, _vault);
            _masterPassword = newPassword;

            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["PasswordChanged"]);
        }
        catch (Exception ex)
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Error"], ex.Message);
        }
    }
    private void OpenGenerator_Click(object sender, RoutedEventArgs e)
    {
        ShowHostedPasswordGeneratorDialog();
    }

    // -----------------------------
    // Folders
    // -----------------------------

        private void BuildFolderTree()
    {
        _folderTreeRoots.Clear();

        // Special nodes (virtual):
        var favoritesNode = new FolderNode(FolderNodeKind.Favorites, GetFavoritesDisplayName());
        var trashNode = new FolderNode(FolderNodeKind.Trash, GetTrashDisplayName());
        var noFolderNode = new FolderNode(FolderNodeKind.NoFolder, GetNoFolderDisplayName());
        _folderTreeRoots.Add(favoritesNode);
        _folderTreeRoots.Add(trashNode);
        _folderTreeRoots.Add(noFolderNode);

        // Create nodes for each folder
        var folders = _vault.Folders ?? Array.Empty<VaultFolder>();
        var byId = folders.ToDictionary(
            f => f.Id,
            f => new FolderNode(FolderNodeKind.Folder, f.Name, f.Id, f.ParentId));

        // Build hierarchy and collect top-level folders directly in TreeView roots
        foreach (var folder in folders)
        {
            var node = byId[folder.Id];

            if (folder.ParentId is Guid parentId && byId.TryGetValue(parentId, out var parentNode))
                parentNode.Children.Add(node);
            else
                _folderTreeRoots.Add(node);
        }

        // Sort children of each top-level folder
        foreach (var r in _folderTreeRoots.Where(x => x.Kind == FolderNodeKind.Folder).ToList())
            SortChildrenRecursive(r);

        // Sort top-level folders by name but keep special nodes first
        var top = _folderTreeRoots.Where(x => x.Kind == FolderNodeKind.Folder).OrderBy(x => x.Name).ToList();
        _folderTreeRoots.Clear();
        _folderTreeRoots.Add(favoritesNode);
        _folderTreeRoots.Add(trashNode);
        _folderTreeRoots.Add(noFolderNode);
        foreach (var t in top) _folderTreeRoots.Add(t);

        // IMPORTANT (UX): compute a stable hint for expander visibility.
        // During some selection changes (notably clicking "Р‘РµР· РїР°РїРєРё" in folder multi-select mode),
        // WPF may temporarily rebuild TreeViewItem containers. If expander visibility relies on
        // collection-based checks, the arrow can disappear for some folders. To avoid that,
        // we compute this hint once from the underlying hierarchy and use it in the template.
        void UpdateHasChildHintRecursive(FolderNode n)
        {
            n.HasChildFoldersHint = n.Kind == FolderNodeKind.Folder && n.Children.Count > 0;
            foreach (var c in n.Children)
                UpdateHasChildHintRecursive(c);
        }

        foreach (var r in _folderTreeRoots)
            UpdateHasChildHintRecursive(r);

        UpdateFolderUiText();

        // Apply folder search filter (if user typed something)
        try
        {
            if (FolderSearchBox != null && !string.IsNullOrWhiteSpace(FolderSearchBox.Text))
                ApplyFolderSearchFilter(FolderSearchBox.Text);
            else
                foreach (var r in _folderTreeRoots) SetVisibleRecursive(r, true);
        }
        catch { /* ignore */ }

        // Re-map active context to the new tree instances (so captions/parents are correct after rebuild).
        if (_activeFolderNode != null)
            _activeFolderNode = FindNodeByIdentity(_activeFolderNode);

        NormalizeSelectedFolderNodeToSteadyState();

        // Keep selection if possible; otherwise keep no selection.
        // IMPORTANT: when active context is not set we show 0 entries (no "All entries" mode).
        SelectFolderNodeInTree(_selectedFolderNode);

        UpdateActiveContextBindings();

        // Tree rebuild resets checkmarks; keep multi-select strip in sync.
        UpdateCheckedFoldersState();
    }

    private void SortChildrenRecursive(FolderNode node)
    {
        if (node.Children.Count == 0) return;

        var ordered = node.Children.OrderBy(x => x.Name).ToList();
        node.Children.Clear();
        foreach (var c in ordered) node.Children.Add(c);

        foreach (var c in node.Children)
            SortChildrenRecursive(c);
    }

        private void UpdateFolderUiText()
    {
        // Special nodes captions.
        var favorites = _folderTreeRoots.FirstOrDefault(x => x.Kind == FolderNodeKind.Favorites);
        if (favorites != null)
            favorites.Name = GetFavoritesDisplayName();

        var trash = _folderTreeRoots.FirstOrDefault(x => x.Kind == FolderNodeKind.Trash);
        if (trash != null)
            trash.Name = GetTrashDisplayName();

        var noFolder = _folderTreeRoots.FirstOrDefault(x => x.Kind == FolderNodeKind.NoFolder);
        if (noFolder != null)
            noFolder.Name = GetNoFolderDisplayName();
    }

    private void SelectFolderNodeInTree(FolderNode? preferred)
    {
        // If preferred isn't present after rebuild, keep no selection.
        FolderNode? toSelect = preferred != null ? FindNodeByIdentity(preferred) : null;
        _selectedFolderNode = toSelect;

        // Try to select visually
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var tree = FolderTree;
            try
            {
                if (tree?.SelectedItem is object currentSelected)
                {
                    ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(tree, true);
                    var currentItem = FindTreeViewItem(tree, currentSelected);
                    if (currentItem != null)
                        currentItem.IsSelected = false;
                }

                if (toSelect != null)
                {
                    ExpandParents(toSelect);
                    var item = tree != null ? FindTreeViewItem(tree, toSelect) : null;
                    if (item != null)
                        item.IsSelected = true;
                }
            }
            catch { }
            finally
            {
                if (tree != null)
                    ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(tree, false);
            }
        }));

        RefreshGrid();
        UpdateFolderActionButtons();
        UpdateEntryActionButtons();
    }

        private FolderNode? FindNodeByIdentity(FolderNode node)
    {
        if (node.Kind == FolderNodeKind.Favorites)
            return _folderTreeRoots.FirstOrDefault(r => r.Kind == FolderNodeKind.Favorites);

        if (node.Kind == FolderNodeKind.Trash)
            return _folderTreeRoots.FirstOrDefault(r => r.Kind == FolderNodeKind.Trash);

        if (node.Kind == FolderNodeKind.NoFolder)
            return _folderTreeRoots.FirstOrDefault(r => r.Kind == FolderNodeKind.NoFolder);

        if (node.Kind == FolderNodeKind.Folder)
        {
            var id = node.Id;
            foreach (var root in _folderTreeRoots)
            {
                var found = FindFolderByIdRecursive(root, id);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private void ExpandParents(FolderNode node)
    {
        // Expand chain from top-level to the node
        var chain = GetAncestorChain(node);
        foreach (var n in chain)
        {
            var tvi = GetTreeViewItem(FolderTree, n);
            if (tvi != null) tvi.IsExpanded = true;
        }
    }

    private System.Collections.Generic.List<FolderNode> GetAncestorChain(FolderNode node)
    {
        var list = new System.Collections.Generic.List<FolderNode>();
        if (node.Kind != FolderNodeKind.Folder) return list;

        var folders = _vault.Folders ?? Array.Empty<VaultFolder>();
        var byId = folders.ToDictionary(f => f.Id, f => f);

        Guid? curParent = byId.TryGetValue(node.Id, out var me) ? me.ParentId : null;

        while (curParent != null)
        {
            var n = FindNodeById(curParent.Value);
            if (n != null) list.Add(n);

            curParent = byId.TryGetValue(curParent.Value, out var p) ? p.ParentId : null;
        }

        list.Reverse();
        return list;
    }

    private TreeViewItem? GetTreeViewItem(ItemsControl container, object item)
    {
        if (container == null) return null;

        var tvi = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
        if (tvi != null) return tvi;

        foreach (var child in container.Items)
        {
            var childControl = container.ItemContainerGenerator.ContainerFromItem(child) as ItemsControl;
            if (childControl == null) continue;
            tvi = GetTreeViewItem(childControl, item);
            if (tvi != null) return tvi;
        }

        return null;
    }


    // Find TreeViewItem for a given bound item (FolderNode). Wrapper around GetTreeViewItem.
    private TreeViewItem? FindTreeViewItem(ItemsControl container, object item)
        => GetTreeViewItem(container, item);

    // Recursively search for a folder node by its Id.
    private FolderNode? FindFolderByIdRecursive(FolderNode node, Guid id)
    {
        if (node.Id == id)
            return node;

        foreach (var child in node.Children)
        {
            var found = FindFolderByIdRecursive(child, id);
            if (found != null)
                return found;
        }

        return null;
    }

    // Search in current tree roots
    private FolderNode? FindNodeById(Guid id)
        => FindNodeById(_folderTreeRoots, id);

    private static FolderNode? FindNodeById(System.Collections.Generic.IEnumerable<FolderNode> roots, Guid id)
    {
        foreach (var root in roots)
        {
            if (root.Id == id)
                return root;

            foreach (var child in root.Children)
            {
                var found = FindNodeById(child, id);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private static FolderNode? FindNodeById(FolderNode node, Guid id)
    {
        if (node.Id == id)
            return node;

        foreach (var child in node.Children)
        {
            var found = FindNodeById(child, id);
            if (found != null)
                return found;
        }

        return null;
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        SafeUi("FolderTree_SelectedItemChanged", () =>
        {
            // In folder multi-select mode, folders must NOT become selected.
            // Users select folders only via checkboxes.
            if (IsFolderMultiSelectMode)
            {
                if (_suppressFolderTreeSelectionChange)
                    return;

                try
                {
                    _suppressFolderTreeSelectionChange = true;

                    // Prevent the selection change from activating/navigating the right pane.
                    if (sender is DependencyObject depSel)
                        ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(depSel, true);

                    // Immediately clear the visual selection.
                    if (sender is TreeView tree && e.NewValue != null)
                    {
                        var tvi = FindTreeViewItem(tree, e.NewValue);
                        if (tvi != null)
                            tvi.IsSelected = false;
                    }

                    _selectedFolderNode = null;
                    UpdateFolderActionButtons();
                    UpdateEntryActionButtons();
                }
                finally
                {
                    _suppressFolderTreeSelectionChange = false;
                }

                return;
            }

            // Selection in the tree is used for context actions (rename/delete/etc).
            // Active folder context controls which entries are displayed.
            // Explorer-like behavior: clearing selection or right-click selection must NOT "navigate" the entries list.
            bool suppressActivate = sender is DependencyObject dep &&
                                   ExplorerSelectionBehavior.GetSuppressTreeActivateNextSelectionChange(dep);

            if (sender is DependencyObject dep2)
                ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(dep2, false);

            _selectedFolderNode = e.NewValue as FolderNode;

            // Update active context ONLY when this selection change is not caused by
            // Explorer-like RMB selection or clearing selection on empty space.
            if (!suppressActivate && _selectedFolderNode != null)
                _activeFolderNode = _selectedFolderNode;

            UpdateFolderActionButtons();
            UpdateEntryActionButtons();
            RefreshGrid();
            UpdateActiveContextBindings();
        });
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T t)
                return t;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static DataGridRow? FindDataGridRowFromSource(DataGrid grid, DependencyObject? source)
    {
        if (source == null)
            return null;

        try
        {
            if (ItemsControl.ContainerFromElement(grid, source) is DataGridRow row)
                return row;

            if (ItemsControl.ContainerFromElement(grid, source) is DataGridCell cell)
                return FindVisualParent<DataGridRow>(cell);
        }
        catch
        {
            // Fall back to visual-parent walk below.
        }

        return FindVisualParent<DataGridRow>(source);
    }

    private static DataGridCell? FindDataGridCellFromSource(DataGrid grid, DependencyObject? source)
    {
        if (source == null)
            return null;

        try
        {
            if (ItemsControl.ContainerFromElement(grid, source) is DataGridCell cell)
                return cell;
        }
        catch
        {
            // Fall back to visual-parent walk below.
        }

        return FindVisualParent<DataGridCell>(source);
    }

    private static DependencyObject? ResolveGridEventSource(DataGrid grid, MouseEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        try
        {
            var hit = grid.InputHitTest(e.GetPosition(grid)) as DependencyObject;
            if (hit != null)
            {
                if (FindDataGridRowFromSource(grid, hit) != null || FindDataGridCellFromSource(grid, hit) != null)
                    return hit;
            }
        }
        catch
        {
            // Fall back to OriginalSource below.
        }

        return source;
    }

    private static bool IsGridEmbeddedButtonSource(DependencyObject? source)
        => FindVisualParent<Button>(source) != null;


    private void SetSingleGridSelection(VaultEntry entry)
    {
        DataGridRow? row = null;

        try { Grid.UnselectAll(); } catch { }
        try { Grid.SelectedItems.Clear(); } catch { }

        try
        {
            row = Grid.ItemContainerGenerator.ContainerFromItem(entry) as DataGridRow;
            if (row == null)
            {
                Grid.ScrollIntoView(entry);
                Grid.UpdateLayout();
                row = Grid.ItemContainerGenerator.ContainerFromItem(entry) as DataGridRow;
            }
        }
        catch { }

        try { Grid.SelectedItem = entry; } catch { }
        try { Grid.SelectedItems.Add(entry); } catch { }
        try { if (row != null) row.IsSelected = true; } catch { }
        try { Grid.UnselectAllCells(); } catch { }
        try { Grid.CurrentCell = new DataGridCellInfo(); } catch { }

        SyncSelectedEntriesFromGrid();
    }

    private void ClearDragDropHover()
{
    if (_dragHoverFolderNode != null)
    {
        _dragHoverFolderNode.IsDropTarget = false;
        _dragHoverFolderNode = null;
    }

    if (_dragAutoExpandTimer != null)
    {
        _dragAutoExpandTimer.Stop();
    }
}

private void SetDragDropHover(FolderNode? node)
{
    if (_dragHoverFolderNode == node)
        return;

    if (_dragHoverFolderNode != null)
        _dragHoverFolderNode.IsDropTarget = false;

    _dragHoverFolderNode = node;

    if (_dragHoverFolderNode != null)
    {
        _dragHoverFolderNode.IsDropTarget = true;
        StartDragAutoExpandTimerIfNeeded(_dragHoverFolderNode);
    }
    else
    {
        if (_dragAutoExpandTimer != null)
            _dragAutoExpandTimer.Stop();
    }
}

private void StartDragAutoExpandTimerIfNeeded(FolderNode node)
{
    // Auto-expand only real folders with child folders.
    if (node.Kind != FolderNodeKind.Folder)
    {
        if (_dragAutoExpandTimer != null)
            _dragAutoExpandTimer.Stop();
        return;
    }

    if (!node.HasChildFoldersHint || node.IsExpanded)
    {
        if (_dragAutoExpandTimer != null)
            _dragAutoExpandTimer.Stop();
        return;
    }

    _dragAutoExpandTimer ??= new System.Windows.Threading.DispatcherTimer();
    _dragAutoExpandTimer.Stop();
    _dragAutoExpandTimer.Interval = _dragAutoExpandDelay;
    _dragAutoExpandTimer.Tick -= DragAutoExpandTimer_Tick;
    _dragAutoExpandTimer.Tick += DragAutoExpandTimer_Tick;
    _dragAutoExpandTimer.Start();
}

private void DragAutoExpandTimer_Tick(object? sender, EventArgs e)
{
    if (_dragAutoExpandTimer != null)
        _dragAutoExpandTimer.Stop();

    var node = _dragHoverFolderNode;
    if (node == null)
        return;

    if (node.Kind == FolderNodeKind.Folder && node.HasChildFoldersHint && !node.IsExpanded)
        node.IsExpanded = true;
}



    private void ClearFolderTreeSelection()
    {
        if (FolderTree == null)
            return;

        // TreeView.SelectedItem is read-only, so clear selection via the generated container.
        if (FolderTree.SelectedItem is object selected)
        {
            if (_suppressFolderTreeSelectionChange)
                return;

            bool cleared = false;
            try
            {
                _suppressFolderTreeSelectionChange = true;

                var tvi = FindTreeViewItem(FolderTree, selected);
                if (tvi != null)
                {
                    ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(FolderTree, true);
                    tvi.IsSelected = false;
                    cleared = true;
                }
            }
            finally
            {
                if (!cleared)
                    ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(FolderTree, false);

                _suppressFolderTreeSelectionChange = false;
            }
        }
    }

    private void FolderTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsFolderMultiSelectMode)
            return;

        var src = e.OriginalSource as DependencyObject;

        // Don't block scrollbars.
        if (FindVisualParent<ScrollBar>(src) != null)
            return;

        // Allow checkbox clicks (they toggle selection via FolderCheckBox_PreviewMouseDown).
        if (FindVisualParent<CheckBox>(src) != null)
            return;

        // Allow expander clicks (expand/collapse).
        var tb = FindVisualParent<ToggleButton>(src);
        if (tb != null && string.Equals(tb.Name, "Expander", StringComparison.Ordinal))
            return;

        // Click on any item header should NOT select it in multi-select mode.
        if (FindVisualParent<TreeViewItem>(src) != null)
        {
            e.Handled = true;
            FolderTree.Focus();
        }
    }

    private void FolderTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsFolderMultiSelectMode)
            return;

        var src = e.OriginalSource as DependencyObject;

        // Don't block scrollbars.
        if (FindVisualParent<ScrollBar>(src) != null)
            return;

        // In multi-select mode, right-click must not select items and should not open per-folder context menu.
        e.Handled = true;
        FolderTree.Focus();
    }

    private void FolderTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsFolderMultiSelectMode)
            return;

        // Keep bulk delete via Delete key (it operates on checked folders in this mode).
        if (e.Key == Key.Delete)
            return;

        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                break;
        }
    }

    // -----------------------------
    // Drag & Drop: move selected entries to folder by dropping on the tree
    // -----------------------------

    private void FolderTree_PreviewDragOver(object sender, DragEventArgs e)
    {
        // In folder multi-select mode we intentionally disable drop to avoid mixing interaction modes.
        if (IsFolderMultiSelectMode)
        {
            ClearDragDropHover();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!TryGetDraggedEntryIds(e, out var ids) || ids.Length == 0)
        {
            ClearDragDropHover();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Disallow moving soft-deleted entries via drag&drop (restore requires master-password).
        if (AreAnyEntriesDeleted(ids))
        {
            ClearDragDropHover();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!TryGetFolderNodeFromDragEvent(e, out var node))
        {
            ClearDragDropHover();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Allow dropping entries onto the Trash node: this performs a soft-delete (same as Delete).
        if (node.Kind == FolderNodeKind.Trash)
        {
            SetDragDropHover(node);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (!IsValidDropTarget(node, out var targetFolderId))
        {
            ClearDragDropHover();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // No-op move: all dragged entries are already in that folder.
        if (AreAllEntriesAlreadyInFolder(ids, targetFolderId))
        {
            ClearDragDropHover();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        // Visual cue + auto-expand on hover.
        SetDragDropHover(node);

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }


private void FolderTree_PreviewDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (IsFolderMultiSelectMode)
            {
                e.Handled = true;
                return;
            }

            if (!TryGetDraggedEntryIds(e, out var ids) || ids.Length == 0)
            {
                e.Handled = true;
                return;
            }

            // Disallow moving soft-deleted entries via drag&drop (restore requires master-password).
            if (AreAnyEntriesDeleted(ids))
            {
                AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
                e.Handled = true;
                return;
            }

            if (!TryGetFolderNodeFromDragEvent(e, out var node))
            {
                e.Handled = true;
                return;
            }

            // Dropping entries onto the Trash node performs a soft-delete (same as Delete).
            if (node.Kind == FolderNodeKind.Trash)
            {
                SafeUi(nameof(FolderTree_PreviewDrop), () =>
                {
                    MoveEntriesToTrash(ids);
                });

                e.Handled = true;
                return;
            }

            if (!IsValidDropTarget(node, out var targetFolderId))
            {
                e.Handled = true;
                return;
            }

            SafeUi(nameof(FolderTree_PreviewDrop), () =>
            {
                MoveEntriesToFolder(ids, targetFolderId);
            });

            e.Handled = true;
        }
        finally
        {
            ClearDragDropHover();
        }
    }

private void FolderTree_PreviewDragLeave(object sender, DragEventArgs e)
{
    // DragLeave fires when moving between TreeViewItems as well.
    // Clear hover only when leaving the whole tree to avoid flicker.
    try
    {
        if (FolderTree != null)
        {
            var p = e.GetPosition(FolderTree);
            if (p.X >= 0 && p.Y >= 0 && p.X <= FolderTree.ActualWidth && p.Y <= FolderTree.ActualHeight)
            {
                e.Handled = true;
                return;
            }
        }
    }
    catch
    {
        // ignore
    }

    ClearDragDropHover();
    e.Handled = true;
}




    private static bool TryGetDraggedEntryIds(DragEventArgs e, out Guid[] ids)
    {
        ids = Array.Empty<Guid>();

        try
        {
            if (e.Data == null)
                return false;

            if (!e.Data.GetDataPresent(DragEntryIdsFormat))
                return false;

            if (e.Data.GetData(DragEntryIdsFormat) is Guid[] g)
            {
                ids = g;
                return true;
            }

            // Sometimes payload may come as object[]
            if (e.Data.GetData(DragEntryIdsFormat) is object[] arr)
            {
                ids = arr.OfType<Guid>().ToArray();
                return ids.Length > 0;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetFolderNodeFromDragEvent(DragEventArgs e, out FolderNode node)
    {
        node = null!;

        var src = e.OriginalSource as DependencyObject;
        if (src == null)
            return false;

        var tvi = FindVisualParent<TreeViewItem>(src);
        if (tvi?.DataContext is FolderNode fn)
        {
            node = fn;
            return true;
        }

        return false;
    }

    private static bool IsValidDropTarget(FolderNode node, out Guid? targetFolderId)
    {
        targetFolderId = null;

        if (node.Kind == FolderNodeKind.Folder)
        {
            targetFolderId = node.Id;
            return true;
        }

        if (node.Kind == FolderNodeKind.NoFolder)
        {
            targetFolderId = null;
            return true;
        }

        return false;
    }

    private bool AreAnyEntriesDeleted(System.Collections.Generic.IReadOnlyCollection<Guid> entryIds)
    {
        if (entryIds == null || entryIds.Count == 0)
            return false;

        var set = new System.Collections.Generic.HashSet<Guid>(entryIds);
        var entries = _vault.Entries ?? System.Array.Empty<VaultEntry>();

        foreach (var en in entries)
        {
            if (!set.Contains(en.Id))
                continue;

            if (en.IsDeleted)
                return true;
        }

        return false;
    }

    private bool AreAllEntriesAlreadyInFolder(Guid[] entryIds, Guid? targetFolderId)
    {
        var set = new System.Collections.Generic.HashSet<Guid>(entryIds);
        var entries = _vault.Entries ?? Array.Empty<VaultEntry>();

        bool any = false;
        foreach (var en in entries)
        {
            if (!set.Contains(en.Id))
                continue;
            any = true;
            if (en.FolderId != targetFolderId)
                return false;
        }

        return any;
    }

    private void MoveEntriesToFolder(Guid[] entryIds, Guid? targetFolderId)
    {
        if (entryIds == null || entryIds.Length == 0)
            return;

        if (AreAnyEntriesDeleted(entryIds))
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return;
        }

        var set = new System.Collections.Generic.HashSet<Guid>(entryIds);
        var entries = _vault.Entries ?? Array.Empty<VaultEntry>();

        bool anyChanged = false;
        var now = DateTime.UtcNow;
        foreach (var en in entries)
        {
            if (!set.Contains(en.Id))
                continue;

            if (en.FolderId == targetFolderId)
                continue;

            en.FolderId = targetFolderId;
            en.UpdatedUtc = now;
            anyChanged = true;
        }

        if (!anyChanged)
            return;

        _store.Save(_masterPassword, _vault);

        // Refresh the right pane list. Context is NOT changed by moving.
        RefreshGrid();
        UpdateEntryActionButtons();


        // Clear selection to avoid stale SelectedEntries state when items moved out of the current view.
        try
        {
            if (Grid != null)
                Grid.SelectedItems.Clear();
            SyncSelectedEntriesFromGrid();
        }
        catch
        {
            // ignore
        }
    }

    private void MoveEntriesToTrash(Guid[] entryIds)
    {
        if (entryIds == null || entryIds.Length == 0)
            return;

        // Deleted entries can only be restored with master-password (drag&drop must not bypass that).
        if (AreAnyEntriesDeleted(entryIds))
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["TrashOnlyRestore"]);
            return;
        }

        string confirmText = entryIds.Length == 1
            ? Loc.Instance["ConfirmDelete"]
            : string.Format(Loc.Instance["ConfirmDeleteMany"], entryIds.Length);

        if (AppMessageDialogWindow.ShowYesNo(this, Loc.Instance["AppTitle"], confirmText) != MessageBoxResult.Yes)
            return;
        var set = new System.Collections.Generic.HashSet<Guid>(entryIds);
        var list = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();

        bool changed = false;
        var now = DateTime.UtcNow;

        foreach (var it in list)
        {
            if (!set.Contains(it.Id))
                continue;

            if (it.IsDeleted)
                continue;

            it.IsDeleted = true;
            it.DeletedAtUtc = now;
            it.DeletedFromFolderId = it.FolderId;
            // Remove from normal folder context immediately.
            it.FolderId = null;
            it.UpdatedUtc = now;

            changed = true;
        }

        if (!changed)
            return;

        _vault.Entries = list.ToArray();

        _store.Save(_masterPassword, _vault);

        // Update special captions ("Favorites (N)", "Trash (N)") and active context title.
        RefreshActiveContextUi();

        RefreshGrid();

        ClearEntriesSelectionSafe();
    }



    private void UpdateFolderActionButtons()
    {
        // РЎРѕР·РґР°РЅРёРµ РїР°РїРѕРє СЂР°Р·СЂРµС€Р°РµРј:
        // - РІ РєРѕСЂРЅРµ ("РџР°РїРєРё" / FolderRoot) в†’ СЃРѕР·РґР°С‘Рј РїР°РїРєСѓ РІРµСЂС…РЅРµРіРѕ СѓСЂРѕРІРЅСЏ
        // - РІРЅСѓС‚СЂРё РѕР±С‹С‡РЅРѕР№ РїР°РїРєРё (Folder) в†’ СЃРѕР·РґР°С‘Рј РїРѕРґРїР°РїРєСѓ
        // Р—Р°РїСЂРµС‰Р°РµРј РґР»СЏ СЃРїРµС†РёР°Р»СЊРЅРѕРіРѕ СѓР·Р»Р° "Р‘РµР· РїР°РїРєРё" (NoFolder).
        bool canNew = _selectedFolderNode == null
                      || _selectedFolderNode.Kind == FolderNodeKind.Folder
                      || _selectedFolderNode.Kind == FolderNodeKind.FolderRoot;

        // Р”РѕРї. РїСЂР°РІРёР»Рѕ: РїСЂРё РЅР°РІРµРґРµРЅРёРё РЅР° "Р‘РµР· РїР°РїРєРё" РєРЅРѕРїРєСѓ СЃРѕР·РґР°РЅРёСЏ РїР°РїРєРё РІС‹РєР»СЋС‡Р°РµРј,
        // РґР°Р¶Рµ РµСЃР»Рё РІС‹Р±СЂР°РЅ РґСЂСѓРіРѕР№ СѓР·РµР» (С‡С‚РѕР±С‹ РЅРµ РІРІРѕРґРёС‚СЊ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ РІ Р·Р°Р±Р»СѓР¶РґРµРЅРёРµ).
        if (_isHoveringNoFolder)
            canNew = false;

        bool canRename = _selectedFolderNode != null &&
                         (_selectedFolderNode.Kind == FolderNodeKind.Folder || _selectedFolderNode.Kind == FolderNodeKind.NoFolder);

        // IMPORTANT UX:
        // - In multi-select mode, the toolbar Delete must apply ONLY to checked folders.
        //   Do not enable it for the currently selected folder (otherwise the button is enabled but does nothing).
        // - In normal mode, keep legacy behavior: delete the selected folder.
        bool hasChecked = CheckedFoldersCount > 0;
        bool canDelete = IsFolderMultiSelectMode
            ? hasChecked
            : (_selectedFolderNode?.Kind == FolderNodeKind.Folder || hasChecked);

        if (AddFolderBtn != null) AddFolderBtn.IsEnabled = canNew && !_foldersCollapsed;
        if (RenameFolderBtn != null) RenameFolderBtn.IsEnabled = canRename && !_foldersCollapsed;
        if (DeleteFolderBtn != null) DeleteFolderBtn.IsEnabled = canDelete && !_foldersCollapsed;


        (HotkeyAddFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }


    private void UpdateEntryActionButtons()
    {
        // "Add entry" is allowed only when active context is explicitly chosen (Folder / NoFolder).
        bool canAddEntry = CanCreateEntry;

        if (EntryAddBtn != null)
            EntryAddBtn.IsEnabled = canAddEntry;


        (HotkeyAddEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // -----------------------------
    // Hotkeys (I1.2)
    // -----------------------------

    private void FocusEntrySearchBox()
    {
        if (!IsUnlocked) return;
        try
        {
            SearchBox?.Focus();
            SearchBox?.SelectAll();
        }
        catch
        {
            // best-effort
        }
    }

    private void FocusFolderSearchBox()
    {
        if (!IsUnlocked) return;
        try
        {
            FolderSearchBox?.Focus();
            FolderSearchBox?.SelectAll();
        }
        catch
        {
            // best-effort
        }
    }

    private bool CanCreateFolderHotkey()
    {
        try
        {
            if (!IsUnlocked) return false;
            if (_foldersCollapsed) return false;

            bool canNew = _selectedFolderNode == null
                          || _selectedFolderNode.Kind == FolderNodeKind.Folder
                          || _selectedFolderNode.Kind == FolderNodeKind.FolderRoot;

            if (_isHoveringNoFolder)
                canNew = false;

            return canNew;
        }
        catch
        {
            return false;
        }
    }

    private System.Windows.Input.ICommand? ResolveMainWindowHotkeyCommand(string id)
    {
        return id switch
        {
            "main.search.focusEntries" => HotkeyFocusEntrySearchCommand,
            "main.search.focusFolders" => HotkeyFocusFolderSearchCommand,
            "main.entry.add" => HotkeyAddEntryCommand,
            "main.folder.add" => HotkeyAddFolderCommand,
            "main.lock.toggle" => HotkeyToggleLockCommand,
            "help.open" => OpenHelpCommand,
            _ => null
        };
    }


    private void FolderContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        // Р’ РєРѕРЅС‚РµРєСЃС‚РЅРѕРј РјРµРЅСЋ Р»РѕРіРёРєР° РѕРїРёСЂР°РµС‚СЃСЏ РЅР° С‚РµРєСѓС‰РёР№ РІС‹Р±СЂР°РЅРЅС‹Р№ СѓР·РµР».
        // Р Р°Р·СЂРµС€Р°РµРј СЃРѕР·РґР°РІР°С‚СЊ РїР°РїРєСѓ РІ РєРѕСЂРЅРµ (FolderRoot) Рё РІРЅСѓС‚СЂРё РѕР±С‹С‡РЅС‹С… РїР°РїРѕРє (Folder).
        // Р—Р°РїСЂРµС‰Р°РµРј РґР»СЏ "Р‘РµР· РїР°РїРєРё" (NoFolder).
        bool canNew = _selectedFolderNode == null
                      || _selectedFolderNode.Kind == FolderNodeKind.Folder
                      || _selectedFolderNode.Kind == FolderNodeKind.FolderRoot;

        bool canRename = _selectedFolderNode != null &&
                         (_selectedFolderNode.Kind == FolderNodeKind.Folder || _selectedFolderNode.Kind == FolderNodeKind.NoFolder);

        bool hasChecked = CheckedFoldersCount > 0;
        bool canDelete = IsFolderMultiSelectMode
            ? hasChecked
            : (_selectedFolderNode?.Kind == FolderNodeKind.Folder || hasChecked);

        if (FolderTree.ContextMenu?.Items.Count >= 3)
        {
            if (FolderTree.ContextMenu.Items[0] is MenuItem miNew) miNew.IsEnabled = canNew;
            if (FolderTree.ContextMenu.Items[1] is MenuItem miRen) miRen.IsEnabled = canRename;
            if (FolderTree.ContextMenu.Items[2] is MenuItem miDel) miDel.IsEnabled = canDelete;
        }
    }

    private void FolderContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        if (FolderTree != null)
            ExplorerSelectionBehavior.SetSuppressTreeActivateNextSelectionChange(FolderTree, false);

        NormalizeSelectedFolderNodeToSteadyState();
        SelectFolderNodeInTree(_selectedFolderNode);
    }

    private void FolderTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        // Requirement: when hovering the special node "Р‘РµР· РїР°РїРєРё", disable "create folder" button.
        var node = GetFolderNodeUnderMouse(e.OriginalSource as DependencyObject);
        bool hoveringNoFolder = node?.Kind == FolderNodeKind.NoFolder;
        bool needUpdateFolderActions = false;

        if (hoveringNoFolder != _isHoveringNoFolder)
        {
            _isHoveringNoFolder = hoveringNoFolder;
            needUpdateFolderActions = true;
        }

        if (needUpdateFolderActions)
            UpdateFolderActionButtons();
    }

    private void FolderTree_MouseLeave(object sender, MouseEventArgs e)
    {
        ClearDragDropHover();
        bool needUpdateFolderActions = false;

        if (_isHoveringNoFolder)
        {
            _isHoveringNoFolder = false;
            needUpdateFolderActions = true;
        }

        if (needUpdateFolderActions)
            UpdateFolderActionButtons();
    }

    private static FolderNode? GetFolderNodeUnderMouse(DependencyObject? original)
    {
        DependencyObject? dep = original;
        while (dep != null && dep is not TreeViewItem)
            dep = VisualTreeHelper.GetParent(dep);

        return dep is TreeViewItem tvi ? tvi.DataContext as FolderNode : null;
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        Guid? parentId = null;

        if (_selectedFolderNode?.Kind == FolderNodeKind.Folder)
            parentId = _selectedFolderNode.Id;

        var folderName = ShowHostedFolderDialog(Loc.Instance["FolderNew"], Loc.Instance["FolderNamePrompt"]);
        if (string.IsNullOrWhiteSpace(folderName)) return;

        var folder = new VaultFolder { Id = Guid.NewGuid(), Name = folderName, ParentId = parentId };

        var list = (_vault.Folders ?? Array.Empty<VaultFolder>()).ToList();
        list.Add(folder);
        _vault.Folders = list.ToArray();

        _store.Save(_masterPassword, _vault);

        // Keep selection on created folder
        _selectedFolderNode = new FolderNode(FolderNodeKind.Folder, folder.Name, folder.Id, folder.ParentId);

        BuildFolderTree();
    }

    private void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolderNode == null) return;

        // Allow renaming the special node "Р‘РµР· РїР°РїРєРё". This does NOT create a real folder
        // and does not move any entries вЂ” it only changes the display name, stored in settings.
        if (_selectedFolderNode.Kind == FolderNodeKind.NoFolder)
        {
            var currentName = GetNoFolderDisplayName();
            // NOTE: use a distinct variable name to avoid CS0136 (shadowing another 'dlg' declared later in the method).
            var newName = ShowHostedFolderDialog(Loc.Instance["FolderRename"], Loc.Instance["FolderNamePrompt"], currentName);
            if (string.IsNullOrWhiteSpace(newName)) return;
            // If user sets it to the localized default, clear override so it follows language changes.
            App.Settings.NoFolderDisplayName = string.Equals(newName, Loc.Instance["FolderNone"], StringComparison.Ordinal)
                ? null
                : newName;
            SettingsStore.Save(App.Settings);

            // Keep selection/context on "Р‘РµР· РїР°РїРєРё"
            _selectedFolderNode = new FolderNode(FolderNodeKind.NoFolder, GetNoFolderDisplayName());
            if (_activeFolderNode?.Kind == FolderNodeKind.NoFolder)
                _activeFolderNode = _selectedFolderNode;

            BuildFolderTree();
            return;
        }

        if (_selectedFolderNode.Kind != FolderNodeKind.Folder) return;

        var folders = (_vault.Folders ?? Array.Empty<VaultFolder>()).ToList();
        var f = folders.FirstOrDefault(x => x.Id == _selectedFolderNode.Id);
        if (f == null) return;

        var folderName = ShowHostedFolderDialog(Loc.Instance["FolderRename"], Loc.Instance["FolderNamePrompt"], f.Name);
        if (string.IsNullOrWhiteSpace(folderName)) return;

        f.Name = folderName;
        _vault.Folders = folders.ToArray();

        _store.Save(_masterPassword, _vault);

        // Keep selection
        _selectedFolderNode = new FolderNode(FolderNodeKind.Folder, f.Name, f.Id, f.ParentId);

        BuildFolderTree();
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedOrCheckedFolders();
    }

    private void FolderCheckBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // IMPORTANT UX:
        // - Clicking a checkbox must NOT change TreeView selection (and therefore must NOT change the active context).
        // - We toggle the bound IsChecked manually and mark the event as handled so the click doesn't bubble to TreeViewItem.
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (sender is CheckBox cb && cb.DataContext is FolderNode node && node.Kind == FolderNodeKind.Folder)
        {
            node.IsChecked = !node.IsChecked;
            e.Handled = true;
            UpdateCheckedFoldersState();
        }
    }

    private void UpdateCheckedFoldersState()
    {
        _checkedFoldersCount = GetCheckedFolderIds().Count;

        OnPropertyChanged(nameof(CheckedFoldersCount));
        OnPropertyChanged(nameof(CheckedFoldersInfo));

        (DeleteCheckedFoldersCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearCheckedFoldersCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Also affects the single delete button in the folders toolbar.
        UpdateFolderActionButtons();
    }

    private void ClearCheckedFolders()
    {
        void Walk(FolderNode node)
        {
            if (node.Kind == FolderNodeKind.Folder && node.IsChecked)
                node.IsChecked = false;
            foreach (var c in node.Children)
                Walk(c);
        }

        foreach (var r in _folderTreeRoots)
            Walk(r);

        UpdateCheckedFoldersState();
    }

    private void DeleteCheckedFolders()
    {
        var checkedIds = GetCheckedFolderIds();
        if (checkedIds.Count == 0)
            return;

        DeleteFoldersInternal(checkedIds);
    }

    private System.Collections.Generic.List<Guid> GetCheckedFolderIds()
    {
        var result = new System.Collections.Generic.List<Guid>();
        void Walk(FolderNode node)
        {
            if (node.Kind == FolderNodeKind.Folder && node.IsChecked)
                result.Add(node.Id);
            foreach (var c in node.Children)
                Walk(c);
        }

        foreach (var r in _folderTreeRoots)
            Walk(r);

        return result;
    }

    private void DeleteFoldersInternal(System.Collections.Generic.List<Guid> rootFolderIds)
    {
        // Build union of folders + descendants.
        var deleteSet = new System.Collections.Generic.HashSet<Guid>();
        foreach (var id in rootFolderIds)
            foreach (var x in GetFolderAndDescendants(id))
                deleteSet.Add(x);

        bool activeContextDeleted = _activeFolderNode?.Kind == FolderNodeKind.Folder && deleteSet.Contains(_activeFolderNode.Id);

        // Count how many entries will be moved to "No folder".
        int movedEntriesCount = 0;
        try
        {
            foreach (var en in _vault.Entries ?? Array.Empty<VaultEntry>())
            {
                if (en.FolderId != null && deleteSet.Contains(en.FolderId.Value))
                    movedEntriesCount++;
            }
        }
        catch { /* ignore */ }

        var noFolderName = GetNoFolderDisplayName();

        string confirmText = rootFolderIds.Count == 1
            ? string.Format(Loc.Instance["ConfirmDeleteFolder"], noFolderName)
            : string.Format(Loc.Instance["ConfirmDeleteFolderMany"], rootFolderIds.Count, noFolderName);

        if (movedEntriesCount > 0)
            confirmText += "\n" + string.Format(Loc.Instance["ConfirmDeleteFolderEntriesMoved"], movedEntriesCount, noFolderName);

        if (AppMessageDialogWindow.ShowYesNo(this, Loc.Instance["AppTitle"], confirmText) != MessageBoxResult.Yes)
            return;
        // Remove folders
        var folders = (_vault.Folders ?? Array.Empty<VaultFolder>()).Where(x => !deleteSet.Contains(x.Id)).ToArray();
        _vault.Folders = folders;

        // Move entries to "No folder"
        var entries = (_vault.Entries ?? Array.Empty<VaultEntry>()).ToList();
        foreach (var en in entries)
        {
            if (en.FolderId != null && deleteSet.Contains(en.FolderId.Value))
                en.FolderId = null;
        }
        _vault.Entries = entries.ToArray();

        _store.Save(_masterPassword, _vault);

        // After deletion keep no selection. IMPORTANT: do NOT fall back to "all entries".
        _selectedFolderNode = null;

        // If the active context folder was deleted, switch the context to "Р‘РµР· РїР°РїРєРё"
        // (entries are moved there by design).
        if (activeContextDeleted)
            _activeFolderNode = new FolderNode(FolderNodeKind.NoFolder, GetNoFolderDisplayName());

        BuildFolderTree();
        UpdateCheckedFoldersState();
    }

    private void DeleteSelectedOrCheckedFolders()
    {
        // TreeView has no true multi-select; for multi-delete we use checkboxes (Variant A).
        var checkedIds = GetCheckedFolderIds();

        if (IsFolderMultiSelectMode)
        {
            // In multi-select mode: delete ONLY checked folders.
            if (checkedIds.Count == 0)
                return;

            DeleteFoldersInternal(checkedIds);
            return;
        }

        // Normal mode: if nothing is checked, delete the currently selected folder (old behavior).
        if (checkedIds.Count == 0)
        {
            if (_selectedFolderNode?.Kind != FolderNodeKind.Folder)
                return;

            checkedIds.Add(_selectedFolderNode.Id);
        }

        DeleteFoldersInternal(checkedIds);
    }

    private System.Collections.Generic.HashSet<Guid> GetFolderAndDescendants(Guid folderId)
    {
        var result = new System.Collections.Generic.HashSet<Guid>();
        var folders = _vault.Folders ?? Array.Empty<VaultFolder>();

        var children = folders
            .Where(f => f.ParentId.HasValue)
            .GroupBy(f => f.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var stack = new System.Collections.Generic.Stack<Guid>();
        stack.Push(folderId);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!result.Add(cur)) continue;

            if (children.TryGetValue(cur, out var kids))
                foreach (var k in kids) stack.Push(k);
        }

        return result;
    }

    // --------------------
    // Attachments
    // --------------------

    private readonly System.Collections.Generic.List<System.Windows.Threading.DispatcherTimer> _tempAttachmentDeleteTimers = new();

    private static string SanitizeFileNameForTemp(string? fileName)
    {
        var name = (fileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return "file.bin";

        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');

        // Extra safety: avoid any separators if a strange name slips through.
        name = name.Replace(System.IO.Path.DirectorySeparatorChar, '_')
                   .Replace(System.IO.Path.AltDirectorySeparatorChar, '_');

        return name;
    }

    private static void CleanupOldTempAttachmentsBestEffort(string tempDir, TimeSpan maxAge)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tempDir) || !Directory.Exists(tempDir))
                return;

            var cutoffUtc = DateTime.UtcNow - maxAge;
            foreach (var fp in Directory.GetFiles(tempDir, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var fi = new FileInfo(fp);
                    if (fi.LastWriteTimeUtc < cutoffUtc)
                        fi.Delete();
                }
                catch { }
            }
        }
        catch { }
    }

    private void ScheduleTempFileDeleteBestEffort(string filePath, TimeSpan delay)
        => ScheduleTempFileDeleteBestEffort(filePath, delay, attempt: 0);

    /// <summary>
    /// Best-effort temp file deletion with retries.
    /// We create decrypted temp files to open attachments via ShellExecute; those files may remain locked
    /// by external processes at the moment of deletion. Retrying ensures we don't keep plaintext longer
    /// than needed.
    /// </summary>
    private void ScheduleTempFileDeleteBestEffort(string filePath, TimeSpan delay, int attempt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            // Hard cap on retries to avoid endless timers.
            if (attempt > 8)
                return;

            var t = new System.Windows.Threading.DispatcherTimer
            {
                Interval = delay
            };

            t.Tick += (_, __) =>
            {
                try { t.Stop(); } catch { }

                var deleted = false;
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        deleted = true;
                    }
                    else
                    {
                        deleted = true;
                    }
                }
                catch
                {
                    // File might still be locked by an external viewer.
                    deleted = false;
                }

                try { _tempAttachmentDeleteTimers.Remove(t); } catch { }

                if (!deleted)
                {
                    // Retry later with capped exponential backoff.
                    var nextMinutes = Math.Min(Math.Max(5, delay.TotalMinutes * 2), 120);
                    ScheduleTempFileDeleteBestEffort(filePath, TimeSpan.FromMinutes(nextMinutes), attempt + 1);
                }
            };

            _tempAttachmentDeleteTimers.Add(t);
            t.Start();
        }
        catch { }
    }

    private static void MarkTempDecryptedFileBestEffort(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            var attrs = File.GetAttributes(filePath);
            attrs |= FileAttributes.Temporary;
            attrs |= FileAttributes.NotContentIndexed;
            File.SetAttributes(filePath, attrs);
        }
        catch
        {
            // best-effort
        }
    }


    public sealed class AttachmentInfo
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public System.Collections.Generic.List<AttachmentInfo> GetAttachmentsForEntry(Guid entryId)
    {
        try
        {
            return (_vault.Attachments ?? Array.Empty<VaultAttachment>())
                .Where(a => a.EntryId == entryId)
                .Select(a => new AttachmentInfo
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    SizeBytes = a.Size,
                    CreatedUtc = a.CreatedUtc
                })
                .ToList();
        }
        catch
        {
            return new System.Collections.Generic.List<AttachmentInfo>();
        }
    }

    public void AddAttachmentsForEntry(Guid entryId, string[] filePaths)
    {
        if (!IsSessionUnlocked)
            return;

        if (filePaths == null || filePaths.Length == 0)
            return;

        var newMetas = new System.Collections.Generic.List<VaultAttachment>();
        var createdBlobPaths = new System.Collections.Generic.List<string>();

        try
        {
            // Ensure directory exists.
            AttachmentsStore.EnsureAttachmentsDir(_store.Path);

            foreach (var fp in filePaths)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(fp) || !File.Exists(fp))
                        continue;

                    var fi = new FileInfo(fp);
                    var meta = new VaultAttachment
                    {
                        Id = Guid.NewGuid(),
                        EntryId = entryId,
                        FileName = Path.GetFileName(fp) ?? "",
                        Size = fi.Exists ? fi.Length : 0,
                        CreatedUtc = DateTime.UtcNow
                    };

                    var plain = File.ReadAllBytes(fp);
                    byte[]? enc = null;
                    try
                    {
                        enc = VaultCrypto.Encrypt(_masterPassword, plain);
                    }
                    finally
                    {
                        try { if (plain.Length > 0) Array.Clear(plain, 0, plain.Length); } catch { }
                    }

                    if (enc == null || enc.Length == 0)
                        continue;

                    var blobPath = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
                    WriteBytesSafely(enc, blobPath);
                    createdBlobPaths.Add(blobPath);

                    try { Array.Clear(enc, 0, enc.Length); } catch { }

                    newMetas.Add(meta);
                }
                catch
                {
                    // Best-effort: skip problematic file.
                }
            }

            if (newMetas.Count == 0)
                return;

            var list = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).ToList();
            list.AddRange(newMetas);
            _vault.Attachments = list.ToArray();

            // Persist metadata in the vault.
            _store.Save(_masterPassword, _vault);
        }
        catch (Exception ex)
        {
            // Roll back best-effort.
            try
            {
                if (newMetas.Count > 0)
                {
                    var list = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).ToList();
                    var ids = newMetas.Select(x => x.Id).ToHashSet();
                    list.RemoveAll(x => ids.Contains(x.Id));
                    _vault.Attachments = list.ToArray();
                }
            }
            catch { }

            foreach (var p in createdBlobPaths)
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { }
            }

            try
            {
                var owner = GetDialogOwnerWindow();
                AppMessageDialogWindow.ShowOk(owner, Loc.Instance["Error"], ex.Message);
            }
            catch { }

            return;
        }
    }

    public void RemoveAttachment(Guid attachmentId)
    {
        if (!IsSessionUnlocked)
            return;

        var list = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).ToList();
        var idx = list.FindIndex(x => x.Id == attachmentId);
        if (idx < 0)
            return;

        var meta = list[idx];
        list.RemoveAt(idx);
        _vault.Attachments = list.ToArray();

        // Delete blob best-effort.
        try
        {
            var path = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }

        try
        {
            _store.Save(_masterPassword, _vault);
        }
        catch (Exception ex)
        {
            try
            {
                var owner = GetDialogOwnerWindow();
                AppMessageDialogWindow.ShowOk(owner, Loc.Instance["Error"], ex.Message);
            }
            catch { }
        }
    }

    public void RemoveAttachments(System.Collections.Generic.IEnumerable<Guid> attachmentIds)
    {
        if (!IsSessionUnlocked)
            return;

        try
        {
            if (attachmentIds == null)
                return;

            var ids = new System.Collections.Generic.HashSet<Guid>(attachmentIds.Where(x => x != Guid.Empty));
            if (ids.Count == 0)
                return;

            var list = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).ToList();
            if (list.Count == 0)
                return;

            var toRemove = list.Where(a => ids.Contains(a.Id)).ToList();
            if (toRemove.Count == 0)
                return;

            list.RemoveAll(a => ids.Contains(a.Id));
            _vault.Attachments = list.ToArray();

            // Delete blobs best-effort.
            foreach (var meta in toRemove)
            {
                try
                {
                    var path = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch { }
            }

            _store.Save(_masterPassword, _vault);
        }
        catch (Exception ex)
        {
            try
            {
                var owner = GetDialogOwnerWindow();
                AppMessageDialogWindow.ShowOk(owner, Loc.Instance["Error"], ex.Message);
            }
            catch { }
        }
    }

    /// <summary>
    /// Applies a draft set of attachment changes in one operation: remove selected existing attachments
    /// and add new ones from file paths. Used to keep attachment changes "atomic" with entry save.
    /// Returns false if the vault couldn't be saved.
    /// </summary>
    private static byte[] ReadAllBytesShared(string filePath)
    {
        // Some applications (e.g., Office) can keep the file open.
        // Try to read with permissive sharing.
        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Applies a draft set of attachment changes in one operation: remove selected existing attachments
    /// and add new ones from file paths. Used to keep attachment changes "atomic" with entry save.
    /// Returns false if the operation can't be completed (I/O failure) or the vault couldn't be saved.
    /// On failure, no changes are applied (atomic) and <paramref name="error"/> is set.
    /// </summary>
    public bool TryApplyAttachmentDraft(Guid entryId,
        System.Collections.Generic.IEnumerable<Guid>? attachmentIdsToRemove,
        System.Collections.Generic.IEnumerable<string>? filePathsToAdd,
        out int added,
        out int removed,
        out Exception? error)
    {
        added = 0;
        removed = 0;
        error = null;

        if (!IsSessionUnlocked)
            return false;

        var removeIds = new System.Collections.Generic.HashSet<Guid>();
        try
        {
            if (attachmentIdsToRemove != null)
                removeIds = new System.Collections.Generic.HashSet<Guid>(attachmentIdsToRemove.Where(x => x != Guid.Empty));
        }
        catch { }

        var addPaths = new System.Collections.Generic.List<string>();
        try
        {
            if (filePathsToAdd != null)
            {
                foreach (var p in filePathsToAdd)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(p))
                            continue;

                        // Keep the path even if the file disappears later.
                        // Atomic apply: if any draft path is missing/unreadable at save time, fail the whole operation.
                        string full;
                        try { full = Path.GetFullPath(p); }
                        catch { full = p; }

                        if (string.IsNullOrWhiteSpace(full))
                            continue;

                        if (addPaths.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        addPaths.Add(full);
                    }
                    catch { }
                }
            }
        }
        catch { }

        if (removeIds.Count == 0 && addPaths.Count == 0)
            return true;

        var originalList = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).ToList();

        var toRemove = removeIds.Count == 0
            ? new System.Collections.Generic.List<VaultAttachment>()
            : originalList.Where(a => removeIds.Contains(a.Id)).ToList();
        removed = toRemove.Count;

        var newMetas = new System.Collections.Generic.List<VaultAttachment>();
        var createdBlobPaths = new System.Collections.Generic.List<string>();

        try
        {
            if (addPaths.Count > 0)
            {
                AttachmentsStore.EnsureAttachmentsDir(_store.Path);

                foreach (var fp in addPaths)
                {
                    if (string.IsNullOrWhiteSpace(fp) || !File.Exists(fp))
                        throw new FileNotFoundException("Attachment source file not found.", fp);

                    var fi = new FileInfo(fp);
                    var meta = new VaultAttachment
                    {
                        Id = Guid.NewGuid(),
                        EntryId = entryId,
                        FileName = Path.GetFileName(fp) ?? "",
                        Size = fi.Exists ? fi.Length : 0,
                        CreatedUtc = DateTime.UtcNow
                    };

                    // Read with permissive sharing to support files opened by other apps.
                    var plain = ReadAllBytesShared(fp);
                    byte[]? enc = null;
                    try
                    {
                        enc = VaultCrypto.Encrypt(_masterPassword, plain);
                    }
                    finally
                    {
                        try { if (plain.Length > 0) Array.Clear(plain, 0, plain.Length); } catch { }
                    }

                    if (enc == null || enc.Length == 0)
                        throw new IOException("Failed to encrypt attachment.");

                    var blobPath = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
                    WriteBytesSafely(enc, blobPath);
                    createdBlobPaths.Add(blobPath);

                    try { Array.Clear(enc, 0, enc.Length); } catch { }

                    newMetas.Add(meta);
                    added++;
                }
            }

            // Apply changes to metadata.
            var updated = originalList;
            if (removeIds.Count > 0)
                updated = updated.Where(a => !removeIds.Contains(a.Id)).ToList();

            if (newMetas.Count > 0)
                updated.AddRange(newMetas);

            _vault.Attachments = updated.ToArray();

            // Persist metadata in the vault.
            _store.Save(_masterPassword, _vault);
        }
        catch (Exception ex)
        {
            // Roll back metadata changes best-effort.
            try { _vault.Attachments = originalList.ToArray(); } catch { }

            // Delete newly created blobs best-effort.
            foreach (var p in createdBlobPaths)
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { }
            }

            // Caller decides how to report (toast). Do not block UI with MessageBox here.
            error = ex;
            added = 0;
            removed = 0;
            return false;
        }

        // After successful save: delete removed blobs best-effort.
        foreach (var meta in toRemove)
        {
            try
            {
                var path = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        return true;
    }

    // --------------------
        // Pending encrypted attachments (entry draft)
    // --------------------

    internal bool TryCreatePendingEncryptedAttachment(string sourceFilePath, string destinationEncryptedPath,
        out long sizeBytes,
        out Exception? error)
    {
        sizeBytes = 0;
        error = null;

        try
        {
            if (!IsSessionUnlocked)
                return false;

            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                error = new FileNotFoundException("Attachment source file not found.", sourceFilePath);
                return false;
            }

            var fi = new FileInfo(sourceFilePath);
            sizeBytes = fi.Exists ? fi.Length : 0;

            var plain = IoUtils.ReadAllBytesShared(sourceFilePath);
            byte[]? enc = null;
            try
            {
                enc = VaultCrypto.Encrypt(_masterPassword, plain);
            }
            finally
            {
                try { if (plain.Length > 0) Array.Clear(plain, 0, plain.Length); } catch { }
            }

            if (enc == null || enc.Length == 0)
                throw new IOException("Failed to encrypt attachment.");

            IoUtils.WriteBytesSafely(enc, destinationEncryptedPath);
            try { Array.Clear(enc, 0, enc.Length); } catch { }

            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    internal bool TryApplyEncryptedAttachmentDraft(Guid entryId,
        System.Collections.Generic.IEnumerable<Guid>? attachmentIdsToRemove,
        System.Collections.Generic.IEnumerable<AttachmentDraftAddEncrypted>? encryptedAdds,
        out int added,
        out int removed,
        out Exception? error)
    {
        added = 0;
        removed = 0;
        error = null;

        if (!IsSessionUnlocked)
            return false;

        var removeIds = new System.Collections.Generic.HashSet<Guid>();
        try
        {
            if (attachmentIdsToRemove != null)
                removeIds = new System.Collections.Generic.HashSet<Guid>(attachmentIdsToRemove.Where(x => x != Guid.Empty));
        }
        catch { }

        var addList = new System.Collections.Generic.List<AttachmentDraftAddEncrypted>();
        try
        {
            if (encryptedAdds != null)
            {
                foreach (var a in encryptedAdds)
                {
                    if (a == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(a.EncryptedPath))
                        continue;

        // Do not dedupe by file name here; the entry editor already ensures name uniqueness.
                    addList.Add(a);
                }
            }
        }
        catch { }

        if (removeIds.Count == 0 && addList.Count == 0)
            return true;

        var originalList = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).ToList();

        var toRemove = removeIds.Count == 0
            ? new System.Collections.Generic.List<VaultAttachment>()
            : originalList.Where(a => removeIds.Contains(a.Id)).ToList();
        removed = toRemove.Count;

        var newMetas = new System.Collections.Generic.List<VaultAttachment>();
        var createdBlobPaths = new System.Collections.Generic.List<string>();

        try
        {
            if (addList.Count > 0)
            {
                AttachmentsStore.EnsureAttachmentsDir(_store.Path);

                foreach (var add in addList)
                {
                    if (!File.Exists(add.EncryptedPath))
                        throw new FileNotFoundException("Pending attachment blob not found.", add.EncryptedPath);

                    var meta = new VaultAttachment
                    {
                        Id = Guid.NewGuid(),
                        EntryId = entryId,
                        FileName = add.FileName ?? "",
                        Size = add.SizeBytes,
                        CreatedUtc = DateTime.UtcNow
                    };

                    // The pending blob is already encrypted with the master password.
                    // Copy its bytes into the vault attachments store.
                    var enc = File.ReadAllBytes(add.EncryptedPath);
                    if (enc.Length == 0)
                        throw new IOException("Pending attachment blob is empty.");

                    var blobPath = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
                    IoUtils.WriteBytesSafely(enc, blobPath);
                    createdBlobPaths.Add(blobPath);

                    try { Array.Clear(enc, 0, enc.Length); } catch { }

                    newMetas.Add(meta);
                    added++;
                }
            }

            // Apply changes to metadata.
            var updated = originalList;
            if (removeIds.Count > 0)
                updated = updated.Where(a => !removeIds.Contains(a.Id)).ToList();

            if (newMetas.Count > 0)
                updated.AddRange(newMetas);

            _vault.Attachments = updated.ToArray();

            // Persist metadata in the vault.
            _store.Save(_masterPassword, _vault);
        }
        catch (Exception ex)
        {
            // Roll back metadata changes best-effort.
            try { _vault.Attachments = originalList.ToArray(); } catch { }

            // Delete newly created blobs best-effort.
            foreach (var p in createdBlobPaths)
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { }
            }

            error = ex;
            added = 0;
            removed = 0;
            return false;
        }

        // After successful save: delete removed blobs best-effort.
        foreach (var meta in toRemove)
        {
            try
            {
                var path = AttachmentsStore.GetAttachmentBlobPath(_store.Path, meta.Id);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        return true;
    }

    internal bool TryOpenPendingEncryptedAttachment(string pendingEncryptedPath, string fileName, out Exception? error)
    {
        error = null;
        if (!IsSessionUnlocked)
            return false;

        try
        {
            if (string.IsNullOrWhiteSpace(pendingEncryptedPath) || !File.Exists(pendingEncryptedPath))
            {
                error = new FileNotFoundException("Pending attachment blob not found.", pendingEncryptedPath);
                return false;
            }

            var enc = File.ReadAllBytes(pendingEncryptedPath);
            if (enc.Length == 0)
            {
                error = new IOException("Pending attachment blob is empty.");
                return false;
            }

            var plain = VaultCrypto.Decrypt(_masterPassword, enc);
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "PassNotes", "AttachmentsOpen");
                Directory.CreateDirectory(tempDir);
                CleanupOldTempAttachmentsBestEffort(tempDir, TimeSpan.FromHours(12));

                var safeName = SanitizeFileNameForTemp(fileName);
                var outPath = Path.Combine(tempDir, $"draft_{Guid.NewGuid():N}_{safeName}");
                File.WriteAllBytes(outPath, plain);
                MarkTempDecryptedFileBestEffort(outPath);

                try
                {
                    Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    error = ex;
                    try { ScheduleTempFileDeleteBestEffort(outPath, TimeSpan.FromMinutes(1)); } catch { }
                    return false;
                }

                ScheduleTempFileDeleteBestEffort(outPath, TimeSpan.FromMinutes(10));
            }
            finally
            {
                try { Array.Clear(plain, 0, plain.Length); } catch { }
                try { Array.Clear(enc, 0, enc.Length); } catch { }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    internal bool TrySavePendingEncryptedAttachmentAs(string pendingEncryptedPath, string destinationFilePath, out Exception? error)
    {
        error = null;
        if (!IsSessionUnlocked)
            return false;

        try
        {
            if (string.IsNullOrWhiteSpace(pendingEncryptedPath) || !File.Exists(pendingEncryptedPath))
            {
                error = new FileNotFoundException("Pending attachment blob not found.", pendingEncryptedPath);
                return false;
            }

            var enc = File.ReadAllBytes(pendingEncryptedPath);
            if (enc.Length == 0)
            {
                error = new IOException("Pending attachment blob is empty.");
                return false;
            }

            var plain = VaultCrypto.Decrypt(_masterPassword, enc);
            try
            {
                IoUtils.WriteBytesSafely(plain, destinationFilePath);
            }
            finally
            {
                try { Array.Clear(plain, 0, plain.Length); } catch { }
                try { Array.Clear(enc, 0, enc.Length); } catch { }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    // Backward-compatible wrapper.
    public bool TryApplyAttachmentDraft(Guid entryId,
        System.Collections.Generic.IEnumerable<Guid>? attachmentIdsToRemove,
        System.Collections.Generic.IEnumerable<string>? filePathsToAdd,
        out int added,
        out int removed)
    {
        return TryApplyAttachmentDraft(entryId, attachmentIdsToRemove, filePathsToAdd, out added, out removed, out _);
    }

    public (int Saved, int Failed, Exception? Error) SaveAttachmentsToFolder(System.Collections.Generic.IEnumerable<Guid> attachmentIds, string folderPath)
    {
        if (!IsSessionUnlocked)
            return (0, 0, null);

        try
        {
            if (attachmentIds == null)
                return (0, 0, null);

            if (string.IsNullOrWhiteSpace(folderPath))
                return (0, 0, null);

            Directory.CreateDirectory(folderPath);

            var ids = attachmentIds.Where(x => x != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0)
                return (0, 0, null);

            var usedNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string MakeUniqueFilePath(string fileName)
            {
                var safe = SanitizeFileNameForTemp(fileName);
                var nameOnly = Path.GetFileNameWithoutExtension(safe);
                var ext = Path.GetExtension(safe);

                var candidate = safe;
                var n = 1;
                while (usedNames.Contains(candidate) || File.Exists(Path.Combine(folderPath, candidate)))
                {
                    n++;
                    candidate = $"{nameOnly} ({n}){ext}";
                }

                usedNames.Add(candidate);
                return Path.Combine(folderPath, candidate);
            }

            int saved = 0;
            int failed = 0;

            foreach (var id in ids)
            {
                var meta = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).FirstOrDefault(x => x.Id == id);
                if (meta == null)
                {
                    failed++;
                    continue;
                }

                var decrypted = ReadAndDecryptAttachment(meta.Id);
                if (decrypted == null)
                {
                    failed++;
                    continue;
                }

                try
                {
                    var fileName = string.IsNullOrWhiteSpace(meta.FileName) ? (meta.Id.ToString("N") + ".bin") : meta.FileName;
                    var outPath = MakeUniqueFilePath(fileName);
                    WriteBytesSafely(decrypted, outPath);
                    saved++;
                }
                catch
                {
                    failed++;
                }
                finally
                {
                    try { Array.Clear(decrypted, 0, decrypted.Length); } catch { }
                }
            }

            return (saved, failed, null);
        }
        catch (Exception ex)
        {
            return (0, 0, ex);
        }
    }

    // Backward-compatible wrapper (older callers expect (Saved, Failed)).
    public (int Saved, int Failed) SaveAttachmentsToFolderLegacy(System.Collections.Generic.IEnumerable<Guid> attachmentIds, string folderPath)
    {
        var (s, f, _) = SaveAttachmentsToFolder(attachmentIds, folderPath);
        return (s, f);
    }
    public bool TryOpenAttachment(Guid attachmentId, out Exception? error)
    {
        error = null;
        if (!IsSessionUnlocked)
            return false;

        var meta = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).FirstOrDefault(x => x.Id == attachmentId);
        if (meta == null)
        {
            error = new FileNotFoundException();
            return false;
        }

        var decrypted = ReadAndDecryptAttachment(meta.Id);
        if (decrypted == null)
        {
            error = new IOException();
            return false;
        }

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PassNotes", "AttachmentsOpen");
            Directory.CreateDirectory(tempDir);

            // Best-effort cleanup of old decrypted files.
            CleanupOldTempAttachmentsBestEffort(tempDir, TimeSpan.FromHours(12));

            var safeName = SanitizeFileNameForTemp(meta.FileName);
            var outPath = Path.Combine(tempDir, $"{meta.Id:N}_{safeName}");

            File.WriteAllBytes(outPath, decrypted);
            MarkTempDecryptedFileBestEffort(outPath);

            try
            {
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                error = ex;
                // Best-effort cleanup of decrypted temp file even when opening fails.
                try { ScheduleTempFileDeleteBestEffort(outPath, TimeSpan.FromMinutes(1)); } catch { }
                return false;
            }

            // Best-effort: attempt to delete the decrypted temp file later (may fail if the file is still in use).
            ScheduleTempFileDeleteBestEffort(outPath, TimeSpan.FromMinutes(10));
        }
        finally
        {
            try { Array.Clear(decrypted, 0, decrypted.Length); } catch { }
        }

        return true;
    }

    // Backward-compatible wrapper.
    public void OpenAttachment(Guid attachmentId)
    {
        TryOpenAttachment(attachmentId, out _);
    }

    public (bool Ok, bool Canceled, Exception? Error) TrySaveAttachmentAs(Guid attachmentId)
    {
        if (!IsSessionUnlocked)
            return (false, false, null);

        var meta = (_vault.Attachments ?? Array.Empty<VaultAttachment>()).FirstOrDefault(x => x.Id == attachmentId);
        if (meta == null)
            return (false, false, new FileNotFoundException());

        var owner = GetDialogOwnerWindow();
        var dlg = new SaveFileDialog
        {
            Title = Loc.Instance["AttachmentsSaveAs"],
            FileName = string.IsNullOrWhiteSpace(meta.FileName) ? (meta.Id.ToString("N") + ".bin") : meta.FileName,
            Filter = Loc.Instance["AllFilesFilter"],
            AddExtension = false,
            OverwritePrompt = true
        };

        if (dlg.ShowDialog(owner) != true)
            return (false, true, null);

        var decrypted = ReadAndDecryptAttachment(meta.Id);
        if (decrypted == null)
            return (false, false, new IOException());

        try
        {
            WriteBytesSafely(decrypted, dlg.FileName);
        }
        finally
        {
            try { Array.Clear(decrypted, 0, decrypted.Length); } catch { }
        }

        return (true, false, null);
    }

    // Backward-compatible wrapper.
    public void SaveAttachmentAs(Guid attachmentId)
    {
        TrySaveAttachmentAs(attachmentId);
    }

    private Window GetDialogOwnerWindow()
        => this;

    private byte[]? ReadAndDecryptAttachment(Guid attachmentId)
    {
        try
        {
            var path = AttachmentsStore.GetAttachmentBlobPath(_store.Path, attachmentId);
            if (!File.Exists(path))
                return null;

            var enc = File.ReadAllBytes(path);
            try
            {
                return VaultCrypto.Decrypt(_masterPassword, enc);
            }
            finally
            {
                try { if (enc.Length > 0) Array.Clear(enc, 0, enc.Length); } catch { }
            }
        }
        catch
        {
            return null;
        }
    }

    private static void WriteBytesSafely(byte[] bytes, string destinationFilePath)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("Destination file path is empty", nameof(destinationFilePath));

        var dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var tempPath = destinationFilePath + ".tmp_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (File.Exists(destinationFilePath))
            {
                try
                {
                    File.Replace(tempPath, destinationFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    tempPath = "";
                }
                catch
                {
                    File.Copy(tempPath, destinationFilePath, overwrite: true);
                    try { File.Delete(tempPath); } catch { }
                    tempPath = "";
                }
            }
            else
            {
                File.Move(tempPath, destinationFilePath);
                tempPath = "";
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }



    private static void ReplaceFileSafely(string sourceFilePath, string destinationFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path is empty", nameof(sourceFilePath));

        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("Destination file path is empty", nameof(destinationFilePath));

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found", sourceFilePath);

        var vaultDir = Path.GetDirectoryName(destinationFilePath) ?? SettingsStore.GetAppDir();
        Directory.CreateDirectory(vaultDir);

        // Copy to a temp file in the same directory to make replacement safer.
        var tempPath = Path.Combine(vaultDir, $".import_{Guid.NewGuid():N}.tmp");
        File.Copy(sourceFilePath, tempPath, overwrite: true);

        try
        {
            if (File.Exists(destinationFilePath))
            {
                // Try atomic replace first.
                try
                {
                    File.Replace(tempPath, destinationFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    // File.Replace removes tempPath.
                    tempPath = "";
                }
                catch
                {
                    // Fallback to overwrite copy.
                    File.Copy(tempPath, destinationFilePath, overwrite: true);
                    try { File.Delete(tempPath); } catch { }
                    tempPath = "";
                }
            }
            else
            {
                File.Move(tempPath, destinationFilePath);
                tempPath = "";
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

}
