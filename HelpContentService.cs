using System;
using System.IO;
using System.Text;

namespace PassNotes;

internal static class HelpContentService
{
    public static string GetHelpRoot()
        => Path.Combine(AppContext.BaseDirectory, "help");

    public static string GetPreferredLanguageFolder()
    {
        var lang = "en";
        try
        {
            var cfg = App.Settings?.Language ?? "en-US";
            if (cfg.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                lang = "ru";
        }
        catch { }
        return lang;
    }

    public static bool TryReadMarkdown(string topicFileName, out string markdown, out string usedLangFolder, out string error)
    {
        markdown = "";
        error = "";
        usedLangFolder = GetPreferredLanguageFolder();

        // Normalize and validate filename.
        topicFileName = NormalizeTopicFileName(topicFileName);
        if (string.IsNullOrWhiteSpace(topicFileName))
        {
            error = "Invalid help topic.";
            return false;
        }

        // I2.3: hotkeys.md is generated from the runtime hotkeys catalog (single source of truth).
        // Best-effort: must never throw and should still provide content even if output folder is read-only.
        if (string.Equals(topicFileName, "hotkeys.md", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (HotkeysHelpGenerator.TryGetHotkeysMarkdown(usedLangFolder, out markdown, out var genError))
                {
                    // Best-effort consistency check (log only, rate-limited).
                    HotkeysConsistencyChecker.ValidateOnce();
                    return true;
                }

                // If generation failed for preferred locale, try EN.
                if (!string.Equals(usedLangFolder, "en", StringComparison.OrdinalIgnoreCase))
                {
                    if (HotkeysHelpGenerator.TryGetHotkeysMarkdown("en", out markdown, out genError))
                    {
                        usedLangFolder = "en";
                        HotkeysConsistencyChecker.ValidateOnce();
                        return true;
                    }
                }

                error = string.IsNullOrWhiteSpace(genError) ? "Failed to generate hotkeys help." : genError;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Try preferred language first, then fallback to EN.
        if (TryReadMarkdownFromFolder(usedLangFolder, topicFileName, out markdown, out error))
            return true;

        if (!string.Equals(usedLangFolder, "en", StringComparison.OrdinalIgnoreCase))
        {
            if (TryReadMarkdownFromFolder("en", topicFileName, out markdown, out error))
            {
                usedLangFolder = "en";
                return true;
            }
        }

        return false;
    }

    public static Uri GetBaseUri(string langFolder)
    {
        var root = GetHelpRoot();
        var dir = Path.Combine(root, langFolder);
        if (!dir.EndsWith(Path.DirectorySeparatorChar))
            dir += Path.DirectorySeparatorChar;
        return new Uri(dir);
    }

    private static bool TryReadMarkdownFromFolder(string langFolder, string topicFileName, out string markdown, out string error)
    {
        markdown = "";
        error = "";

        try
        {
            var root = GetHelpRoot();
            var langDir = Path.Combine(root, langFolder);
            var langDirFull = Path.GetFullPath(langDir);

            var combined = Path.Combine(langDir, topicFileName);
            var full = Path.GetFullPath(combined);

            // Path traversal guard.
            if (!full.StartsWith(langDirFull, StringComparison.OrdinalIgnoreCase))
            {
                error = "Help topic path is outside help folder.";
                return false;
            }

            if (!File.Exists(full))
            {
                error = $"Help file not found: {topicFileName}";
                return false;
            }

            markdown = File.ReadAllText(full, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string NormalizeTopicFileName(string topicFileName)
    {
        topicFileName = (topicFileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(topicFileName))
            return "";

        // Strip folders.
        topicFileName = topicFileName.Replace('\\', '/');
        if (topicFileName.StartsWith("./", StringComparison.Ordinal))
            topicFileName = topicFileName[2..];

        // Disallow rooted / absolute.
        if (topicFileName.Contains(":") || topicFileName.StartsWith("/", StringComparison.Ordinal) || topicFileName.StartsWith("\\", StringComparison.Ordinal))
            return "";

        // Only allow file names (no directories).
        topicFileName = Path.GetFileName(topicFileName);

        if (!topicFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return "";

        if (topicFileName.Contains(".."))
            return "";

        return topicFileName;
    }
}
