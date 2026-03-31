using System.Threading;

namespace PassNotes;

/// <summary>
/// Logs attachment reference index once per process run.
/// This helps validate MVP-3B2 indexing without changing UI.
/// </summary>
internal static class AttachmentsOrphanIndexDiagnostics
{
    private static int _didLog;

    public static void LogOnce(VaultData? vault)
    {
        if (vault == null)
            return;

        if (Interlocked.Exchange(ref _didLog, 1) != 0)
            return;

        try
        {
            var idx = AttachmentReferenceIndex.Build(vault);
            DiagnosticsLog.AppendLine("ATT_REF_INDEX", idx.ToDiagnosticString());
        }
        catch
        {
            // best-effort
        }
    }
}
