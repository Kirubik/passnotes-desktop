using System;
using System.Collections.Generic;
using System.Linq;

namespace PassNotes;

/// <summary>
/// Builds a fast index of which attachment blobs are referenced by the current vault data.
/// Used by orphan-cleanup routines (MVP-3B2).
///
/// IMPORTANT: This class is indexing-only. It does not mutate vault data or files.
/// </summary>
internal sealed class AttachmentReferenceIndex
{
    public int EntriesCount { get; private set; }
    public int AttachmentsMetaCount { get; private set; }

    /// <summary>
    /// All attachment IDs present in metadata (even if the EntryId is missing from Entries).
    /// </summary>
    public HashSet<Guid> MetaAttachmentIds { get; private set; } = new();

    /// <summary>
    /// Attachment IDs whose EntryId does not exist in Entries (dangling metadata).
    /// These are not "blob orphans" per se, but are useful diagnostics for cleanup.
    /// </summary>
    public HashSet<Guid> DanglingMetaAttachmentIds { get; private set; } = new();

    /// <summary>
    /// Expected blob file names derived from metadata IDs, e.g. "{id:N}.pna".
    /// </summary>
    public HashSet<string> ExpectedBlobFileNames { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public static AttachmentReferenceIndex Build(VaultData? data)
    {
        var idx = new AttachmentReferenceIndex();

        if (data == null)
            return idx;

        var entries = data.Entries ?? Array.Empty<VaultEntry>();
        var atts = data.Attachments ?? Array.Empty<VaultAttachment>();

        idx.EntriesCount = entries.Length;
        idx.AttachmentsMetaCount = atts.Length;

        var entryIds = new HashSet<Guid>(entries.Select(e => e.Id).Where(id => id != Guid.Empty));

        foreach (var a in atts)
        {
            if (a == null)
                continue;

            if (a.Id == Guid.Empty)
                continue;

            idx.MetaAttachmentIds.Add(a.Id);
            idx.ExpectedBlobFileNames.Add($"{a.Id:N}.pna");

            // EntryId is non-nullable in the model; treat missing/unknown entry as dangling metadata.
            if (a.EntryId == Guid.Empty || !entryIds.Contains(a.EntryId))
                idx.DanglingMetaAttachmentIds.Add(a.Id);
        }

        return idx;
    }

    public string ToDiagnosticString()
    {
        var uniqueMeta = MetaAttachmentIds?.Count ?? 0;
        var dangling = DanglingMetaAttachmentIds?.Count ?? 0;
        var expected = ExpectedBlobFileNames?.Count ?? 0;
        return $"entries={EntriesCount}; metas={AttachmentsMetaCount}; uniqueMetaIds={uniqueMeta}; expectedBlobs={expected}; danglingMetaIds={dangling}";
    }
}
