using System;
using System.Reflection;

namespace PassNotes;

/// <summary>
/// Best-effort token replacement for help markdown content.
/// Tokens are intentionally limited to keep behavior predictable.
/// </summary>
internal static class HelpTokenReplacer
{
    private static bool _loggedUnknownVersion;

    internal static string Replace(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown;

        // Keep this list small and explicit.
        var appName = GetAppName();
        var appVersion = GetAppVersion();

        // Use Ordinal to avoid culture surprises. Tokens are ASCII.
        var result = markdown
            .Replace("{APP_NAME}", appName, StringComparison.Ordinal)
            .Replace("{APP_VERSION}", appVersion, StringComparison.Ordinal);

        return result;
    }

    internal static string GetAppVersion()
    {
        try
        {
            var asm = typeof(HelpTokenReplacer).Assembly;

            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
                return info.Trim();

            var v = asm.GetName().Version;
            if (v is not null)
                return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch
        {
            // ignore
        }

        if (!_loggedUnknownVersion)
        {
            _loggedUnknownVersion = true;
            try { DiagnosticsLog.AppendLine("HELP_VERSION_UNKNOWN", "using 'unknown'"); } catch { }
        }

        return "unknown";
    }

    private static string GetAppName()
    {
        try
        {
            var title = Loc.Instance["AppTitle"];
            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();
        }
        catch
        {
            // ignore
        }

        return "PassNotes Desktop";
    }
}
