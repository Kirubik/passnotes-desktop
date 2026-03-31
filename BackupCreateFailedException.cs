using System;

namespace PassNotes;

/// <summary>
/// A controlled failure during backup creation/validation.
/// Used to stop creating a "partial" backup that later can't be restored.
/// </summary>
public sealed class BackupCreateFailedException : Exception
{
    /// <summary>
    /// Short human-friendly tag (io_in_use, io_access_denied, missing_attachment_blob, ...).
    /// Safe to show in UI and write to diagnostic logs.
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// Internal diagnostic code for grep/analytics (e.g. backup_attachments_copy_failed).
    /// </summary>
    public string DiagnosticCode { get; }

    /// <summary>
    /// Optional attachment display name (sanitized, without paths).
    /// </summary>
    public string? AttachmentDisplayName { get; }

    public BackupCreateFailedException(
        string tag,
        string diagnosticCode,
        string? attachmentDisplayName = null,
        Exception? inner = null)
        : base(message: tag ?? "backup_failed", innerException: inner)
    {
        Tag = string.IsNullOrWhiteSpace(tag) ? "backup_failed" : tag.Trim();
        DiagnosticCode = string.IsNullOrWhiteSpace(diagnosticCode) ? "backup_failed" : diagnosticCode.Trim();
        AttachmentDisplayName = string.IsNullOrWhiteSpace(attachmentDisplayName) ? null : attachmentDisplayName.Trim();
    }
}
