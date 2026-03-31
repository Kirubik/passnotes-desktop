using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace PassNotes;

/// <summary>
/// Minimal backup operations for the encrypted vault file.
/// Default backup folder: %APPDATA%\PassNotes\Backups
/// </summary>
public static class BackupService
{
    public static string VaultFilePath
    {
        get
        {
            var p = App.Settings.VaultPath;
            if (string.IsNullOrWhiteSpace(p))
                p = SettingsStore.GetDefaultVaultPath();
            p = p.Trim();
            // Treat relative paths as relative to the app data folder.
            if (!Path.IsPathRooted(p))
                return Path.Combine(SettingsStore.GetAppDir(), p);
            return p;
        }
    }

    public static string BackupsFolderPath
    {
        get
        {
            var p = App.Settings.BackupsFolderPath;
            if (string.IsNullOrWhiteSpace(p))
                p = SettingsStore.GetDefaultBackupsFolderPath();
            p = p.Trim();
            if (!Path.IsPathRooted(p))
                return Path.Combine(SettingsStore.GetAppDir(), p);
            return p;
        }
    }

    /// <summary>
    /// Creates a regular backup. Copies the vault file and its attachments sidecar.
    /// Attachments copy is NOT best-effort: if attachments can't be copied, the backup is not created.
    /// </summary>
    public static string CreateBackupNow()
        => CreateBackupInternal(trigger: "Manual", masterPasswordForVerify: null);

    /// <summary>
    /// Creates a regular backup and validates that all attachment blobs referenced by the vault
    /// exist in the created sidecar folder. Requires the current master password.
    /// Used by the UI action to avoid creating a backup that can't be restored.
    /// </summary>
    public static string CreateBackupNowValidated(string masterPassword)
        => CreateBackupInternal(trigger: "Manual", masterPasswordForVerify: masterPassword);

    internal static string CreateBackupNowAuto()
        => CreateBackupInternal(trigger: "Auto", masterPasswordForVerify: null);

    private static string CreateBackupInternal(string trigger, string? masterPasswordForVerify)
    {
        return VaultIoGate.Run("BackupService.CreateBackupNow", () =>
        {
            Directory.CreateDirectory(BackupsFolderPath);

            if (!File.Exists(VaultFilePath))
                throw new FileNotFoundException("Vault file not found", VaultFilePath);

            var ext = Path.GetExtension(VaultFilePath);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var baseName = $"PassNotesBackup_{stamp}";

            var backupPath = Path.Combine(BackupsFolderPath, baseName + ext);
            var i = 1;
            while (File.Exists(backupPath))
            {
                backupPath = Path.Combine(BackupsFolderPath, $"{baseName}_{i}{ext}");
                i++;
            }

            File.Copy(VaultFilePath, backupPath, overwrite: false);

            var dstAtt = backupPath + ".attachments";

            try
            {
                CopyAttachmentsSidecarStrict(sourceVaultPath: VaultFilePath, dstAttDir: dstAtt, trigger: trigger);

                // Optional stronger validation: open the created backup and ensure it contains
                // all blobs referenced by the vault metadata.
                if (!string.IsNullOrWhiteSpace(masterPasswordForVerify))
                    ValidateBackupAttachmentsStrict(backupPath, masterPasswordForVerify!, trigger);
            }
            catch (BackupCreateFailedException)
            {
                CleanupFailedBackup(backupPath);
                throw;
            }
            catch (Exception ex)
            {
                // Sanitize any unexpected exception.
                var tag = MapIoTag(ex);
                try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_COPY_ERROR", $"trigger={trigger} tag={tag} ex={ex.GetType().Name}"); } catch { }
                CleanupFailedBackup(backupPath);
                throw new BackupCreateFailedException(tag, "backup_attachments_failed", inner: ex);
            }

            // Apply retention policy for regular backups (PassNotesBackup_*).
            try { PruneRegularBackupsIfNeeded(); } catch { }

            return backupPath;
        });
    }

