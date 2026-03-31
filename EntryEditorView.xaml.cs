using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Input;

namespace PassNotes;

public partial class EntryEditorView : UserControl
{
    private readonly PopupToastController _copyToast = new(900);
    private readonly PopupToastController _infoToast = new(2600);
    private Popup? _infoToastPopup;
    private TextBlock? _infoToastText;

    public VaultEntry Result { get; private set; }

    public string DialogTitle { get; private set; } = string.Empty;

    public string Title { get => DialogTitle; private set => DialogTitle = value; }
    public Window? Owner => _hostOwner;
    public bool? DialogResult { get; private set; }
    public WindowState WindowState { get; private set; } = WindowState.Normal;
    public bool IsActive => (Window.GetWindow(this) ?? _hostOwner)?.IsActive ?? true;

    // Exposed for MainWindow (lock/auto-lock handling)
    public bool IsDirty => _dirtyTrackingReady && _isDirty;
    // Unsaved changes (dirty) tracking
    private bool _dirtyTrackingReady;
    private bool _isDirty;
    private bool _isSaving;
    private bool _suppressUnsavedPrompt;
    private bool _unsavedPromptInProgress;

    private string _origTitle = "";
    private string _origUsername = "";
    private string _origPassword = "";
    private string _origUrl = "";
    private string _origComment = "";

    private readonly bool _isNewEntry;

    private Guid? _locationFolderId;
    private bool _locationIsMissing;

    private bool _isHostedMode;
    private bool _hostedClosed;
    private MainWindow? _hostOwner;

    internal event Action<VaultEntry>? HostedSaved;
    internal event Action? HostedCancelled;
    // Sync events for child windows.
    internal event EventHandler? CommentTextChangedForExternal;

    internal string CurrentCommentText
    {
        get => CommentBox?.Text ?? "";
        set
        {
            try { CommentBox.Text = value ?? ""; } catch { }
        }
    }

