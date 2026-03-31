using System;
using System.Collections.Generic;
using System.IO;

namespace PassNotes;

/// <summary>
/// Best-effort orphan cleanup for attachment blobs.
///
/// MVP-3B2 (subblock 1.2): implements scan + quarantine + purge.
/// No UI/toasts here; integration/rate-limit is handled in a later subblock.
///
/// Safety:
/// - does not show UI
/// - never deletes from the live attachments folder; moves to a quarantine folder
/// - uses a grace period to avoid touching fresh/in-flight files
///
/// NOTE (MVP-3B2 / Block 2 / Subblock 2.1):
/// - Exceptions are allowed to bubble up (wrapped with a phase) so orchestration
///   can log a single unambiguous ATT_ORPHAN_CLEANUP_ERROR line.
/// </summary>
internal static class AttachmentsOrphanCleanupService
{
    internal sealed class OrphanCleanupPhaseException : Exception
    {
        public string Phase { get; }

        public OrphanCleanupPhaseException(string phase, Exception inner)
            : base("Attachments orphan cleanup failed", inner)
        {
            Phase = string.IsNullOrWhiteSpace(phase) ? "unknown" : phase;
        }
    }

    internal sealed class Result
    {
        public const int MaxSampleIds = 50;

        // --- Quarantine actions
        public int MovedDanglingMetaBlobs { get; set; }
        public int MovedUnreferencedBlobs { get; set; }
        public int PurgedFromQuarantine { get; set; }

        // --- Edge-case diagnostics (MVP-3B2 / Block 2 / Subblock 2.2)
        public int ReferencedIds { get; set; }
        public int MetaFiles { get; set; }
        public int BlobFiles { get; set; }

        // "Dangling refs" in our model:
        // - meta row pointing to missing entry => dangling_ref_meta_missing
        // - meta row (for existing entry) pointing to missing blob => dangling_ref_blob_missing
        public int DanglingRefMetaMissing { get; set; }
        public int DanglingRefBlobMissing { get; set; }

        // "meta without blob" candidates (for entries that exist)
        public int OrphansMetaNoBlobCandidates { get; set; }
        // "blob without meta" candidates
        public int OrphansBlobNoMetaCandidates { get; set; }

        // Quarantine / grace breakdown for edge-cases
        public int QuarantinedMetaOnly { get; set; }
        public int QuarantinedBlobOnly { get; set; }
        public int SkippedGraceMetaOnly { get; set; }
        public int SkippedGraceBlobOnly { get; set; }

        // Protection against false moves/purge
        public int SkippedMoveBecauseParsedAsMeta { get; set; }
        public int PurgeSkippedReferenced { get; set; }

        // --- Samples (IDs only, no paths). Used for best-effort report.
        public List<Guid> SampleDanglingMetaAttachmentIds { get; } = new();
        public List<Guid> SampleMissingBlobAttachmentIds { get; } = new();
        public List<Guid> SampleOrphansBlobNoMetaIds { get; } = new();
        public List<Guid> SampleOrphansMetaNoBlobIds { get; } = new();
        public List<Guid> SampleQuarantinedBlobOnlyIds { get; } = new();
        public List<Guid> SampleQuarantinedMetaOnlyIds { get; } = new();

        // --- Metadata fixes
        public int RemovedDanglingMetadata { get; set; }
        public int RemovedMissingBlobMetadata { get; set; }

        // --- Diagnostics (helps explain "0 actions")
        public int DanglingMetaCandidates { get; set; }
        public int DanglingMetaMissingBlobs { get; set; }
        public int DanglingMetaSkippedGrace { get; set; }

        public int UnreferencedScanCount { get; set; }
        public int UnreferencedExpectedCount { get; set; }
        public int UnreferencedCandidates { get; set; }
        public int UnreferencedSkippedGrace { get; set; }

        public int QuarantineScanned { get; set; }

        public bool HasAnyWork =>
            MovedDanglingMetaBlobs > 0 ||
            MovedUnreferencedBlobs > 0 ||
            PurgedFromQuarantine > 0 ||
            RemovedDanglingMetadata > 0 ||
            RemovedMissingBlobMetadata > 0;

