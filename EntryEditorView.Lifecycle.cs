using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PassNotes;

public partial class EntryEditorView
{
    private void SetCommentEditingEnabled(bool enabled)
    {
        try { CommentBox.IsEnabled = enabled; } catch { }
    }

    private void RunHostedKeyboardActionDeferred(string context, Action action)
    {
        if (_isHostedMode && _hostOwner != null)
        {
            _hostOwner.RunHostedKeyboardActionDeferred(context, action);
            return;
        }

        action();
    }

    internal bool TryHandleHostedCloseRequest()
    {
        if (_suppressUnsavedPrompt || !_dirtyTrackingReady || !_isDirty)
        {
            HostedCancelled?.Invoke();
            return true;
        }

        if (_unsavedPromptInProgress)
            return true;

        _unsavedPromptInProgress = true;

        try
        {
            var res = AppMessageDialogWindow.ShowYesNoCancel(
                GetDialogOwnerForHostedAwareDialogs(),
                Loc.Instance["UnsavedChangesTitle"],
                Loc.Instance["UnsavedChangesMessage"]);

            if (res == MessageBoxResult.Cancel)
            {
                _unsavedPromptInProgress = false;
                return true;
            }

            if (res == MessageBoxResult.Yes)
            {
                if (!TrySaveAndClose())
                {
                    _unsavedPromptInProgress = false;
                    _suppressUnsavedPrompt = false;
                }

                return true;
            }

            _suppressUnsavedPrompt = true;
            HostedCancelled?.Invoke();
            return true;
        }
        catch
        {
            _unsavedPromptInProgress = false;
            _suppressUnsavedPrompt = false;
            return true;
        }
    }

    private Window GetDialogOwnerForHostedAwareDialogs()
        => _hostOwner ?? Window.GetWindow(this) ?? Application.Current?.MainWindow ?? throw new InvalidOperationException("Hosted Entry owner is not available.");

    private IntPtr GetDialogOwnerHandleForHostedAwareDialogs()
    {
        try
        {
            return new System.Windows.Interop.WindowInteropHelper(GetDialogOwnerForHostedAwareDialogs()).Handle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private void Close()
    {
        if (_isHostedMode)
        {
            HostedCancelled?.Invoke();
            return;
        }
    }

    private void CleanupAfterClose()
    {
        if (!_keepPendingAttachmentsOnClose)
        {
            try { CleanupPendingAttachmentsSessionBestEffort("window_closed"); } catch { }
        }

        SetCommentEditingEnabled(true);
    }

    private void CommentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (_dirtyTrackingReady)
                UpdateDirtyState();
        }
        catch { }

        try { CommentTextChangedForExternal?.Invoke(this, EventArgs.Empty); } catch { }
    }

    public EntryEditorDraft CaptureDraft()
    {
        try
        {
            if (_pendingAttachmentAdds.Count > 0 || _pendingAttachmentDeletes.Count > 0)
            {
                DiagnosticsLog.AppendLine("PENDING_DRAFT_CAPTURE",
                    $"entry={Result.Id:N} isNew={_isNewEntry} adds={_pendingAttachmentAdds.Count} deletes={_pendingAttachmentDeletes.Count}");
            }
        }
        catch { }

        return new EntryEditorDraft
        {
            IsNew = _isNewEntry,
            EntryId = Result.Id,
            FolderId = Result.FolderId,
            Title = (TitleBox.Text ?? "").Trim(),
            Username = (UsernameBox.Text ?? "").Trim(),
            Password = PasswordBox.Visibility == Visibility.Visible ? (PasswordBox.Password ?? "") : (PasswordTextBox.Text ?? ""),
            Url = (UrlBox.Text ?? "").Trim(),
            Comment = CommentBox.Text ?? "",
            PendingAttachmentAdds = _pendingAttachmentAdds.Select(x => new EntryEditorDraft.PendingAttachmentAddDraft
            {
                DraftId = x.DraftId,
                EncryptedPath = x.EncryptedPath,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                OriginalPath = x.OriginalPath
            }).ToArray(),
            PendingAttachmentDeleteIds = _pendingAttachmentDeletes.ToArray()
        };
    }