    public static void PruneRegularBackupsIfNeeded()
    {
        var keep = App.Settings.KeepLastBackups;
        if (keep <= 0)
            return;

        var folder = BackupsFolderPath;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        // Only regular backups created by M4.
        var files = Directory.GetFiles(folder, "PassNotesBackup_*")
            .Select(p => new FileInfo(p))
            .Where(fi => fi.Exists)
            .ToList();

        if (files.Count <= keep)
            return;

        // Order newest -> oldest. Prefer timestamp embedded in the filename.
        var ordered = files
            .OrderByDescending(fi => GetBackupTimestamp(fi))
            .ThenByDescending(fi => fi.LastWriteTimeUtc)
            .ThenByDescending(fi => fi.CreationTimeUtc)
            .ThenByDescending(fi => fi.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var toDelete = ordered.Skip(keep).ToList();
        foreach (var fi in toDelete)
        {
            try { fi.Delete(); } catch { }
            // Also remove sidecar attachments folder if present.
            try { TryDeleteDirectory(fi.FullName + ".attachments"); } catch { }
        }
    }

    private static DateTime GetBackupTimestamp(FileInfo fi)
    {
        try
        {
            var name = fi.Name;
            const string prefix = "PassNotesBackup_";
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return fi.LastWriteTimeUtc;

            // Expect: PassNotesBackup_yyyy-MM-dd_HH-mm-ss[...].ext
            var after = name.Substring(prefix.Length);
            if (after.Length < 19)
                return fi.LastWriteTimeUtc;

            var stamp = after.Substring(0, 19);
            if (DateTime.TryParseExact(stamp, "yyyy-MM-dd_HH-mm-ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
            {
                // The stamp is local time in name; compare using local DateTime.
                return dt;
            }
        }
        catch { }

        return fi.LastWriteTimeUtc;
    }

    public static void OpenBackupsFolder()
    {
        Directory.CreateDirectory(BackupsFolderPath);

        // On Windows this opens the folder in Explorer.
        var psi = new ProcessStartInfo
        {
            FileName = BackupsFolderPath,
            UseShellExecute = true
        };
        Process.Start(psi);
    }


    public static string? CreateBeforeRestoreBackup()
    {
        return VaultIoGate.Run("BackupService.CreateBeforeRestoreBackup", () =>
        {
            Directory.CreateDirectory(BackupsFolderPath);

            if (!File.Exists(VaultFilePath))
                return null;

            var ext = Path.GetExtension(VaultFilePath);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var baseName = $"BeforeRestore_{stamp}";

            var beforePath = Path.Combine(BackupsFolderPath, baseName + ext);
            var i = 1;
            while (File.Exists(beforePath))
            {
                beforePath = Path.Combine(BackupsFolderPath, $"{baseName}_{i}{ext}");
                i++;
            }

            File.Copy(VaultFilePath, beforePath, overwrite: false);

            // Sidecar attachments for safety backup (best-effort).
            try
            {
                var dstAtt = beforePath + ".attachments";
                CopyAttachmentsSidecarStrict(sourceVaultPath: VaultFilePath, dstAttDir: dstAtt, trigger: "BeforeRestore");
            }
            catch (Exception ex)
            {
                var tag = MapIoTag(ex);
                try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_COPY_ERROR", $"trigger=BeforeRestore tag={tag} ex={ex.GetType().Name}"); } catch { }
                // Keep the safety backup vault file even if attachments copy failed.
                // Restore will still be protected by pre-validation.
                try { TryDeleteDirectory(beforePath + ".attachments"); } catch { }
            }
            return beforePath;
        });
    }

    public static string? CreateBeforeVaultSwitchBackup(string currentVaultFilePath)
    {
        return VaultIoGate.Run("BackupService.CreateBeforeVaultSwitchBackup", () =>
        {
            Directory.CreateDirectory(BackupsFolderPath);

            if (string.IsNullOrWhiteSpace(currentVaultFilePath) || !File.Exists(currentVaultFilePath))
                return null;

            var ext = Path.GetExtension(currentVaultFilePath);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var baseName = $"BeforeVaultSwitch_{stamp}";

            var beforePath = Path.Combine(BackupsFolderPath, baseName + ext);
            var i = 1;
            while (File.Exists(beforePath))
            {
                beforePath = Path.Combine(BackupsFolderPath, $"{baseName}_{i}{ext}");
                i++;
            }

            File.Copy(currentVaultFilePath, beforePath, overwrite: false);

            // Sidecar attachments for safety backup (best-effort).
            try
            {
                var dstAtt = beforePath + ".attachments";
                CopyAttachmentsSidecarStrict(sourceVaultPath: currentVaultFilePath, dstAttDir: dstAtt, trigger: "BeforeVaultSwitch");
            }
            catch (Exception ex)
            {
                var tag = MapIoTag(ex);
                try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_COPY_ERROR", $"trigger=BeforeVaultSwitch tag={tag} ex={ex.GetType().Name}"); } catch { }
                try { TryDeleteDirectory(beforePath + ".attachments"); } catch { }
            }
            return beforePath;
        });
    }

    public static void RestoreFromBackup(string backupFilePath)
    {
        VaultIoGate.Run("BackupService.RestoreFromBackup", () =>
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("Backup file path is empty", nameof(backupFilePath));

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found", backupFilePath);

            var vaultDir = Path.GetDirectoryName(VaultFilePath) ?? SettingsStore.GetAppDir();
            Directory.CreateDirectory(vaultDir);

            // Copy to a temp file in the same directory to make replacement safer.
            var tempPath = Path.Combine(vaultDir, $".restore_{Guid.NewGuid():N}.tmp");
            File.Copy(backupFilePath, tempPath, overwrite: true);

            try
            {
                if (File.Exists(VaultFilePath))
                {
                    // Try atomic replace first.
                    try
                    {
                        File.Replace(tempPath, VaultFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                        // File.Replace removes tempPath.
                        tempPath = "";
                    }
                    catch
                    {
                        // Fallback to overwrite copy.
                        File.Copy(tempPath, VaultFilePath, overwrite: true);
                        try { File.Delete(tempPath); } catch { }
                        tempPath = "";
                    }
                }
                else
                {
                    File.Move(tempPath, VaultFilePath);
                    tempPath = "";
                }

                // Restore attachments sidecar to the current vault attachments folder.
                // If the backup has no sidecar (older backup), we delete current attachments
                // to avoid mixing data between different vault states.
                try
                {
                    var srcAtt = backupFilePath + ".attachments";
                    var dstAtt = AttachmentsStore.GetAttachmentsDir(VaultFilePath);
                    AttachmentsStore.ReplaceDirectorySafely(srcAtt, dstAtt);
                }
                catch { /* best-effort */ }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }

        });
    }

    /// <summary>
    /// Restores the vault file and its attachments sidecar in a transaction-like way.
    /// If any step fails, it attempts to roll back to the previous state.
    /// </summary>
    public static void RestoreFromBackupTransactional(string backupFilePath)
    {
        VaultIoGate.Run("BackupService.RestoreFromBackupTransactional", () =>
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("Backup file path is empty", nameof(backupFilePath));

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found", backupFilePath);

            var destVaultPath = VaultFilePath;
            var vaultDir = Path.GetDirectoryName(destVaultPath) ?? SettingsStore.GetAppDir();
            Directory.CreateDirectory(vaultDir);

            var destAttDir = AttachmentsStore.GetAttachmentsDir(destVaultPath);
            var srcAttDir = backupFilePath + ".attachments";
            var hasSrcAtt = Directory.Exists(srcAttDir);

            var opId = Guid.NewGuid().ToString("N");
            var tempNewVault = Path.Combine(vaultDir, $".restore_new_{opId}.tmp");
            string? tempNewAtt = null;
            string? prevVault = null;
            string? prevAtt = null;
            var destVaultExisted = File.Exists(destVaultPath);

            try
            {
                // Stage new vault file.
                File.Copy(backupFilePath, tempNewVault, overwrite: true);

                // Stage new attachments (if present) into a temp folder next to destination.
                if (hasSrcAtt)
                {
                    tempNewAtt = destAttDir + ".tmp_" + opId;
                    CopyDirectoryRecursive(srcAttDir, tempNewAtt);
                }

                // Commit vault (file). Keep a local rollback copy.
                if (destVaultExisted)
                {
                    prevVault = Path.Combine(vaultDir, $".restore_prev_{opId}.bak");
                    try
                    {
                        File.Replace(tempNewVault, destVaultPath, destinationBackupFileName: prevVault, ignoreMetadataErrors: true);
                        tempNewVault = ""; // File.Replace removes temp file.
                    }
                    catch
                    {
                        // Fallback: copy current aside, then overwrite.
                        var movedPrev = Path.Combine(vaultDir, $".restore_prev_{opId}.moved");
                        File.Copy(destVaultPath, movedPrev, overwrite: true);
                        prevVault = movedPrev;
                        File.Copy(tempNewVault, destVaultPath, overwrite: true);
                        try { File.Delete(tempNewVault); } catch { }
                        tempNewVault = "";
                    }
                }
                else
                {
                    File.Move(tempNewVault, destVaultPath);
                    tempNewVault = "";
                }

                // Commit attachments (directory) with rollback.
                if (Directory.Exists(destAttDir))
                {
                    prevAtt = destAttDir + ".prev_restore_" + opId;
                    try
                    {
                        Directory.Move(destAttDir, prevAtt);
                    }
                    catch
                    {
                        // Fallback: copy then delete (best-effort).
                        CopyDirectoryRecursive(destAttDir, prevAtt);
                        try { Directory.Delete(destAttDir, recursive: true); } catch { }
                    }
                }

                if (hasSrcAtt)
                {
                    // Prefer move for atomic rename.
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(tempNewAtt))
                        {
                            Directory.Move(tempNewAtt, destAttDir);
                            tempNewAtt = null;
                        }
                    }
                    catch
                    {
                        // Fallback: copy over.
                        AttachmentsStore.ReplaceDirectorySafely(srcAttDir, destAttDir);
                        if (!string.IsNullOrWhiteSpace(tempNewAtt) && Directory.Exists(tempNewAtt))
                        {
                            try { Directory.Delete(tempNewAtt, recursive: true); } catch { }
                            tempNewAtt = null;
                        }
                    }
                }
                else
                {
                    // No attachments in backup: ensure destination is removed to avoid mixing.
                    try
                    {
                        if (Directory.Exists(destAttDir))
                            Directory.Delete(destAttDir, recursive: true);
                    }
                    catch { }
                }

                // Success: clean rollback artifacts.
                if (!string.IsNullOrWhiteSpace(prevVault) && File.Exists(prevVault))
                {
                    try { File.Delete(prevVault); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(prevAtt) && Directory.Exists(prevAtt))
                {
                    try { Directory.Delete(prevAtt, recursive: true); } catch { }
                }
            }
            catch
            {
                // Best-effort rollback.
                try
                {
                    if (!string.IsNullOrWhiteSpace(prevAtt) && Directory.Exists(prevAtt))
                    {
                        try { if (Directory.Exists(destAttDir)) Directory.Delete(destAttDir, recursive: true); } catch { }
                        try { Directory.Move(prevAtt, destAttDir); } catch { }
                    }
                }
                catch { }

                try
                {
                    if (!string.IsNullOrWhiteSpace(prevVault) && File.Exists(prevVault))
                    {
                        try
                        {
                            // Try atomic replace back.
                            File.Replace(prevVault, destVaultPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                        }
                        catch
                        {
                            try { File.Copy(prevVault, destVaultPath, overwrite: true); } catch { }
                            try { File.Delete(prevVault); } catch { }
                        }
                    }
                    else if (!destVaultExisted)
                    {
                        // Destination didn't exist before; best-effort delete.
                        try { if (File.Exists(destVaultPath)) File.Delete(destVaultPath); } catch { }
                    }
                }
                catch { }

                throw;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempNewVault) && File.Exists(tempNewVault))
                {
                    try { File.Delete(tempNewVault); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(tempNewAtt) && Directory.Exists(tempNewAtt))
                {
                    try { Directory.Delete(tempNewAtt, recursive: true); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(prevVault) && File.Exists(prevVault))
                {
                    try { File.Delete(prevVault); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(prevAtt) && Directory.Exists(prevAtt))
                {
                    try { Directory.Delete(prevAtt, recursive: true); } catch { }
                }
            }
        });
    }

    private static void TryDeleteDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return;
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, name);
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(sub);
            CopyDirectoryRecursive(sub, Path.Combine(destDir, name));
        }
    }

