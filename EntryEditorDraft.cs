using System;

namespace PassNotes;

public sealed class EntryEditorDraft
{
    public bool IsNew { get; init; }
    public Guid EntryId { get; init; }
    public Guid? FolderId { get; init; }
    public string Title { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Url { get; init; } = "";
    public string Comment { get; init; } = "";

    public sealed class PendingAttachmentAddDraft
    {
        public Guid DraftId { get; init; }
        public string EncryptedPath { get; init; } = "";
        public string FileName { get; init; } = "";
        public long SizeBytes { get; init; }
        public string? OriginalPath { get; init; }
    }

    public PendingAttachmentAddDraft[] PendingAttachmentAdds { get; init; } = Array.Empty<PendingAttachmentAddDraft>();
    public Guid[] PendingAttachmentDeleteIds { get; init; } = Array.Empty<Guid>();
}
