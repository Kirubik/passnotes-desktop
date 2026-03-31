using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace PassNotes;

internal static class HelpNavParser
{
    private static readonly Regex MdLink = new(@"\[(?<text>[^\]]+)\]\((?<target>[^\)]+)\)", RegexOptions.Compiled);

    public static List<HelpTocItem> ParseNavigationMarkdown(string markdown)
    {
        var items = new List<HelpTocItem>();
        if (string.IsNullOrWhiteSpace(markdown))
            return items;

        var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var raw in lines)
        {
            var line = raw?.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var m = MdLink.Match(line);
            if (!m.Success)
                continue;

            var title = m.Groups["text"].Value.Trim();
            var targetRaw = m.Groups["target"].Value.Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(targetRaw))
                continue;

            // Strip quotes if any.
            if ((targetRaw.StartsWith('"') && targetRaw.EndsWith('"')) || (targetRaw.StartsWith('\'') && targetRaw.EndsWith('\'')))
                targetRaw = targetRaw[1..^1].Trim();

            // Normalize to a file name (ignore folders).
            targetRaw = targetRaw.Replace('\\', '/');
            if (targetRaw.StartsWith("./", StringComparison.Ordinal))
                targetRaw = targetRaw[2..];

            string filePart = targetRaw;
            string? anchor = null;
            var hashIdx = targetRaw.IndexOf('#');
            if (hashIdx >= 0)
            {
                filePart = targetRaw[..hashIdx];
                anchor = targetRaw[(hashIdx + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(anchor))
                    anchor = null;
            }

            filePart = filePart.Trim();
            if (string.IsNullOrWhiteSpace(filePart) || !filePart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = Path.GetFileName(filePart);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            items.Add(new HelpTocItem(title, fileName, anchor));
        }

        return items;
    }
}
