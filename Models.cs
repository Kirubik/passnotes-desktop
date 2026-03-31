using System;
using System.Text.Json.Serialization;

namespace PassNotes;

public sealed class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Url { get; set; } = "";
    public string Comment { get; set; } = "";
    public bool IsFavorite { get; set; } = false;
    /// <summary>
    /// Soft-delete flag: entries in trash are not shown in normal views.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// When the entry was moved to trash (UTC).
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; } = null;

    /// <summary>
    /// Original folder where the entry was located before moving to trash (used for restore).
    /// </summary>
    public Guid? DeletedFromFolderId { get; set; } = null;

    public Guid? FolderId { get; set; } = null;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    // UI-only (not persisted): helps show folder info in aggregated views (Favorites / global search).
    [JsonIgnore]
    public string UiFolderPath { get; set; } = "";
}

public sealed class VaultFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; } = null;
}

public sealed class VaultAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public string FileName { get; set; } = "";
    public long Size { get; set; } = 0;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class VaultData
{
    public int Version { get; set; } = 4;
    public VaultEntry[] Entries { get; set; } = Array.Empty<VaultEntry>();
    public VaultFolder[] Folders { get; set; } = Array.Empty<VaultFolder>();
    public VaultAttachment[] Attachments { get; set; } = Array.Empty<VaultAttachment>();
}

/// <summary>
    /// Used for folder selection in the entry editor.
/// </summary>
public sealed class FolderOption
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
}