        public string ToDiagnosticString()
        {
            // Keep the original fields first (for backward readability), then append counters.
            return $"movedDanglingMetaBlobs={MovedDanglingMetaBlobs}; movedUnreferencedBlobs={MovedUnreferencedBlobs}; purged={PurgedFromQuarantine}; removedDanglingMeta={RemovedDanglingMetadata}; removedMissingBlobMeta={RemovedMissingBlobMetadata}"
                 + $"; danglingMetaCandidates={DanglingMetaCandidates}; danglingMetaMissingBlobs={DanglingMetaMissingBlobs}; danglingMetaSkippedGrace={DanglingMetaSkippedGrace}"
                 + $"; unrefScan={UnreferencedScanCount}; unrefExpected={UnreferencedExpectedCount}; unrefCandidates={UnreferencedCandidates}; unrefSkippedGrace={UnreferencedSkippedGrace}"
                 + $"; quarantineScanned={QuarantineScanned}"
                 + $"; referenced_ids={ReferencedIds}; meta_files={MetaFiles}; blob_files={BlobFiles}"
                 + $"; dangling_ref_meta_missing={DanglingRefMetaMissing}; dangling_ref_blob_missing={DanglingRefBlobMissing}"
                 + $"; orphans_blob_no_meta_candidates={OrphansBlobNoMetaCandidates}; orphans_meta_no_blob_candidates={OrphansMetaNoBlobCandidates}"
                 + $"; quarantined_meta_only={QuarantinedMetaOnly}; quarantined_blob_only={QuarantinedBlobOnly}"
                 + $"; skipped_grace_meta_only={SkippedGraceMetaOnly}; skipped_grace_blob_only={SkippedGraceBlobOnly}"
                 + $"; skipped_move_parsed_as_meta={SkippedMoveBecauseParsedAsMeta}; purge_skipped_referenced={PurgeSkippedReferenced}";
        }
    }

