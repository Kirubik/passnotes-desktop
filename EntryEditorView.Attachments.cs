using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace PassNotes;

public partial class EntryEditorView
{
    // Pending attachments temp store cleanup should run at most once per process.
    private static int _pendingTtlCleanupOnceFlag = 0;

    // --------------------
    // Attachments UI
    // --------------------
    private readonly ObservableCollection<AttachmentListItem> _attachments = new();
    private Window? _attachmentsSelectionOwnerWindow;

    private sealed class PendingAttachmentAdd
    {
        public Guid DraftId { get; init; } = Guid.NewGuid();
        public string EncryptedPath { get; init; } = "";
        public string FileName { get; init; } = "";
        public long SizeBytes { get; init; }
        public string? OriginalPath { get; init; }
    }

    private Guid? _pendingAttachmentsSessionId;
    private string? _pendingAttachmentsSessionDir;
    private bool _keepPendingAttachmentsOnClose;
    private readonly System.Collections.Generic.List<PendingAttachmentAdd> _pendingAttachmentAdds = new();
    private readonly System.Collections.Generic.HashSet<Guid> _pendingAttachmentDeletes = new();

    private static void TryCleanupStalePendingAttachmentSessionsOnce()
    {
        try
        {
            if (System.Threading.Interlocked.Exchange(ref _pendingTtlCleanupOnceFlag, 1) != 0)
                return;

            CleanupStalePendingAttachmentSessionsBestEffort(TimeSpan.FromDays(3));
        }
        catch
        {
            // best-effort
        }
    }

