using System.Diagnostics;

namespace PassNotes;

internal static class ExternalUrlService
{
    public static bool TryOpen(string? url, string logTag = "EXTERNAL_URL_OPEN")
    {
        if (!TryNormalizeSupportedUrl(url, out var absoluteUrl))
        {
            TryLog(logTag, $"result=invalid url={(url ?? "<null>").Trim()}");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(absoluteUrl) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            TryLog(logTag, $"result=error url={absoluteUrl} ex={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static bool CanOpenWebUrl(string? url)
        => TryNormalizeWebUrl(url, out _);

    public static bool TryOpenWebUrl(string? url, string logTag = "EXTERNAL_WEB_URL_OPEN")
    {
        if (!TryNormalizeWebUrl(url, out var absoluteUrl))
        {
            TryLog(logTag, $"result=invalid url={(url ?? "<null>").Trim()}");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(absoluteUrl) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            TryLog(logTag, $"result=error url={absoluteUrl} ex={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static bool TryNormalizeWebUrl(string? url, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;

        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (TryCreateAbsoluteWebUri(trimmed, out var directUri))
        {
            absoluteUrl = directUri.AbsoluteUri;
            return true;
        }

        if (!LooksLikeSchemeLessWebUrl(trimmed))
            return false;

        if (!TryCreateAbsoluteWebUri($"https://{trimmed}", out var normalizedUri))
            return false;

        absoluteUrl = normalizedUri.AbsoluteUri;
        return true;
    }

    private static bool TryNormalizeSupportedUrl(string? url, out string absoluteUrl)
    {
        absoluteUrl = string.Empty;

        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return false;

        var isHttp = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isMailTo = uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);

        if (!isHttp && !isHttps && !isMailTo)
        {
            return false;
        }

        // Keep mailto as entered so the shell receives the full address route unchanged.
        absoluteUrl = isMailTo ? trimmed : uri.AbsoluteUri;
        return true;
    }

    private static bool TryCreateAbsoluteWebUri(string value, out Uri uri)
    {
        uri = null!;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            return false;

        var isHttp = parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        var isHttps = parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp && !isHttps)
            return false;

        uri = parsed;
        return true;
    }

    private static bool LooksLikeSchemeLessWebUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Contains("://", StringComparison.Ordinal))
            return false;

        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return false;

        if (value.Contains(' '))
            return false;

        if (value.StartsWith("/") || value.StartsWith("\\") || value.StartsWith("."))
            return false;

        if (value.Contains('@'))
            return false;

        return value.Contains('.');
    }

    private static void TryLog(string tag, string message)
    {
        try { DiagnosticsLog.EnsureExists(); } catch { }
        try { DiagnosticsLog.AppendLine(tag, message); } catch { }
    }
}