    /// <summary>
    /// Runs orphan cleanup. Returns true if vault metadata was changed.
    /// </summary>
    public static bool RunBestEffort(VaultData? vault, string vaultFilePath, out Result result)
    {
        var res = new Result();

        // Assign early so caller gets a value in normal flow; use local "res" in lambdas (C# disallows capturing out params).
        result = res;

        if (vault == null)
            return false;

        var attsDir = AttachmentsStore.GetAttachmentsDir(vaultFilePath);
        if (string.IsNullOrWhiteSpace(attsDir))
            return false;

        // Grace: do not touch anything fresher than this.
        var grace = TimeSpan.FromHours(24);
        // Retention: how long items stay in quarantine before being deleted.
        var retention = TimeSpan.FromDays(7);

        // Index current vault references.
        AttachmentReferenceIndex idx = null!;
        var entryIds = new HashSet<Guid>();
        RunPhase("index", () =>
        {
            idx = AttachmentReferenceIndex.Build(vault);

            var entries = vault.Entries ?? Array.Empty<VaultEntry>();
            foreach (var e in entries)
            {
                if (e == null)
                    continue;
                if (e.Id == Guid.Empty)
                    continue;
                entryIds.Add(e.Id);
            }

            res.MetaFiles = vault.Attachments?.Length ?? 0;
            res.ReferencedIds = idx.MetaAttachmentIds?.Count ?? 0;
            res.DanglingRefMetaMissing = idx.DanglingMetaAttachmentIds?.Count ?? 0;

            // Sample dangling meta IDs (entry missing)
            foreach (var id in idx.DanglingMetaAttachmentIds ?? new HashSet<Guid>())
                AddSampleId(res.SampleDanglingMetaAttachmentIds, id);
        });

        // Scan current live blob files (top-level only).
        var liveBlobFiles = new List<string>();
        RunPhase("scan", () =>
        {
            if (!Directory.Exists(attsDir))
                return;

            foreach (var fp in Directory.GetFiles(attsDir, "*.pna", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(fp);
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    continue;

                liveBlobFiles.Add(fp);
            }

            res.BlobFiles = liveBlobFiles.Count;
        });

        // Classify "meta without blob" (for existing entries) and "dangling meta" missing blob.
        RunPhase("classify", () =>
        {
            vault.Attachments ??= Array.Empty<VaultAttachment>();
            foreach (var a in vault.Attachments)
            {
                if (a == null)
                    continue;
                if (a.Id == Guid.Empty)
                    continue;

                var isDanglingEntry = a.EntryId == Guid.Empty || !entryIds.Contains(a.EntryId);
                var blobPath = AttachmentsStore.GetAttachmentBlobPath(vaultFilePath, a.Id);
                var blobExists = !string.IsNullOrWhiteSpace(blobPath) && File.Exists(blobPath);

                if (!blobExists)
                {
                    res.DanglingRefBlobMissing++;
                    AddSampleId(res.SampleMissingBlobAttachmentIds, a.Id);
                    if (!isDanglingEntry)
                    {
                        res.OrphansMetaNoBlobCandidates++;
                        AddSampleId(res.SampleOrphansMetaNoBlobIds, a.Id);
                    }
                }
            }
        });

        // 1) Quarantine blobs for dangling metadata (EntryId missing).
        RunPhase("scan", () =>
        {
            foreach (var id in idx.DanglingMetaAttachmentIds)
            {
                if (id == Guid.Empty)
                    continue;

                res.DanglingMetaCandidates++;

                var fp = AttachmentsStore.GetAttachmentBlobPath(vaultFilePath, id);
                if (string.IsNullOrWhiteSpace(fp) || !File.Exists(fp))
                {
                    res.DanglingMetaMissingBlobs++;
                    continue;
                }

                if (!IsOlderThanGrace(fp, grace))
                {
                    res.DanglingMetaSkippedGrace++;
                    continue;
                }

                RunPhase("move", () =>
                {
                    if (MoveToQuarantine(attsDir, fp))
                        res.MovedDanglingMetaBlobs++;
                });
            }
        });

        // 2) Quarantine blobs that are not referenced by metadata.
        //    Extra safety: if the filename can be parsed as a GUID and it exists in metadata,
        //    treat it as referenced and never move it (protects against weird naming / old/manual states).
        RunPhase("scan", () =>
        {
            if (liveBlobFiles.Count == 0)
                return;

            foreach (var fp in liveBlobFiles)
            {
                var name = Path.GetFileName(fp);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // Skip hidden/marker files if any.
                if (name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    continue;

                res.UnreferencedScanCount++;

                if (idx.ExpectedBlobFileNames.Contains(name))
                {
                    res.UnreferencedExpectedCount++;
                    continue;
                }

                // If we can parse an attachmentId from the filename and it's present in metadata,
                // it is not an orphan (even if the name is not exactly "{id}.pna").
                if (TryParseAttachmentIdFromFileName(name, out var parsed) && idx.MetaAttachmentIds.Contains(parsed))
                {
                    res.SkippedMoveBecauseParsedAsMeta++;
                    continue;
                }

                res.UnreferencedCandidates++;
                res.OrphansBlobNoMetaCandidates++;

                if (TryParseAttachmentIdFromFileName(name, out var orphanId))
                    AddSampleId(res.SampleOrphansBlobNoMetaIds, orphanId);

                if (!IsOlderThanGrace(fp, grace))
                {
                    res.UnreferencedSkippedGrace++;
                    res.SkippedGraceBlobOnly++;
                    continue;
                }

                RunPhase("move", () =>
                {
                    if (MoveToQuarantine(attsDir, fp))
                    {
                        res.MovedUnreferencedBlobs++;
                        res.QuarantinedBlobOnly++;

                        if (TryParseAttachmentIdFromFileName(name, out var movedId))
                            AddSampleId(res.SampleQuarantinedBlobOnlyIds, movedId);
                    }
                });
            }
        });

        // 3) Purge old quarantine items (delete from quarantine only).
        //    Safety: never purge a quarantined blob if its attachmentId is referenced by metadata
        //    AND the owning entry still exists (protects against older false moves).
        RunPhase("purge", () =>
        {
            res.PurgedFromQuarantine = PurgeQuarantine(attsDir, retention, idx, out var scanned, out var skippedReferenced);
            res.QuarantineScanned = scanned;
            res.PurgeSkippedReferenced = skippedReferenced;
        });

        // NOTE: Do not log here. Integration layer (MainWindow) is responsible for BEGIN/SKIP/END/ERROR logging.

        // In Subblock 2.2 we intentionally avoid mutating vault metadata automatically.
        // Cleanup is best-effort and file-based (quarantine/purge) only.
        return false;
    }

    private static void RunPhase(string phase, Action action)
    {
        try
        {
            action();
        }
        catch (OrphanCleanupPhaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OrphanCleanupPhaseException(phase, ex);
        }
    }

    private static void AddSampleId(List<Guid> list, Guid id)
    {
        if (id == Guid.Empty)
            return;
        if (list.Count >= Result.MaxSampleIds)
            return;
        if (list.Contains(id))
            return;
        list.Add(id);
    }

    private static bool IsOlderThanGrace(string filePath, TimeSpan grace)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // Defensive: the file may disappear between enumeration and checks.
        if (!File.Exists(filePath))
            return false;

        var ts = DateTime.UtcNow - File.GetLastWriteTimeUtc(filePath);
        return ts >= grace;
    }

    private static bool MoveToQuarantine(string attachmentsDir, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(attachmentsDir) || string.IsNullOrWhiteSpace(sourceFilePath))
            return false;

        if (!File.Exists(sourceFilePath))
            return false;

        var quarantineRoot = Path.Combine(attachmentsDir, ".orphan_quarantine");
        var monthDir = Path.Combine(quarantineRoot, DateTime.UtcNow.ToString("yyyy-MM"));
        Directory.CreateDirectory(monthDir);

        var name = Path.GetFileName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var dest = GetUniquePath(Path.Combine(monthDir, name));

        try
        {
            File.Move(sourceFilePath, dest);
        }
        catch
        {
            // Fallback: copy + delete.
            File.Copy(sourceFilePath, dest, overwrite: false);
            File.Delete(sourceFilePath);
        }

        // Mark quarantine time so retention is based on when we quarantined.
        File.SetLastWriteTimeUtc(dest, DateTime.UtcNow);
        return true;
    }