    public void ApplyDraft(EntryEditorDraft d)
    {
        TitleBox.Text = d.Title ?? "";
        UsernameBox.Text = d.Username ?? "";
        PasswordBox.Password = d.Password ?? "";
        PasswordTextBox.Text = d.Password ?? "";
        UrlBox.Text = d.Url ?? "";
        CommentBox.Text = d.Comment ?? "";

        Result.FolderId = d.FolderId;

        try
        {
            _pendingAttachmentAdds.Clear();
            _pendingAttachmentDeletes.Clear();
        }
        catch { }

        try
        {
            if (d.PendingAttachmentDeleteIds != null)
            {
                foreach (var id in d.PendingAttachmentDeleteIds)
                    _pendingAttachmentDeletes.Add(id);
            }
        }
        catch { }

        try
        {
            if (d.PendingAttachmentAdds != null)
            {
                foreach (var p in d.PendingAttachmentAdds)
                    TryRestorePendingEncryptedAttachment(p);
            }
        }
        catch { }

        try
        {
            if (_pendingAttachmentAdds.Count > 0 || _pendingAttachmentDeletes.Count > 0)
            {
                DiagnosticsLog.AppendLine("PENDING_DRAFT_RESTORE",
                    $"entry={Result.Id:N} isNew={_isNewEntry} adds={_pendingAttachmentAdds.Count} deletes={_pendingAttachmentDeletes.Count}");
            }
        }
        catch { }

        try { RefreshAttachments(); } catch { }

        if (_dirtyTrackingReady)
            UpdateDirtyState();
    }

    public void ForceCloseForLock()
    {
        try { _suppressUnsavedPrompt = true; } catch { }
        try { _unsavedPromptInProgress = false; } catch { }
        try { _keepPendingAttachmentsOnClose = true; } catch { }

        if (_isHostedMode)
        {
            HostedCancelled?.Invoke();
            return;
        }

        try
        {
            DialogResult = false;
        }
        catch
        {
            try { Close(); } catch { }
        }
    }

    public void ForceCloseDiscardChangesForGate()
    {
        try { _suppressUnsavedPrompt = true; } catch { }
        try { _unsavedPromptInProgress = false; } catch { }

        if (_isHostedMode)
        {
            HostedCancelled?.Invoke();
            return;
        }

        try
        {
            DialogResult = false;
        }
        catch
        {
            try { Close(); } catch { }
        }
    }

    public bool TrySaveAndCloseForGate()
    {
        try
        {
            if (_isHostedMode)
                return TrySaveAndClose();

            if (!TrySaveEntry())
                return false;

            _suppressUnsavedPrompt = true;

            try
            {
                DialogResult = true;
            }
            catch
            {
                try { Close(); } catch { }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isHostedMode)
        {
            TryHandleHostedCloseRequest();
            return;
        }

        Close();
    }

    private void EntryWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_isHostedMode)
            {
                RunHostedKeyboardActionDeferred("EntryEditorEscape", () => TryHandleHostedCloseRequest());
                return;
            }

