using System;

namespace PassNotes;

/// <summary>
/// Draft item representing an attachment already encrypted into a temp blob.
/// Used to atomically commit attachment changes together with entry save.
/// </summary>
internal sealed class AttachmentDraftAddEncrypted
{
    public string EncryptedPath { get; init; } = "";
    public string FileName { get; init; } = "";
    public long SizeBytes { get; init; } = 0;
}