    private static int PurgeQuarantine(
        string attachmentsDir,
        TimeSpan retention,
        AttachmentReferenceIndex idx,
        out int scanned,
        out int skippedReferenced)
    {
        var purged = 0;
        scanned = 0;
        skippedReferenced = 0;

        var quarantineRoot = Path.Combine(attachmentsDir, ".orphan_quarantine");
        if (!Directory.Exists(quarantineRoot))
            return 0;

        foreach (var fp in Directory.GetFiles(quarantineRoot, "*.pna", SearchOption.AllDirectories))
        {
            scanned++;

            // If this looks like a referenced attachment, never purge it.
            var name = Path.GetFileName(fp);
            if (!string.IsNullOrWhiteSpace(name)
                && TryParseAttachmentIdFromFileName(name, out var id)
                && idx.MetaAttachmentIds.Contains(id)
                && !idx.DanglingMetaAttachmentIds.Contains(id))
            {
                skippedReferenced++;
                continue;
            }

            var ts = DateTime.UtcNow - File.GetLastWriteTimeUtc(fp);
            if (ts < retention)
                continue;

            File.Delete(fp);
            purged++;
        }

        return purged;
    }

    private static bool TryParseAttachmentIdFromFileName(string fileName, out Guid id)
    {
        id = Guid.Empty;
        try
        {
            fileName = (fileName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var baseName = Path.GetFileNameWithoutExtension(fileName) ?? "";
            if (baseName.Length >= 32)
            {
                // Common patterns:
                // - {id:N}.pna
                // - {id:N}_something.pna (rare, but can appear in manual/legacy states)
                var prefix = baseName.Substring(0, 32);
                if (Guid.TryParseExact(prefix, "N", out id))
                    return true;
            }

            // Fallback: try parse the whole base name.
            if (Guid.TryParse(baseName, out id))
                return true;
        }
        catch { }

        id = Guid.Empty;
        return false;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_orphan{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_orphan_{Guid.NewGuid():N}{ext}");
    }
}