            Close();
        }
    }

    private void CommentBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (e.Key == Key.Enter || e.Key == Key.Return))
        {
            e.Handled = true;
            RunHostedKeyboardActionDeferred("EntryEditorCommentCtrlEnter", () => Ok_Click(this, new RoutedEventArgs()));
        }
    }

    private void OpenCommentWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_hostOwner != null)
            _hostOwner.ShowHostedEntryCommentDialog(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _ = TrySaveAndClose();
    }

    private bool TrySaveAndClose()
    {
        if (!TrySaveEntry())
            return false;

        _suppressUnsavedPrompt = true;

        if (_isHostedMode)
        {
            HostedSaved?.Invoke(Result);
            return true;
        }

        try
        {
            DialogResult = true;
        }
        catch
        {
            try { Close(); } catch { }
        }

        return true;
    }

    private bool TrySaveEntry()
    {
        if (_isSaving)
            return false;

        _isSaving = true;

        try
        {
            Result.Title = TitleBox.Text?.Trim() ?? "";
            Result.Username = UsernameBox.Text?.Trim() ?? "";
            Result.Password = PasswordBox.Visibility == Visibility.Visible
                ? (PasswordBox.Password ?? "")
                : (PasswordTextBox.Text ?? "");
            Result.Url = UrlBox.Text?.Trim() ?? "";
            Result.Comment = CommentBox.Text ?? "";
            Result.UpdatedUtc = DateTime.UtcNow;

            if (_pendingAttachmentAdds.Count > 0 || _pendingAttachmentDeletes.Count > 0)
            {
                var mw = _hostOwner;
                if (mw == null)
                {
                    try
                    {
                        DiagnosticsLog.AppendLine("PENDING_ERROR",
                            "phase=commit tag=no_owner ex=InvalidOperation");
                    }
                    catch { }

                    ShowInfoToast(Loc.Instance["AttachmentsSaveFailed"], AttachmentAddBtn);
                    return false;
                }

                try
                {
                    var missing = _pendingAttachmentAdds
                        .Where(x => string.IsNullOrWhiteSpace(x.EncryptedPath) || !System.IO.File.Exists(x.EncryptedPath))
                        .Select(x => x.FileName)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Take(3)
                        .ToArray();

                    if (missing.Length > 0)
                    {
                        try
                        {
                            DiagnosticsLog.AppendLine("PENDING_ERROR",
                                $"phase=preflight tag=missing_blob ex=FileNotFound names={string.Join("|", missing)}");
                        }
                        catch { }

                        ShowInfoToast($"{Loc.Instance["AttachmentsSaveFailed"]} {Loc.Instance["IoFileNotFound"]}", AttachmentAddBtn);
                        return false;
                    }
                }
                catch { }

                var add = _pendingAttachmentAdds.Select(x => new AttachmentDraftAddEncrypted
                {
                    EncryptedPath = x.EncryptedPath,
                    FileName = x.FileName,
                    SizeBytes = x.SizeBytes
                }).ToArray();
                var del = _pendingAttachmentDeletes.ToArray();

                Exception? applyErr = null;
                var added = 0;
                var removed = 0;
                bool ok;
                try
                {
                    ok = VaultIoGate.Run("EntryEditorView.TrySaveEntry.ApplyAttachmentDraft", () =>
                        _hostOwner!.TryApplyEncryptedAttachmentDraft(Result.Id, del, add, out added, out removed, out applyErr));
                }
                catch (Exception ex)
                {
                    applyErr = ex;
                    ok = false;
                }

                if (!ok)
                {
                    try
                    {
                        DiagnosticsLog.AppendLine("PENDING_ERROR",
                            $"phase=commit tag=commit ex={(applyErr?.GetType().Name ?? "unknown")}");
                    }
                    catch { }

                    if (applyErr != null)
                        ShowAttachmentIoError("AttachmentsSaveFailed", applyErr, AttachmentAddBtn);
                    else
                        ShowInfoToast(Loc.Instance["AttachmentsSaveFailed"], AttachmentAddBtn);

                    return false;
                }

                try
                {
                    DiagnosticsLog.AppendLine("PENDING_COMMIT",
                        $"added={added} removed={removed}");
                }
                catch { }

                try { _pendingAttachmentDeletes.Clear(); } catch { }

                if (_pendingAttachmentAdds.Count > 0 || !string.IsNullOrWhiteSpace(_pendingAttachmentsSessionDir))
                    CleanupPendingAttachmentsSessionBestEffort("commit");
            }

            _isDirty = false;

            if (_dirtyTrackingReady)
                SnapshotOriginalValues();

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void SnapshotOriginalValues()
    {
        _origTitle = (TitleBox.Text ?? "").Trim();
        _origUsername = (UsernameBox.Text ?? "").Trim();
        _origPassword = PasswordBox.Password ?? "";
        _origUrl = (UrlBox.Text ?? "").Trim();
        _origComment = CommentBox.Text ?? "";
    }

    private void UpdateDirtyState()
    {
        var curTitle = (TitleBox.Text ?? "").Trim();
        var curUsername = (UsernameBox.Text ?? "").Trim();
        var curPassword = PasswordBox.Password ?? "";
        var curUrl = (UrlBox.Text ?? "").Trim();
        var curComment = CommentBox.Text ?? "";

        var attachmentsDirty = (_pendingAttachmentAdds.Count > 0 || _pendingAttachmentDeletes.Count > 0);

        _isDirty = attachmentsDirty ||
            !string.Equals(curTitle, _origTitle, StringComparison.Ordinal) ||
            !string.Equals(curUsername, _origUsername, StringComparison.Ordinal) ||
            !string.Equals(curPassword, _origPassword, StringComparison.Ordinal) ||
            !string.Equals(curUrl, _origUrl, StringComparison.Ordinal) ||
            !string.Equals(curComment, _origComment, StringComparison.Ordinal);
    }
}
