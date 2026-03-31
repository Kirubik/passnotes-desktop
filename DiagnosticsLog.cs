using System;
using System.IO;
using System.Text;

namespace PassNotes;

/// <summary>
/// Best-effort diagnostics logger (appends to %APPDATA%\PassNotes\diagnostic.log).
/// Must never throw.
/// </summary>
internal static class DiagnosticsLog
{
    private static string DiagnosticLogPath => Path.Combine(SettingsStore.GetAppDir(), "diagnostic.log");

    /// <summary>
    /// Ensures the diagnostic log file exists (creates an empty file if missing).
    /// Must never throw.
    /// </summary>
    public static void EnsureExists()
    {
        try
        {
            var dir = Path.GetDirectoryName(DiagnosticLogPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(DiagnosticLogPath))
            {
                using var _ = File.Create(DiagnosticLogPath);
            }
        }
        catch
        {
            // best-effort
        }
    }

    public static void AppendLine(string tag, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(DiagnosticLogPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            tag = (tag ?? "").Trim();
            if (string.IsNullOrWhiteSpace(tag))
                tag = "INFO";

            message ??= "";

            var line = $"{DateTimeOffset.Now:O}\t{tag}\t{message}{Environment.NewLine}";
            File.AppendAllText(DiagnosticLogPath, line, Encoding.UTF8);
        }
        catch
        {
            // best-effort
        }
    }
}
