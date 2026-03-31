using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PassNotes;

/// <summary>
/// Stores encrypted attachment blobs next to the vault file, in a vault-specific folder.
/// Layout:
///   <vaultFilePath>.attachments\{attachmentId:N}.pna
/// </summary>
public static class AttachmentsStore
{
    public static string GetAttachmentsDir(string vaultFilePath)
    {
        vaultFilePath = (vaultFilePath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(vaultFilePath))
            vaultFilePath = BackupService.VaultFilePath;
        return vaultFilePath + ".attachments";
    }

    public static string GetAttachmentBlobPath(string vaultFilePath, Guid attachmentId)
    {
        var dir = GetAttachmentsDir(vaultFilePath);
        return Path.Combine(dir, $"{attachmentId:N}.pna");
    }

    public static void EnsureAttachmentsDir(string vaultFilePath)
    {
        var dir = GetAttachmentsDir(vaultFilePath);
        Directory.CreateDirectory(dir);
    }

    public static void DeleteAttachmentsDir(string vaultFilePath)
    {
        var dir = GetAttachmentsDir(vaultFilePath);
        if (!Directory.Exists(dir))
            return;
        try { Directory.Delete(dir, recursive: true); } catch { }
    }
    /// <summary>
    /// Removes:
    /// - metadata entries without a corresponding blob file
    /// - blob files that are not referenced by metadata
    /// Returns true if metadata was changed.
    /// </summary>
    public static bool CleanupOrphans(VaultData data, string vaultFilePath)
    {
        data.Attachments ??= Array.Empty<VaultAttachment>();
        var list = data.Attachments.ToList();

        var dir = GetAttachmentsDir(vaultFilePath);
        var blobNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(dir))
        {
            try
            {
                foreach (var fp in Directory.GetFiles(dir, "*.pna", SearchOption.TopDirectoryOnly))
                    blobNames.Add(Path.GetFileName(fp));
            }
            catch (Exception ex)
            {
                DiagnosticsLog.AppendLine("ATT_ORPHAN_ENUM_ERROR", $"phase=scan ex={ex.GetType().Name}");
            }
        }

        var before = list.Count;
        list.RemoveAll(a => !blobNames.Contains($"{a.Id:N}.pna"));
        var metaChanged = list.Count != before;

        var expected = new HashSet<string>(list.Select(a => $"{a.Id:N}.pna"), StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(dir))
        {
            try
            {
                foreach (var fp in Directory.GetFiles(dir, "*.pna", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(fp);
                    if (!expected.Contains(name))
                    {
                        try { File.Delete(fp); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.AppendLine("ATT_ORPHAN_ENUM_ERROR", $"phase=purge ex={ex.GetType().Name}");
            }
        }

        if (metaChanged)
            data.Attachments = list.ToArray();

        return metaChanged;
    }

    internal static void ReplaceDirectorySafely(string sourceDir, string destDir)
    {
        // If there is no source (older backup/export), delete destination to avoid mixing content.
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            try
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, recursive: true);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.AppendLine("ATT_REPLACE_DEST_DELETE_ERROR", $"ex={ex.GetType().Name}");
            }
            return;
        }

        var parent = Path.GetDirectoryName(destDir);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);

        // Copy to a temp directory in the same parent, then swap.
        var tempDir = destDir + ".tmp_" + Guid.NewGuid().ToString("N");
        try
        {
            CopyDirectoryRecursive(sourceDir, tempDir);

            // Delete destination and move temp in its place.
            try
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, recursive: true);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.AppendLine("ATT_REPLACE_DEST_DELETE_ERROR", $"ex={ex.GetType().Name}");
            }

            try
            {
                Directory.Move(tempDir, destDir);
                tempDir = "";
            }
            catch (Exception ex)
            {
                DiagnosticsLog.AppendLine("ATT_REPLACE_MOVE_FALLBACK", $"ex={ex.GetType().Name}");
                // Fallback: copy over.
                CopyDirectoryRecursive(sourceDir, destDir);
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
                tempDir = "";
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempDir) && Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
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
}
