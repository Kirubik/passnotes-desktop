using System;
using System.Collections.Generic;
using System.Linq;

namespace PassNotes;

/// <summary>
/// I2.3 (C11): Best-effort consistency check between HotkeysCatalog and runtime-installed hotkeys.
/// Logs only, rate-limited (once per process).
/// </summary>
internal static class HotkeysConsistencyChecker
{
    private static bool _done;
    private static readonly object _lock = new();

    public static void ValidateOnce()
    {
        lock (_lock)
        {
            if (_done)
                return;
            _done = true;
        }

        try
        {
            var appliedScopes = HotkeysInstaller.GetAppliedScopesSnapshot();
            var records = HotkeysInstaller.GetInstallRecordsSnapshot();

            // Only validate scopes that were actually applied in this process.
            var scopesSet = new HashSet<string>(appliedScopes, StringComparer.OrdinalIgnoreCase);

            int missing = 0;
            int errors = 0;
            int dup = 0;

            var missingList = new List<string>();
            var errorList = new List<string>();
            var dupList = new List<string>();

            foreach (var def in HotkeysCatalog.All)
            {
                if (!scopesSet.Contains(def.ScopeWindow))
                    continue;

                var rec = records.LastOrDefault(r =>
                    string.Equals(r.ScopeWindow, def.ScopeWindow, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Id, def.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.GestureText, def.GestureText, StringComparison.OrdinalIgnoreCase));

                if (rec is null)
                {
                    missing++;
                    if (missingList.Count < 10)
                        missingList.Add($"{def.ScopeWindow}:{def.Id}:{def.GestureText}");
                    continue;
                }

                if (rec.Outcome == HotkeyInstallOutcome.Error)
                {
                    errors++;
                    if (errorList.Count < 10)
                        errorList.Add($"{rec.ScopeWindow}:{rec.Id}:{rec.GestureText}");
                    continue;
                }

                if (rec.Outcome == HotkeyInstallOutcome.SkippedDuplicate)
                {
                    dup++;
                    if (dupList.Count < 10)
                        dupList.Add($"{rec.ScopeWindow}:{rec.Id}:{rec.GestureText}");
                    continue;
                }
            }

            if (missing == 0 && errors == 0 && dup == 0)
            {
                DiagnosticsLog.AppendLine("HELP_HOTKEYS_SYNC_OK",
                    $"scopesApplied={string.Join(',', appliedScopes)} count={HotkeysCatalog.All.Count}");
                return;
            }

            var parts = new List<string>();
            if (missing > 0) parts.Add($"missing={missing}({string.Join(';', missingList)})");
            if (dup > 0) parts.Add($"duplicates={dup}({string.Join(';', dupList)})");
            if (errors > 0) parts.Add($"errors={errors}({string.Join(';', errorList)})");
            parts.Add($"scopesApplied={string.Join(',', appliedScopes)}");

            DiagnosticsLog.AppendLine("HELP_HOTKEYS_OUT_OF_SYNC", string.Join(" ", parts));
        }
        catch (Exception ex)
        {
            try
            {
                DiagnosticsLog.AppendLine("HELP_HOTKEYS_OUT_OF_SYNC", $"ex={ex.GetType().Name}");
            }
            catch { }
        }
    }
}