    private static void CleanupStalePendingAttachmentSessionsBestEffort(TimeSpan maxAge)
    {
        try
        {
            var baseDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PassNotes",
                "PendingAttachments");

            if (!System.IO.Directory.Exists(baseDir))
                return;

            var cutoffUtc = DateTime.UtcNow - maxAge;
            var removed = 0;

            foreach (var ymDir in System.IO.Directory.EnumerateDirectories(baseDir))
            {
                try
                {
                    foreach (var sessionDir in System.IO.Directory.EnumerateDirectories(ymDir))
                    {
                        try
                        {
                            var lastWriteUtc = System.IO.Directory.GetLastWriteTimeUtc(sessionDir);
                            if (lastWriteUtc <= cutoffUtc)
                            {
                                try { System.IO.Directory.Delete(sessionDir, true); } catch { }
                                removed++;
                            }
                        }
                        catch { }
                    }

                    try
                    {
                        if (!System.IO.Directory.EnumerateFileSystemEntries(ymDir).Any())
                            System.IO.Directory.Delete(ymDir, false);
                    }
                    catch { }
                }
                catch { }
            }

            if (removed > 0)
                DiagnosticsLog.AppendLine("PENDING_TTL_CLEANUP", $"removedSessions={removed}");
        }
        catch
        {
            // best-effort
        }
    }

    private string GetIoReason(Exception ex)
    {
        try
        {
            if (ex is System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException)
                return Loc.Instance["IoFileNotFound"];

            if (ex is UnauthorizedAccessException)
                return Loc.Instance["IoAccessDenied"];

            if (ex is System.IO.PathTooLongException)
                return Loc.Instance["IoPathTooLong"];

            if (ex is System.IO.IOException io)
            {
                var hr = (uint)io.HResult;
                if (hr == 0x80070020u || hr == 0x80070021u)
                    return Loc.Instance["IoFileInUse"];
            }

            if (ex is Win32Exception w32 && w32.NativeErrorCode == 5)
                return Loc.Instance["IoAccessDenied"];

            return Loc.Instance["IoOperationFailed"];
        }
        catch
        {
            return Loc.Instance["Error"];
        }
    }

    private void ShowAttachmentIoError(string actionKey, Exception ex, UIElement? anchor = null)
    {
        try
        {
            var action = Loc.Instance[actionKey];
            var reason = GetIoReason(ex);
            ShowInfoToast($"{action} {reason}", anchor);
        }
        catch
        {
            // ignore
        }
    }

    private static string? NormalizePath(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            return System.IO.Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private string EnsurePendingAttachmentsSessionDir()
    {
        if (!string.IsNullOrWhiteSpace(_pendingAttachmentsSessionDir))
            return _pendingAttachmentsSessionDir!;

        _pendingAttachmentsSessionId ??= Guid.NewGuid();
        var baseDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PassNotes",
            "PendingAttachments");

        var ym = DateTime.UtcNow.ToString("yyyy-MM");
        var dir = System.IO.Path.Combine(baseDir, ym, _pendingAttachmentsSessionId.Value.ToString("N"));
        System.IO.Directory.CreateDirectory(dir);

        try
        {
            DiagnosticsLog.AppendLine("PENDING_SESSION_CREATE",
                $"id={_pendingAttachmentsSessionId.Value:N} dir={dir}");
        }
        catch { }

        _pendingAttachmentsSessionDir = dir;
        return dir;
    }

    private void CleanupPendingAttachmentsSessionBestEffort(string reason)
    {
        try
        {
            if (_pendingAttachmentAdds.Count == 0 && string.IsNullOrWhiteSpace(_pendingAttachmentsSessionDir))
                return;

            var removedFiles = 0;

            foreach (var p in _pendingAttachmentAdds)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(p.EncryptedPath) && System.IO.File.Exists(p.EncryptedPath))
                    {
                        System.IO.File.Delete(p.EncryptedPath);
                        removedFiles++;
                    }
                }
                catch { }
            }

            var baseDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PassNotes",
                "PendingAttachments");

            string? sessionDir = _pendingAttachmentsSessionDir;
            if (string.IsNullOrWhiteSpace(sessionDir))
            {
                try
                {
                    var anyEnc = _pendingAttachmentAdds.FirstOrDefault()?.EncryptedPath;
                    if (!string.IsNullOrWhiteSpace(anyEnc))
                        sessionDir = System.IO.Path.GetDirectoryName(anyEnc);
                }
                catch { }
            }

            var baseFull = NormalizePath(baseDir);
            var sessionFull = NormalizePath(sessionDir);
            if (!string.IsNullOrWhiteSpace(baseFull) && !string.IsNullOrWhiteSpace(sessionFull)
                && sessionFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (System.IO.Directory.Exists(sessionFull))
                        System.IO.Directory.Delete(sessionFull, true);
                }
                catch { }

                try
                {
                    var ymDir = System.IO.Path.GetDirectoryName(sessionFull);
                    if (!string.IsNullOrWhiteSpace(ymDir)
                        && ymDir.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)
                        && System.IO.Directory.Exists(ymDir)
                        && !System.IO.Directory.EnumerateFileSystemEntries(ymDir).Any())
                    {
                        System.IO.Directory.Delete(ymDir, false);
                    }
                }
                catch { }
            }

            try
            {
                _pendingAttachmentAdds.Clear();
                _pendingAttachmentDeletes.Clear();
            }
            catch { }

            _pendingAttachmentsSessionDir = null;
            _pendingAttachmentsSessionId = null;

            try
            {
                DiagnosticsLog.AppendLine("PENDING_CLEANUP",
                    $"reason={reason} removedFiles={removedFiles}");
            }
            catch { }
        }
        catch
        {
            // best-effort
        }
    }

    private bool TryAddPendingAttachment(string filePath, out Exception? error)
        => TryAddPendingAttachment(filePath, out error, out _);

    private bool TryAddPendingAttachment(string filePath, out Exception? error, out bool isDuplicate)
    {
        error = null;
        isDuplicate = false;
        try
        {
            var full = NormalizePath(filePath);
            if (string.IsNullOrWhiteSpace(full))
                return false;

            if (!System.IO.File.Exists(full))
            {
                error = new System.IO.FileNotFoundException();
                return false;
            }

            try
            {
                using var _ = System.IO.File.Open(full, System.IO.FileMode.Open, System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }

            var fileName = System.IO.Path.GetFileName(full) ?? "";
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                if (_pendingAttachmentAdds.Any(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    isDuplicate = true;
                    return false;
                }

                if (_hostOwner is MainWindow mwDup)
                {
                    var existing = mwDup.GetAttachmentsForEntry(Result.Id);
                    if (existing.Any(a => !_pendingAttachmentDeletes.Contains(a.Id)
                                          && string.Equals(a.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        isDuplicate = true;
                        return false;
                    }
                }
            }

            if (_pendingAttachmentAdds.Any(x => string.Equals(x.OriginalPath, full, StringComparison.OrdinalIgnoreCase)))
            {
                isDuplicate = true;
                return false;
            }

            if (_hostOwner is not MainWindow mw)
                return false;

            var draftId = Guid.NewGuid();
            var pendingDir = EnsurePendingAttachmentsSessionDir();
            var encPath = System.IO.Path.Combine(pendingDir, $"{draftId:N}.pna");

            if (!mw.TryCreatePendingEncryptedAttachment(full, encPath, out var sizeBytes, out var encErr))
            {
                error = encErr;
                try
                {
                    DiagnosticsLog.AppendLine("PENDING_ERROR",
                        $"tag=encrypt file={System.IO.Path.GetFileName(full)} err={(encErr?.GetType().Name ?? "unknown")}");
                }
                catch { }
                try { if (System.IO.File.Exists(encPath)) System.IO.File.Delete(encPath); } catch { }
                return false;
            }

            _pendingAttachmentAdds.Add(new PendingAttachmentAdd
            {
                DraftId = draftId,
                EncryptedPath = encPath,
                FileName = fileName,
                SizeBytes = sizeBytes,
                OriginalPath = full
            });

            try
            {
                DiagnosticsLog.AppendLine("PENDING_ADD",
                    $"id={draftId:N} name={fileName} size={sizeBytes}");
            }
            catch { }

            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private void TryRestorePendingEncryptedAttachment(EntryEditorDraft.PendingAttachmentAddDraft d)
    {
        try
        {
            if (d == null)
                return;

            var encPath = NormalizePath(d.EncryptedPath);
            if (string.IsNullOrWhiteSpace(encPath) || !System.IO.File.Exists(encPath))
                return;

            var fileName = d.FileName ?? "";
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                if (_pendingAttachmentAdds.Any(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
                    return;

                if (_hostOwner is MainWindow mw)
                {
                    var existing = mw.GetAttachmentsForEntry(Result.Id);
                    if (existing.Any(a => !_pendingAttachmentDeletes.Contains(a.Id)
                                          && string.Equals(a.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
                        return;
                }
            }

            if (_pendingAttachmentAdds.Any(x => x.DraftId == d.DraftId))
                return;

            if (string.IsNullOrWhiteSpace(_pendingAttachmentsSessionDir))
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(encPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                        _pendingAttachmentsSessionDir = dir;
                }
                catch { }
            }

            _pendingAttachmentAdds.Add(new PendingAttachmentAdd
            {
                DraftId = d.DraftId == Guid.Empty ? Guid.NewGuid() : d.DraftId,
                EncryptedPath = encPath,
                FileName = fileName,
                SizeBytes = d.SizeBytes,
                OriginalPath = d.OriginalPath
            });
        }
        catch
        {
            // ignore
        }
    }

    private void RemovePendingAttachmentByDraftId(Guid draftId)
    {
        try
        {
            var idx = _pendingAttachmentAdds.FindIndex(x => x.DraftId == draftId);
            if (idx >= 0)
            {
                var p = _pendingAttachmentAdds[idx];
                _pendingAttachmentAdds.RemoveAt(idx);
                try
                {
                    if (!string.IsNullOrWhiteSpace(p.EncryptedPath) && System.IO.File.Exists(p.EncryptedPath))
                        System.IO.File.Delete(p.EncryptedPath);
                }
                catch { }

                try
                {
                    DiagnosticsLog.AppendLine("PENDING_REMOVE",
                        $"id={draftId:N} name={p.FileName}");
                }
                catch { }
            }
        }
        catch { }
    }

    private PendingAttachmentAdd? FindPendingAddByDraftId(Guid draftId)
    {
        try { return _pendingAttachmentAdds.FirstOrDefault(x => x.DraftId == draftId); }
        catch { return null; }
    }

    private static string GetUniqueCopyPath(string folder, string fileName)
    {
        try
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(fileName) ?? "";
            var ext = System.IO.Path.GetExtension(fileName) ?? "";
            var candidate = System.IO.Path.Combine(folder, fileName);
            if (!System.IO.File.Exists(candidate))
                return candidate;

            for (var i = 1; i < 10_000; i++)
            {
                var name = string.IsNullOrWhiteSpace(baseName)
                    ? $"{i}{ext}"
                    : $"{baseName} ({i}){ext}";
                candidate = System.IO.Path.Combine(folder, name);
                if (!System.IO.File.Exists(candidate))
                    return candidate;
            }

            return System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);
        }
        catch
        {
            return System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N"));
        }
    }

    private void SaveDraftAttachmentAs(PendingAttachmentAdd pending)
    {
        try
        {
            if (pending == null)
                return;

            if (_hostOwner is not MainWindow mw)
                return;

            var encPath = pending.EncryptedPath;
            if (string.IsNullOrWhiteSpace(encPath) || !System.IO.File.Exists(encPath))
            {
                ShowInfoToast($"{Loc.Instance["AttachmentsSaveFailed"]} {Loc.Instance["IoFileNotFound"]}", AttachmentSaveAsBtn);
                return;
            }

            var dlg = new SaveFileDialog
            {
                FileName = pending.FileName,
                OverwritePrompt = true,
                AddExtension = true,
                Title = Loc.Instance["AttachmentsSaveAs"]
            };

            if (dlg.ShowDialog(GetDialogOwnerForHostedAwareDialogs()) != true)
                return;

            var dst = dlg.FileName;
            if (string.IsNullOrWhiteSpace(dst))
                return;

            if (!mw.TrySavePendingEncryptedAttachmentAs(encPath, dst, out var err) && err != null)
                ShowAttachmentIoError("AttachmentsSaveFailed", err, AttachmentSaveAsBtn);
        }
        catch (Exception ex)
        {
            ShowAttachmentIoError("AttachmentsSaveFailed", ex, AttachmentSaveAsBtn);
        }
    }

    private void RefreshAttachments()
    {
        try
        {
            AttachmentsList.IsEnabled = _hostOwner is MainWindow;

            _attachments.Clear();

            if (_hostOwner is MainWindow mw)
            {
                if (!_isNewEntry)
                {
                    var list = mw.GetAttachmentsForEntry(Result.Id);

                    foreach (var a in list
                                 .Where(x => !_pendingAttachmentDeletes.Contains(x.Id))
                                 .OrderByDescending(x => x.CreatedUtc))
                    {
                        _attachments.Add(new AttachmentListItem
                        {
                            Id = a.Id,
                            FileName = a.FileName,
                            SizeBytes = a.SizeBytes,
                            IsDraft = false,
                            DraftEncryptedPath = null,
                            OriginalPath = null
                        });
                    }
                }

                foreach (var p in _pendingAttachmentAdds)
                {
                    _attachments.Add(new AttachmentListItem
                    {
                        Id = p.DraftId,
                        FileName = p.FileName,
                        SizeBytes = p.SizeBytes,
                        IsDraft = true,
                        DraftEncryptedPath = p.EncryptedPath,
                        OriginalPath = p.OriginalPath
                    });
                }
            }
        }
        catch
        {
            // best-effort
        }
        finally
        {
            UpdateAttachmentsButtons();
            if (_dirtyTrackingReady)
                UpdateDirtyState();
        }
    }

    private void UpdateAttachmentsButtons()
    {
        try
        {
            var enabled = _hostOwner is MainWindow;

            int selCount = 0;
            try { selCount = AttachmentsList.SelectedItems?.Count ?? 0; } catch { selCount = AttachmentsList.SelectedItem is AttachmentListItem ? 1 : 0; }

            AttachmentAddBtn.IsEnabled = enabled;
            AttachmentRemoveBtn.IsEnabled = enabled && selCount > 0;
            AttachmentOpenBtn.IsEnabled = enabled && selCount == 1;
            AttachmentSaveAsBtn.IsEnabled = enabled && selCount > 0;
        }
        catch
        {
            // ignore
        }
    }

    private System.Collections.Generic.List<AttachmentListItem> GetSelectedAttachments()
    {
        try
        {
            var list = new System.Collections.Generic.List<AttachmentListItem>();
            foreach (var obj in AttachmentsList.SelectedItems)
            {
                if (obj is AttachmentListItem ali)
                    list.Add(ali);
            }
            return list;
        }
        catch
        {
            var single = AttachmentsList.SelectedItem as AttachmentListItem;
            return single != null ? new System.Collections.Generic.List<AttachmentListItem> { single } : new System.Collections.Generic.List<AttachmentListItem>();
        }
    }

    private AttachmentListItem? GetSingleSelectedAttachment()
    {
        var list = GetSelectedAttachments();
        return list.Count == 1 ? list[0] : null;
    }

    private void AttachmentAdd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Title = Loc.Instance["AttachmentsAdd"]
            };

            if (dlg.ShowDialog(GetDialogOwnerForHostedAwareDialogs()) != true)
                return;

            var any = false;
            var failed = 0;
            var duplicates = 0;
            string? firstDuplicateName = null;
            Exception? firstError = null;

            foreach (var fp in dlg.FileNames ?? Array.Empty<string>())
            {
                if (TryAddPendingAttachment(fp, out var err, out var isDup))
                {
                    any = true;
                }
                else
                {
                    if (isDup)
                    {
                        duplicates++;
                        if (firstDuplicateName == null)
                        {
                            try { firstDuplicateName = System.IO.Path.GetFileName(fp); } catch { firstDuplicateName = null; }
                        }
                    }
                    else if (err != null)
                    {
                        failed++;
                        firstError ??= err;
                    }
                }
            }

            string? dupMsg = null;
            if (duplicates == 1 && !string.IsNullOrWhiteSpace(firstDuplicateName))
                dupMsg = string.Format(Loc.Instance["AttachmentsDuplicateOne"], firstDuplicateName);
            else if (duplicates > 0)
                dupMsg = string.Format(Loc.Instance["AttachmentsDuplicateMany"], duplicates);

            if (!any)
            {
                if (firstError != null)
                    ShowAttachmentIoError("AttachmentsAddFailed", firstError, AttachmentAddBtn);
                else if (dupMsg != null)
                    ShowInfoToast(dupMsg, AttachmentAddBtn);
                return;
            }

            if (failed > 0 && dupMsg != null)
                ShowInfoToast($"{Loc.Instance["AttachmentsAddSomeFailed"]} {dupMsg}", AttachmentAddBtn);
            else if (failed > 0)
                ShowInfoToast(Loc.Instance["AttachmentsAddSomeFailed"], AttachmentAddBtn);
            else if (dupMsg != null)
                ShowInfoToast(dupMsg, AttachmentAddBtn);

            RefreshAttachments();
        }
        catch (Exception ex)
        {
            ShowAttachmentIoError("AttachmentsAddFailed", ex, AttachmentAddBtn);
        }
    }

    private void AttachmentRemove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = GetSelectedAttachments();
            if (items.Count == 0)
                return;

            string msg;
            if (items.Count == 1)
                msg = string.Format(Loc.Instance["AttachmentsRemoveConfirm"], items[0].FileName);
            else
                msg = string.Format(Loc.Instance["AttachmentsRemoveConfirmMany"], items.Count);

            var res = AppMessageDialogWindow.ShowYesNo(
                GetDialogOwnerForHostedAwareDialogs(),
                Loc.Instance["Info"],
                msg);

            if (res != MessageBoxResult.Yes)
                return;

            foreach (var it in items)
            {
                if (it.IsDraft)
                {
                    RemovePendingAttachmentByDraftId(it.Id);
                }
                else
                {
                    if (it.Id != Guid.Empty)
                        _pendingAttachmentDeletes.Add(it.Id);
                }
            }

            RefreshAttachments();
        }
        catch (Exception ex)
        {
            ShowAttachmentIoError("Error", ex, AttachmentRemoveBtn);
        }
    }

    private void AttachmentOpen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var item = GetSingleSelectedAttachment();
            if (item == null)
                return;

            var mw = _hostOwner;
            if (mw == null)
                return;

            if (item.IsDraft)
            {
                var p = FindPendingAddByDraftId(item.Id);
                var encPath = p?.EncryptedPath ?? item.DraftEncryptedPath;
                if (!string.IsNullOrWhiteSpace(encPath))
                {
                    if (!mw.TryOpenPendingEncryptedAttachment(encPath, p?.FileName ?? item.FileName, out var openErr) && openErr != null)
                        ShowAttachmentIoError("AttachmentsOpenFailed", openErr, AttachmentOpenBtn);
                }
                return;
            }

            if (!mw.TryOpenAttachment(item.Id, out var err) && err != null)
                ShowAttachmentIoError("AttachmentsOpenFailed", err, AttachmentOpenBtn);
        }
        catch (Exception ex)
        {
            ShowAttachmentIoError("AttachmentsOpenFailed", ex, AttachmentOpenBtn);
        }
    }

    private void AttachmentSaveAs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = GetSelectedAttachments();
            if (items.Count == 0)
                return;

            var mw = _hostOwner;

            if (items.Count == 1)
            {
                var it = items[0];
                if (it.IsDraft)
                {
                    var p = FindPendingAddByDraftId(it.Id);
                    if (p != null)
                        SaveDraftAttachmentAs(p);
                    return;
                }

                if (mw != null)
                {
                    var (ok, canceled, err) = mw.TrySaveAttachmentAs(it.Id);
                    if (!ok && !canceled && err != null)
                        ShowAttachmentIoError("AttachmentsSaveFailed", err, AttachmentSaveAsBtn);
                }
                return;
            }

            var folder = PickFolderForAttachmentsSave();
            if (string.IsNullOrWhiteSpace(folder))
                return;

            int saved = 0;
            int failed = 0;
            Exception? firstError = null;

            if (mw != null)
            {
                var existingIds = items.Where(x => !x.IsDraft).Select(x => x.Id).Where(x => x != Guid.Empty).ToList();
                if (existingIds.Count > 0)
                {
                    var (savedExisting, failedExisting, err) = mw.SaveAttachmentsToFolder(existingIds, folder);
                    saved += savedExisting;
                    failed += failedExisting;

                    if (err != null)
                        ShowAttachmentIoError("AttachmentsSaveManyFailed", err, AttachmentSaveAsBtn);
                }
            }

            foreach (var it in items.Where(x => x.IsDraft))
            {
                try
                {
                    var p = FindPendingAddByDraftId(it.Id);
                    var encPath = p?.EncryptedPath ?? it.DraftEncryptedPath;
                    if (string.IsNullOrWhiteSpace(encPath) || !System.IO.File.Exists(encPath) || mw == null)
                    {
                        failed++;
                        continue;
                    }

                    var name = p?.FileName ?? it.FileName;
                    var dst = GetUniqueCopyPath(folder, name);
                    if (mw.TrySavePendingEncryptedAttachmentAs(encPath, dst, out var err))
                    {
                        saved++;
                    }
                    else
                    {
                        failed++;
                        if (err != null)
                            firstError = err;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            if (failed > 0)
                ShowInfoToast(string.Format(Loc.Instance["AttachmentsSaveManyResult"], saved, items.Count, folder), AttachmentSaveAsBtn, 3200);

            if (firstError != null)
                ShowAttachmentIoError("AttachmentsSaveManyFailed", firstError, AttachmentSaveAsBtn);
        }
        catch (Exception ex)
        {
            ShowAttachmentIoError("AttachmentsSaveManyFailed", ex, AttachmentSaveAsBtn);
        }
    }

    private void AttachmentsList_PreviewDragOver(object sender, DragEventArgs e)
    {
        try
        {
            if (_hostOwner is not MainWindow)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }
        catch
        {
            // best-effort
        }
    }

    private void AttachmentsList_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
                return;

            var unique = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (unique.Length == 0)
                return;

            var any = false;
            var failed = 0;
            var duplicates = 0;
            string? firstDuplicateName = null;
            Exception? firstError = null;

            foreach (var fp in unique)
            {
                if (TryAddPendingAttachment(fp, out var err, out var isDup))
                {
                    any = true;
                }
                else
                {
                    if (isDup)
                    {
                        duplicates++;
                        if (firstDuplicateName == null)
                        {
                            try { firstDuplicateName = System.IO.Path.GetFileName(fp); } catch { firstDuplicateName = null; }
                        }
                    }
                    else if (err != null)
                    {
                        failed++;
                        firstError ??= err;
                    }
                }
            }

            string? dupMsg = null;
            if (duplicates == 1 && !string.IsNullOrWhiteSpace(firstDuplicateName))
                dupMsg = string.Format(Loc.Instance["AttachmentsDuplicateOne"], firstDuplicateName);
            else if (duplicates > 0)
                dupMsg = string.Format(Loc.Instance["AttachmentsDuplicateMany"], duplicates);

            if (any)
                RefreshAttachments();

            if (!any && firstError != null)
                ShowAttachmentIoError("AttachmentsAddFailed", firstError, AttachmentAddBtn);
            else if (!any && dupMsg != null)
                ShowInfoToast(dupMsg, AttachmentAddBtn);
            else if (failed > 0 && dupMsg != null)
                ShowInfoToast($"{Loc.Instance["AttachmentsAddSomeFailed"]} {dupMsg}", AttachmentAddBtn);
            else if (failed > 0)
                ShowInfoToast(Loc.Instance["AttachmentsAddSomeFailed"], AttachmentAddBtn);
            else if (dupMsg != null)
                ShowInfoToast(dupMsg, AttachmentAddBtn);
        }
        catch (Exception ex)
        {
            ShowAttachmentIoError("AttachmentsAddFailed", ex, AttachmentAddBtn);
        }
    }

    private void AttachmentsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (GetSingleSelectedAttachment() == null)
                return;

            AttachmentOpen_Click(this, new RoutedEventArgs());
        }
        catch
        {
            // ignore
        }
    }

    private void EntryEditorView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = Window.GetWindow(this) ?? _hostOwner;
            if (ReferenceEquals(window, _attachmentsSelectionOwnerWindow))
                return;

            if (_attachmentsSelectionOwnerWindow != null)
                _attachmentsSelectionOwnerWindow.Deactivated -= AttachmentsOwnerWindow_Deactivated;

            _attachmentsSelectionOwnerWindow = window;

            if (_attachmentsSelectionOwnerWindow != null)
                _attachmentsSelectionOwnerWindow.Deactivated += AttachmentsOwnerWindow_Deactivated;
        }
        catch
        {
            // best-effort
        }
    }

    private void EntryEditorView_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_attachmentsSelectionOwnerWindow != null)
                _attachmentsSelectionOwnerWindow.Deactivated -= AttachmentsOwnerWindow_Deactivated;

            _attachmentsSelectionOwnerWindow = null;
        }
        catch
        {
            // best-effort
        }
    }

    private void AttachmentsOwnerWindow_Deactivated(object? sender, EventArgs e)
        => ClearAttachmentsSelectionBestEffort();

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match)
                return match;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void ClearAttachmentsSelectionBestEffort()
    {
        try
        {
            var list = AttachmentsList;
            if (list == null)
                return;

            var hasSelection = false;
            try { hasSelection = (list.SelectedItems?.Count ?? 0) > 0; } catch { }
            if (!hasSelection)
                hasSelection = list.SelectedItem != null;

            if (!hasSelection)
                return;

            try { list.UnselectAll(); } catch { }
            try
            {
                var selectedItems = list.SelectedItems;
                if (selectedItems != null)
                    selectedItems.Clear();
            }
            catch { }
            try { list.SelectedItem = null; } catch { }

            UpdateAttachmentsButtons();
        }
        catch
        {
            // best-effort
        }
    }

    private void AttachmentsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is not ListBox list)
                return;

            var dep = e.OriginalSource as DependencyObject;
            if (dep == null)
                return;

            if (FindVisualParent<ScrollBar>(dep) != null)
                return;

            var item = ItemsControl.ContainerFromElement(list, dep) as ListBoxItem;
            if (item != null)
                return;

            ClearAttachmentsSelectionBestEffort();
        }
        catch
        {
            // best-effort
        }
    }

    private void EntryEditorView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var dep = e.OriginalSource as DependencyObject;
            if (dep == null)
                return;

            if (FindVisualParent<ListBox>(dep) == AttachmentsList)
                return;

            var button = FindVisualParent<Button>(dep);
            if (button == AttachmentAddBtn
                || button == AttachmentRemoveBtn
                || button == AttachmentOpenBtn
                || button == AttachmentSaveAsBtn)
            {
                return;
            }

            if (FindVisualParent<Popup>(dep) != null || FindVisualParent<ContextMenu>(dep) != null)
                return;

            ClearAttachmentsSelectionBestEffort();
        }
        catch
        {
            // best-effort
        }
    }

    private void AttachmentsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.A)
            {
                e.Handled = true;
                try
                {
                    AttachmentsList.SelectedItems.Clear();
                    foreach (var it in AttachmentsList.Items)
                        AttachmentsList.SelectedItems.Add(it);
                }
                catch { }
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (GetSelectedAttachments().Count == 0)
                    return;

                e.Handled = true;
                AttachmentRemove_Click(this, new RoutedEventArgs());
            }
        }
        catch
        {
            // ignore
        }
    }

    private void AttachmentsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is not ListBox list)
                return;

            var dep = e.OriginalSource as DependencyObject;
            if (dep == null)
                return;

            if (FindVisualParent<ScrollBar>(dep) != null)
                return;

            var item = ItemsControl.ContainerFromElement(list, dep) as ListBoxItem;
            if (item == null)
            {
                ClearAttachmentsSelectionBestEffort();
                return;
            }

            var dc = item.DataContext;

            if (dc != null && !list.SelectedItems.Contains(dc))
            {
                try { list.SelectedItems.Clear(); } catch { }
                try { list.SelectedItems.Add(dc); } catch { list.SelectedItem = dc; }
            }

            item.Focus();
        }
        catch
        {
            // best-effort
        }
    }

    private sealed class Win32Window : Forms.IWin32Window
    {
        public Win32Window(IntPtr handle) { Handle = handle; }
        public IntPtr Handle { get; }
    }

    private string? PickFolderForAttachmentsSave()
    {
        try
        {
            var dlg = new Forms.FolderBrowserDialog
            {
                Description = Loc.Instance["AttachmentsSelectFolderToSave"],
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            var handle = GetDialogOwnerHandleForHostedAwareDialogs();
            if (handle == IntPtr.Zero)
                return null;

            var res = dlg.ShowDialog(new Win32Window(handle));
            if (res != Forms.DialogResult.OK)
                return null;

            return string.IsNullOrWhiteSpace(dlg.SelectedPath) ? null : dlg.SelectedPath;
        }
        catch
        {
            return null;
        }
    }

    private void AttachmentsContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            var count = GetSelectedAttachments().Count;

            AttachmentsMenuOpen.IsEnabled = count == 1;

            AttachmentsMenuSaveAs.IsEnabled = count > 0;
            AttachmentsMenuSaveAs.Header = count <= 1 ? Loc.Instance["AttachmentsSaveAs"] : Loc.Instance["AttachmentsSaveSelected"];

            AttachmentsMenuCopyName.IsEnabled = count > 0;
            AttachmentsMenuCopyName.Header = count <= 1 ? Loc.Instance["CopyFileName"] : Loc.Instance["CopyFileNames"];

            AttachmentsMenuRemove.IsEnabled = count > 0;
            AttachmentsMenuRemove.Header = count <= 1 ? Loc.Instance["AttachmentsRemove"] : Loc.Instance["AttachmentsRemoveSelected"];
        }
        catch
        {
            // ignore
        }
    }

    private void AttachmentCopyFileName_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = GetSelectedAttachments();
            if (items.Count == 0)
                return;

            string text;
            if (items.Count == 1)
            {
                var item = items[0];
                text = string.IsNullOrWhiteSpace(item.FileName) ? item.Id.ToString("N") : item.FileName;
            }
            else
            {
                text = string.Join(Environment.NewLine, items.Select(x => string.IsNullOrWhiteSpace(x.FileName) ? x.Id.ToString("N") : x.FileName));
            }

            var ok = ClipboardSecurity.TryCopyText(text, out _);
            ShowCopyToast(ok ? CopyAttachmentNameToastPopup : CopyAttachmentNameFailedToastPopup);
        }
        catch
        {
            // ignore
        }
    }
}