    internal void EnableHostedMode(MainWindow? hostOwner)
    {
        _isHostedMode = true;
        _hostOwner = hostOwner;

        try
        {
            EntryActionsPanel.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    internal void RequestHostedPrimaryAction()
        => _ = TrySaveAndClose();

    internal void RequestHostedSecondaryAction()
        => TryHandleHostedCloseRequest();

    internal UIElement ExtractHostedContent()
    {
        if (Content is not UIElement root)
            throw new InvalidOperationException("Entry hosted content is not ready.");

        Content = null;
        return root;
    }

    internal void NotifyHostedLoaded()
    {
        try { RefreshAttachments(); } catch { }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    TitleBox.Focus();
                    Keyboard.Focus(TitleBox);
                    TitleBox.SelectAll();
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        catch { }
    }

    internal void NotifyHostedDialogClosed()
    {
        if (_hostedClosed)
            return;

        _hostedClosed = true;
        CleanupAfterClose();
    }

    internal void PrepareHostedCloseForLock()
    {
        try { _suppressUnsavedPrompt = true; } catch { }
        try { _unsavedPromptInProgress = false; } catch { }
        try { _keepPendingAttachmentsOnClose = true; } catch { }


    }

    public EntryEditorView(MainWindow hostOwner, VaultEntry? existing = null)
    {
        InitializeComponent();

        _hostOwner = hostOwner ?? throw new ArgumentNullException(nameof(hostOwner));
        _isHostedMode = true;
        try { EntryActionsPanel.Visibility = Visibility.Collapsed; } catch { }

        // Best-effort cleanup of stale pending attachment sessions (from previous crashes).
        // Runs once per app process and must never throw.
        TryCleanupStalePendingAttachmentSessionsOnce();

                
        // Attachments list (only for existing entries; new entries must be saved first).
        try
        {
            AttachmentsList.ItemsSource = _attachments;
            AttachmentsList.DisplayMemberPath = "Display";
            AttachmentsList.SelectionChanged += (_, _) => UpdateAttachmentsButtons();
            Loaded += EntryEditorView_Loaded;
            Unloaded += EntryEditorView_Unloaded;
            Loaded += (_, _) => RefreshAttachments();
        }
        catch
        {
            // best-effort
        }

        // Keep PasswordTextBox in sync when user types while visible
        PasswordTextBox.TextChanged += (_, _) =>
        {
            if (PasswordTextBox.Visibility == Visibility.Visible)
                PasswordBox.Password = PasswordTextBox.Text ?? "";

            // Re-evaluate dirty state after any password text changes.
            if (_dirtyTrackingReady)
                UpdateDirtyState();
        };

        if (existing is null)
        {
            DialogTitle = Loc.Instance["EntryAddTitle"];
            Result = new VaultEntry();
            _isNewEntry = true;
        }
        else
        {
            _isNewEntry = false;
            DialogTitle = Loc.Instance["EntryEditTitle"];
            Result = new VaultEntry
            {
                Id = existing.Id,
                Title = existing.Title,
                Username = existing.Username,
                Password = existing.Password,
                Url = existing.Url,
                Comment = existing.Comment,
                IsFavorite = existing.IsFavorite,
                FolderId = existing.FolderId,
                UpdatedUtc = existing.UpdatedUtc
            };

            TitleBox.Text = Result.Title;
            UsernameBox.Text = Result.Username;
            PasswordBox.Password = Result.Password;
            PasswordTextBox.Text = Result.Password;
            UrlBox.Text = Result.Url;
            CommentBox.Text = Result.Comment;
        }

        // Dirty tracking: attach handlers after initial values are assigned.
        TitleBox.TextChanged += (_, _) => { if (_dirtyTrackingReady) UpdateDirtyState(); };
        UsernameBox.TextChanged += (_, _) => { if (_dirtyTrackingReady) UpdateDirtyState(); };
        UrlBox.TextChanged += (_, _) => { if (_dirtyTrackingReady) UpdateDirtyState(); };
        CommentBox.TextChanged += CommentBox_TextChanged;
        PasswordBox.PasswordChanged += (_, _) => { if (_dirtyTrackingReady) UpdateDirtyState(); };

        SnapshotOriginalValues();
        _dirtyTrackingReady = true;
        UpdateDirtyState();
    }

    /// <summary>
    /// Sets the folder location line shown under the "Comment" label.
    /// - displayName: visible caption (folder name / "No folder" / "Folder not found")
    /// - folderId: null means "No folder" target
    /// - isMissing: true disables navigation (folder removed / not found)
    /// </summary>
    public void SetFolderLocation(string displayName, Guid? folderId, bool isMissing)
    {
        _locationFolderId = folderId;
        _locationIsMissing = isMissing;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            FolderLocationPanel.Visibility = Visibility.Collapsed;
            return;
        }

        FolderLocationText.Text = displayName;
        FolderLocationPanel.Visibility = Visibility.Visible;

        if (isMissing)
        {
            FolderLocationText.IsHitTestVisible = false;
            FolderLocationText.Cursor = Cursors.Arrow;
            FolderLocationText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextDisabled");
        }
        else
        {
            FolderLocationText.IsHitTestVisible = true;
            FolderLocationText.Cursor = Cursors.Hand;
            FolderLocationText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");
        }
    }

    private void FolderLocationText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_locationIsMissing)
            return;

