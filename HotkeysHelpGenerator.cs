using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PassNotes;

/// <summary>
/// I2.3: Generates help/{ru|en}/hotkeys.md from the runtime hotkeys catalog (HotkeysCatalog).
/// Best-effort: must never throw to callers.
/// </summary>
internal static class HotkeysHelpGenerator
{
    private static readonly object _lock = new();
    private static readonly HashSet<string> _generatedOnce = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetHotkeysMarkdown(string langFolder, out string markdown, out string error)
    {
        markdown = "";
        error = "";

        try
        {
            langFolder = NormalizeLang(langFolder);
            var isRu = string.Equals(langFolder, "ru", StringComparison.OrdinalIgnoreCase);

            var hash = ComputeCatalogHash();
            markdown = BuildMarkdown(isRu, hash);

            // Best-effort: try to persist to output help folder so that the file exists on disk.
            TryPersistToOutput(langFolder, hash, markdown);

            lock (_lock)
            {
                _generatedOnce.Add(langFolder);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void TryPersistToOutput(string langFolder, string hash, string markdown)
    {
        try
        {
            lock (_lock)
            {
                // If already generated in this process and the file already has the same hash, skip.
                if (_generatedOnce.Contains(langFolder))
                {
                    var pathExisting = GetOutputPath(langFolder);
                    if (TryReadExistingHash(pathExisting, out var existingHash) && string.Equals(existingHash, hash, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            var path = GetOutputPath(langFolder);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // If same hash already present on disk, skip.
            if (TryReadExistingHash(path, out var existing) && string.Equals(existing, hash, StringComparison.OrdinalIgnoreCase))
                return;

            File.WriteAllText(path, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // ignore (output folder might be read-only)
        }
    }

    private static string GetOutputPath(string langFolder)
    {
        var root = HelpContentService.GetHelpRoot();
        return Path.Combine(root, langFolder, "hotkeys.md");
    }

    private static bool TryReadExistingHash(string path, out string hash)
    {
        hash = "";
        try
        {
            if (!File.Exists(path))
                return false;

            // Read first 8 KB; our header is at the top.
            var head = File.ReadAllText(path, Encoding.UTF8);
            var idx = head.IndexOf("catalogHash=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            idx += "catalogHash=".Length;
            var end = head.IndexOfAny(new[] { ' ', '\r', '\n', '>', '-' }, idx);
            if (end < 0)
                end = Math.Min(head.Length, idx + 128);

            hash = head.Substring(idx, end - idx).Trim().Trim('"', '\'', '>');
            return !string.IsNullOrWhiteSpace(hash);
        }
        catch
        {
            hash = "";
            return false;
        }
    }

    private static string NormalizeLang(string langFolder)
    {
        langFolder = (langFolder ?? "").Trim();
        if (langFolder.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
            return "ru";
        return "en";
    }

    private static string ComputeCatalogHash()
    {
        var defs = HotkeysCatalog.All
            .OrderBy(x => x.ScopeWindow)
            .ThenBy(x => x.Id)
            .ThenBy(x => x.GestureText)
            .Select(x => $"{x.ScopeWindow}|{x.Id}|{x.GestureText}|{x.TitleRu}|{x.TitleEn}|{x.InputPolicy}");

        var joined = string.Join("\n", defs);
        var bytes = Encoding.UTF8.GetBytes(joined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string BuildMarkdown(bool isRu, string hash)
    {
        var nowUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var sb = new StringBuilder();
        sb.AppendLine($"<!-- generatedUtc={nowUtc} catalogHash={hash} -->");
        sb.AppendLine(isRu ? "# Горячие клавиши" : "# Hotkeys");
        sb.AppendLine();
        sb.AppendLine(isRu
            ? "Хоткеи работают **внутри приложения** (не глобальные)."
            : "Hotkeys work **inside the app** (not global)."
        );
        sb.AppendLine();

        var byScope = HotkeysCatalog.All
            .GroupBy(x => x.ScopeWindow)
            .OrderBy(g => ScopeOrderKey(g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var scope in byScope)
        {
            var title = GetScopeTitle(scope.Key, isRu);
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            sb.AppendLine(@"<table class=""hotkeys-table"">");
            sb.AppendLine(@"  <colgroup>");
            sb.AppendLine(@"    <col class=""hotkeys-col-action"" />");
            sb.AppendLine(@"    <col class=""hotkeys-col-keys"" />");
            sb.AppendLine(@"  </colgroup>");
            sb.AppendLine(@"  <thead>");
            sb.AppendLine(@"    <tr>");
            sb.AppendLine($"      <th>{EscapeHtml(isRu ? "Действие" : "Action")}</th>");
            sb.AppendLine($"      <th>{EscapeHtml(isRu ? "Клавиши" : "Keys")}</th>");
            sb.AppendLine(@"    </tr>");
            sb.AppendLine(@"  </thead>");
            sb.AppendLine(@"  <tbody>");

            var defs = scope
                .OrderBy(x => x.Category ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => isRu ? x.TitleRu : x.TitleEn, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dupGestures = defs
                .GroupBy(x => x.GestureText)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var d in defs)
            {
                var name = isRu ? d.TitleRu : d.TitleEn;
                var keys = d.GestureText;
                var note = BuildNote(d.InputPolicy, dupGestures.Contains(d.GestureText), isRu);

                sb.AppendLine(@"    <tr>");
                sb.AppendLine($"      <td>{BuildActionCellHtml(name, note)}</td>");
                sb.AppendLine($"      <td><span class=\"hotkeys-gesture\">{EscapeHtml(keys)}</span></td>");
                sb.AppendLine(@"    </tr>");
            }

            sb.AppendLine(@"  </tbody>");
            sb.AppendLine(@"</table>");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine(isRu
            ? $"_Сгенерировано автоматически из HotkeysCatalog. Время: {nowUtc}_"
            : $"_Generated automatically from HotkeysCatalog. Time: {nowUtc}_");

        return sb.ToString();
    }

    private static string EscapeMdCell(string s)
    {
        s ??= "";
        return s.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
    }

    private static string EscapeHtml(string s)
    {
        return System.Net.WebUtility.HtmlEncode(s ?? "");
    }

    private static string BuildActionCellHtml(string title, string note)
    {
        var encodedTitle = EscapeHtml(title);
        if (string.IsNullOrWhiteSpace(note))
            return $"<div class=\"hotkeys-action-main\">{encodedTitle}</div>";

        return $"<div class=\"hotkeys-action-main\">{encodedTitle}</div><div class=\"hotkeys-action-note\">{EscapeHtml(note)}</div>";
    }

    private static string BuildNote(HotkeyInputPolicy policy, bool isConflict, bool isRu)
    {
        var parts = new List<string>();
        if (policy == HotkeyInputPolicy.BlockInTextInput)
            parts.Add(isRu ? "блокируется при вводе" : "blocked while typing");
        else if (policy == HotkeyInputPolicy.BlockInMultilineTextInput)
            parts.Add(isRu ? "блокируется в многострочном вводе" : "blocked in multiline input");

        if (isConflict)
            parts.Add(isRu ? "⚠ конфликт" : "⚠ conflict");

        return string.Join(isRu ? "; " : "; ", parts);
    }

    private static string GetScopeTitle(string scopeWindow, bool isRu)
    {
        return scopeWindow switch
        {
            "MainWindow" => isRu ? "Главное окно" : "Main window",
            "EntryEditor" => isRu ? "Редактор записи" : "Entry editor",
            "SettingsDialog" => isRu ? "Настройки" : "Settings",
            "PasswordGeneratorDialog" => isRu ? "Генератор пароля" : "Password generator",
            _ => scopeWindow
        };
    }

    private static int ScopeOrderKey(string scopeWindow)
    {
        return scopeWindow switch
        {
            "MainWindow" => 0,
            "EntryEditor" => 1,
            "SettingsDialog" => 2,
            "PasswordGeneratorDialog" => 3,
            _ => 100
        };
    }
}