    private static void CleanupFailedBackup(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            return;

        try { TryDeleteDirectory(backupPath + ".attachments"); } catch { }
        try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
    }

    private static void CopyAttachmentsSidecarStrict(string sourceVaultPath, string dstAttDir, string trigger)
    {
        var srcAtt = AttachmentsStore.GetAttachmentsDir(sourceVaultPath);
        if (!Directory.Exists(srcAtt))
        {
            // No attachments folder: ensure no stale sidecar folder remains.
            TryDeleteDirectory(dstAttDir);
            return;
        }

        // Ensure destination is clean.
        TryDeleteDirectory(dstAttDir);

        var srcFiles = GetAllFilesRelative(srcAtt);
        try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_COPY_BEGIN", $"trigger={trigger} files={srcFiles.Count}"); } catch { }

        try
        {
            CopyDirectoryRecursive(srcAtt, dstAttDir);

            // Verify that all source files are present in destination (avoid silent partial backups).
            foreach (var rel in srcFiles)
            {
                var dstFile = Path.Combine(dstAttDir, rel);
                if (!File.Exists(dstFile))
                    throw new FileNotFoundException("attachments_copy_incomplete", rel);
            }

            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_COPY_END", $"trigger={trigger} files={srcFiles.Count}"); } catch { }
        }
        catch (Exception ex)
        {
            var tag = MapIoTag(ex);
            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_COPY_ERROR", $"trigger={trigger} tag={tag} ex={ex.GetType().Name}"); } catch { }
            throw new BackupCreateFailedException(tag, "backup_attachments_copy_failed", inner: ex);
        }
    }