        // Main window is modal owner; we can still change its context programmatically.
        if (_hostOwner is MainWindow mw)
            mw.NavigateToFolderContextFromEntry(_locationFolderId);
    }
    private void ShowPwdToggle_Checked(object sender, RoutedEventArgs e)
    {
        PasswordTextBox.Text = PasswordBox.Password ?? "";
        PasswordTextBox.Visibility = Visibility.Visible;
        PasswordBox.Visibility = Visibility.Collapsed;

        // Update tooltip to "Hide"
        ShowPwdToggle.ToolTip = Loc.Instance["HidePassword"];
    }

    private void ShowPwdToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        PasswordBox.Password = PasswordTextBox.Text ?? "";
        PasswordBox.Visibility = Visibility.Visible;
        PasswordTextBox.Visibility = Visibility.Collapsed;

        ShowPwdToggle.ToolTip = Loc.Instance["ShowPassword"];
    }

    private void ShowCopyToast(Popup popup)
    {
        _copyToast.Show(popup);
    }

    private void CopyPwdButton_Click(object sender, RoutedEventArgs e)
    {
        var ok = ClipboardSecurity.TryCopySecret(PasswordBox.Password, out _);
        ShowCopyToast(ok ? CopyPasswordToastPopup : CopyPasswordFailedToastPopup);
    }

    private void CopyLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var ok = ClipboardSecurity.TryCopyLogin(UsernameBox.Text, out _);
        ShowCopyToast(ok ? CopyLoginToastPopup : CopyLoginFailedToastPopup);
    }

    private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsLog.AppendLine("ENTRY_EDITOR_URL_COPY_BEGIN", $"source={(sender is MenuItem ? "context_menu" : "inline_button")}");
        var toastAnchor = sender is MenuItem ? UrlBox as UIElement : sender as UIElement ?? UrlBox;

        var ok = EntryUrlActions.TryCopy(UrlBox.Text, out var failureReason);
        if (ok)
        {
            DiagnosticsLog.AppendLine("ENTRY_EDITOR_URL_COPY_END", "result=ok");
            ShowInfoToast(Loc.Instance["Copied"], toastAnchor, 2200);
        }
        else
        {
            DiagnosticsLog.AppendLine("ENTRY_EDITOR_URL_COPY_END", $"result=fail reason={(failureReason ?? "unknown")}");
            ShowInfoToast(Loc.Instance["CopyFailed"], toastAnchor, 2600);
        }
    }

    private void OpenUrlButton_Click(object sender, RoutedEventArgs e)
    {
        EntryUrlActions.TryOpenInBrowser(UrlBox.Text, "ENTRY_EDITOR_URL_OPEN");
    }

    private void OpenGeneratorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hostOwner != null)
            _hostOwner.ShowHostedEntryPasswordGeneratorDialog(this);
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_hostOwner is MainWindow mw)
            {
                if (mw.LockCommand != null && mw.LockCommand.CanExecute(null))
                    mw.LockCommand.Execute(null);
            }
        }
        catch
        {
            // Best-effort only.
        }
    }

    // --------------------
    // Attachments
    // --------------------

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
                PlacementTarget = AttachmentAddBtn as UIElement ?? (AttachmentsList as UIElement ?? Content as UIElement),
                Child = border
            };
        }
        catch
        {
            // best-effort
            _infoToastPopup = null;
            _infoToastText = null;
        }
    }

    private void ShowInfoToast(string message, UIElement? placementTarget = null, int? durationMs = null)
    {
        try
        {
            EnsureInfoToast();
            if (_infoToastPopup == null || _infoToastText == null)
                return;

            _infoToastText.Text = message ?? "";
            if (placementTarget != null)
                _infoToastPopup.PlacementTarget = placementTarget;

            _infoToast.Show(_infoToastPopup, durationMs);
        }
        catch
        {
            // ignore
        }
    }

    private void EntryUrlContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        var canCopy = EntryUrlActions.CanCopy(UrlBox.Text);
        var canOpen = EntryUrlActions.CanOpenInBrowser(UrlBox.Text);

        foreach (var item in menu.Items)
        {
            if (item is not MenuItem mi)
                continue;

            var tag = mi.Tag as string;
            if (string.Equals(tag, "CopyUrl", StringComparison.OrdinalIgnoreCase))
            {
                mi.IsEnabled = canCopy;
            }
            else if (string.Equals(tag, "OpenUrlInBrowser", StringComparison.OrdinalIgnoreCase))
            {
                mi.IsEnabled = canOpen;
            }
        }
    }

}
