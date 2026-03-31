namespace PassNotes;

internal static class EntryUrlActions
{
    public static string NormalizeCopyText(string? url)
        => (url ?? string.Empty).Trim();

    public static bool CanCopy(string? url)
        => !string.IsNullOrWhiteSpace(NormalizeCopyText(url));

    public static bool TryCopy(string? url, out string? failureReason)
    {
        var text = NormalizeCopyText(url);
        if (string.IsNullOrWhiteSpace(text))
        {
            failureReason = "empty";
            return false;
        }

        return ClipboardSecurity.TryCopyText(text, out failureReason);
    }

    public static bool CanOpenInBrowser(string? url)
        => ExternalUrlService.CanOpenWebUrl(url);

    public static bool TryOpenInBrowser(string? url, string logTag = "ENTRY_URL_OPEN")
        => ExternalUrlService.TryOpenWebUrl(url, logTag);
}