    private static void ValidateBackupAttachmentsStrict(string backupPath, string masterPassword, string trigger)
    {
        try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_BEGIN", $"trigger={trigger} backup={Path.GetFileName(backupPath)}"); } catch { }

        try
        {
            var previewStore = new VaultStore(backupPath);
            var preview = previewStore.Load(masterPassword);

	            if (preview.Attachments is { Length: > 0 })
	            {
	                // Strictly validate only attachments that belong to existing entries in the backup.
	                // Dangling attachment metadata (EntryId not found in Entries) must not block backup creation.
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
	                if (required.Count > 0)
	                {
	                    if (!Directory.Exists(attDir))
	                    {
	                        try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_END", $"trigger={trigger} required={required.Count} missing_required={required.Count} skipped_dangling={skippedDangling}"); } catch { }
	                        try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_FAIL", $"trigger={trigger} missing_required={required.Count}"); } catch { }
	                        throw new BackupCreateFailedException("missing_attachments_folder", "backup_missing_attachments_folder");
	                    }

	                    foreach (var a in required)
	                    {
	                        var blobPath = Path.Combine(attDir, $"{a.Id:N}.pna");
	                        if (!File.Exists(blobPath))
	                        {
	                            var name = string.IsNullOrWhiteSpace(a.FileName) ? $"{a.Id:N}.pna" : a.FileName;
	                            name = SanitizeDisplayName(name);
	                            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_END", $"trigger={trigger} required={required.Count} missing_required=1 skipped_dangling={skippedDangling}"); } catch { }
	                            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_FAIL", $"trigger={trigger} missing_required=1"); } catch { }
	                            throw new BackupCreateFailedException("missing_attachment_blob", "backup_missing_attachment_blob", attachmentDisplayName: name);
	                        }
	                    }
	                }

	                try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_END", $"trigger={trigger} required={required.Count} missing_required=0 skipped_dangling={skippedDangling}"); } catch { }
	            }
	            else
	            {
	                try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_END", $"trigger={trigger} required=0 missing_required=0 skipped_dangling=0"); } catch { }
	            }
        }
        catch (BackupCreateFailedException)
        {
            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_ERROR", $"trigger={trigger}"); } catch { }
            throw;
        }
        catch (CryptographicException ex)
        {
            var tag = "crypto_error";
            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_ERROR", $"trigger={trigger} tag={tag} ex={ex.GetType().Name}"); } catch { }
            throw new BackupCreateFailedException(tag, "backup_verify_crypto_error", inner: ex);
        }
        catch (Exception ex)
        {
            var tag = "unexpected";
            try { DiagnosticsLog.AppendLine("BACKUP_ATTACHMENTS_VERIFY_ERROR", $"trigger={trigger} tag={tag} ex={ex.GetType().Name}"); } catch { }
            throw new BackupCreateFailedException(tag, "backup_verify_unexpected", inner: ex);
        }
    }

    private static List<string> GetAllFilesRelative(string root)
    {
        var res = new List<string>();
        try
        {
            foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var rel = Path.GetRelativePath(root, f);
                    if (!string.IsNullOrWhiteSpace(rel))
                        res.Add(rel);
                }
                catch { }
            }
        }
        catch { }
        return res;
    }

    private static string SanitizeDisplayName(string value)
    {
        value = (value ?? "").Trim();
        value = value.Replace("\r", " ").Replace("\n", " ");
        if (value.Length > 80)
            value = value.Substring(0, 80);
        return value;
    }

    private static string MapIoTag(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
            return "io_access_denied";

        if (ex is DirectoryNotFoundException)
            return "dir_missing";

        if (ex is FileNotFoundException)
            return "file_not_found";

        if (ex is CryptographicException)
            return "crypto_error";

        if (ex is IOException)
        {
            try
            {
                var hr = Marshal.GetHRForException(ex);
                if (hr == unchecked((int)0x80070020) || hr == unchecked((int)0x80070021))
                    return "io_in_use";
            }
            catch { }
            return "io_error";
        }

        return "unexpected";
    }

}
