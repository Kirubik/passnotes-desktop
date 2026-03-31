using System;

namespace PassNotes;

/// <summary>
/// UI projection for attachments list.
/// Kept as a simple POCO to avoid pulling in MVVM infrastructure.
/// </summary>
internal sealed class AttachmentListItem
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }

    /// <summary>
    /// True when this item represents a pending attachment that is not yet stored in the vault.
    /// In that case, <see cref="DraftEncryptedPath"/> points to an encrypted temp blob
    /// (stored outside the vault attachments directory).
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>
    /// For draft items only: encrypted temp blob path on disk.
    /// </summary>
    public string? DraftEncryptedPath { get; set; }

    /// <summary>
    /// For draft items only: original file path (best-effort, may be null).
    /// Kept only for diagnostics / UX (not required for saving).
    /// </summary>
    public string? OriginalPath { get; set; }

    public string Display
        => string.IsNullOrWhiteSpace(FileName)
            ? Id.ToString("N")
            : $"{FileName}  ({FormatBytes(SizeBytes)})";

    private static string FormatBytes(long bytes)
    {
        try
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:0.#} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:0.#} MB";
            double gb = mb / 1024.0;
            return $"{gb:0.##} GB";
        }
        catch
        {
            return bytes.ToString();
        }
    }
}
