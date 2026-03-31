using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PassNotes;

internal static class HotkeysInstaller
{
    // Rate-limit for guard logs: once per (hotkey id + focused type + multiline flag) per app process.
    private static readonly HashSet<string> _guardLogOnce = new();

    // I2.3: runtime registry of installed hotkeys (for best-effort consistency checks).
    private static readonly object _installLock = new();
    private static readonly List<HotkeyInstallRecord> _installRecords = new();
    private static readonly HashSet<string> _appliedScopes = new(StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<HotkeyInstallRecord> GetInstallRecordsSnapshot()
    {
        lock (_installLock)
        {
            return _installRecords.ToList();
        }
    }

    internal static IReadOnlyList<string> GetAppliedScopesSnapshot()
    {
        lock (_installLock)
        {
            return _appliedScopes.ToList();
        }
    }

    private static void MarkScopeApplied(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return;

        lock (_installLock)
        {
            _appliedScopes.Add(scope);
        }
    }

    private static void AddInstallRecord(HotkeyDefinition def, HotkeyInstallOutcome outcome)
    {
        try
        {
            lock (_installLock)
            {
                _installRecords.Add(new HotkeyInstallRecord(def.ScopeWindow, def.Id, def.GestureText, outcome));
            }
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>
    /// Applies hotkeys to a Window via Window.InputBindings.
    /// Best-effort: must never throw.
    /// </summary>
    public static void Apply(Window window,
        IEnumerable<HotkeyDefinition> defs,
        Func<string, ICommand?> resolveCommand)
    {
        if (window == null) return;
        if (defs == null) return;
        if (resolveCommand == null) return;

        foreach (var def in defs)
        {
            if (def == null) continue;

            MarkScopeApplied(def.ScopeWindow);

            ICommand? cmd = null;
            try { cmd = resolveCommand(def.Id); } catch { }

            if (cmd == null)
            {
                try
                {
                    DiagnosticsLog.AppendLine("HOTKEY_BINDING_SKIP",
                        $"reason=no_command id={def.Id} gesture={def.GestureText} scope={def.ScopeWindow}");
                }
                catch { }

                AddInstallRecord(def, HotkeyInstallOutcome.SkippedNoCommand);
                continue;
            }

            try
            {
                if (HasDuplicateGesture(window.InputBindings, def.Key, def.Modifiers))
                {
                    DiagnosticsLog.AppendLine("HOTKEY_BINDING_SKIP",
                        $"reason=duplicate id={def.Id} gesture={def.GestureText} scope={def.ScopeWindow}");

                    AddInstallRecord(def, HotkeyInstallOutcome.SkippedDuplicate);
                    continue;
                }
            }
            catch
            {
                // best-effort
            }

            try
            {
                var guarded = WrapWithInputGuard(cmd, def);
                var binding = new KeyBinding(guarded, def.ToKeyGesture());
                window.InputBindings.Add(binding);

                AddInstallRecord(def, HotkeyInstallOutcome.Added);
            }
            catch (Exception ex)
            {
                try
                {
                    DiagnosticsLog.AppendLine("HOTKEY_BINDING_ERROR",
                        $"id={def.Id} gesture={def.GestureText} scope={def.ScopeWindow} ex={ex.GetType().Name}");
                }
                catch { }

                AddInstallRecord(def, HotkeyInstallOutcome.Error);
            }
        }
    }

    private static ICommand WrapWithInputGuard(ICommand inner, HotkeyDefinition def)
    {
        try
        {
            if (def.InputPolicy == HotkeyInputPolicy.AllowEverywhere)
                return inner;

            return new GuardedCommand(inner, def);
        }
        catch
        {
            return inner;
        }
    }

    private static bool HasDuplicateGesture(InputBindingCollection col, Key key, ModifierKeys mods)
    {
        try
        {
            foreach (var b in col.OfType<KeyBinding>())
            {
                if (b.Gesture is KeyGesture kg && kg.Key == key && kg.Modifiers == mods)
                    return true;
            }
        }
        catch
        {
            // best-effort
        }

        return false;
    }

    private sealed class GuardedCommand : ICommand
    {
        private readonly ICommand _inner;
        private readonly HotkeyDefinition _def;

        public GuardedCommand(ICommand inner, HotkeyDefinition def)
        {
            _inner = inner;
            _def = def;

            // Forward inner CanExecuteChanged → this CanExecuteChanged.
            // NOTE: do NOT subscribe to static CommandManager events here to avoid leaks.
            try
            {
                _inner.CanExecuteChanged += InnerCanExecuteChanged;
            }
            catch
            {
                // best-effort
            }
        }

        private void InnerCanExecuteChanged(object? sender, EventArgs e)
        {
            try { CanExecuteChanged?.Invoke(this, EventArgs.Empty); } catch { }
        }

        public bool CanExecute(object? parameter)
        {
            try
            {
                if (!InputGuardAllows(_def, out var focusedType, out var isMultiline))
                {
                    LogGuardBlockedOnce(_def, focusedType, isMultiline);
                    return false;
                }

                return _inner.CanExecute(parameter);
            }
            catch
            {
                // If guard or inner command throws, fail closed for safety (do nothing).
                return false;
            }
        }

        public void Execute(object? parameter)
        {
            try
            {
                // WPF should have checked CanExecute already, but keep a best-effort safety net.
                if (!InputGuardAllows(_def, out var focusedType, out var isMultiline))
                {
                    LogGuardBlockedOnce(_def, focusedType, isMultiline);
                    return;
                }

                if (_inner.CanExecute(parameter))
                    _inner.Execute(parameter);
            }
            catch
            {
                // best-effort
            }
        }

        public event EventHandler? CanExecuteChanged;
    }

    private static bool InputGuardAllows(HotkeyDefinition def, out string focusedType, out bool isMultiline)
    {
        focusedType = "";
        isMultiline = false;

        try
        {
            if (def.InputPolicy == HotkeyInputPolicy.AllowEverywhere)
                return true;

            if (!TryGetTextInputFocusInfo(out focusedType, out isMultiline))
                return true;

            return def.InputPolicy switch
            {
                HotkeyInputPolicy.BlockInTextInput => false,
                HotkeyInputPolicy.BlockInMultilineTextInput => !isMultiline,
                _ => true
            };
        }
        catch
        {
            // best-effort: allow if unsure (avoid breaking unrelated keys)
            focusedType = "";
            isMultiline = false;
            return true;
        }
    }

    private static void LogGuardBlockedOnce(HotkeyDefinition def, string focusedType, bool isMultiline)
    {
        try
        {
            focusedType = (focusedType ?? "").Trim();
            if (string.IsNullOrWhiteSpace(focusedType))
                focusedType = "(unknown)";

            var key = $"{def.Id}|{focusedType}|{(isMultiline ? "ml" : "sl")}";

            lock (_guardLogOnce)
            {
                if (_guardLogOnce.Contains(key))
                    return;

                _guardLogOnce.Add(key);
            }

            DiagnosticsLog.AppendLine("HOTKEY_GUARD_BLOCK",
                $"id={def.Id} gesture={def.GestureText} policy={def.InputPolicy} focused={focusedType} multiline={(isMultiline ? "true" : "false")}");
        }
        catch
        {
            // best-effort
        }
    }

    private static bool TryGetTextInputFocusInfo(out string focusedType, out bool isMultiline)
    {
        focusedType = "";
        isMultiline = false;

        try
        {
            var fe = Keyboard.FocusedElement;
            if (fe is null)
                return false;

            if (fe is not DependencyObject d)
                return false;

            DependencyObject? cur = d;

            // Traverse up a bit to catch cases where focus is on Run/Span inside RichTextBox etc.
            for (int i = 0; i < 32 && cur != null; i++)
            {
                if (cur is System.Windows.Controls.Primitives.TextBoxBase)
                {
                    focusedType = cur.GetType().Name;

                    if (cur is System.Windows.Controls.TextBox tb)
                        isMultiline = tb.AcceptsReturn;
                    else if (cur is System.Windows.Controls.RichTextBox)
                        isMultiline = true;
                    else
                        isMultiline = false;

                    return true;
                }

                if (cur is System.Windows.Controls.PasswordBox)
                {
                    focusedType = cur.GetType().Name;
                    isMultiline = false;
                    return true;
                }

                cur = GetParentSmart(cur);
            }
        }
        catch
        {
            // best-effort
        }

        focusedType = "";
        isMultiline = false;
        return false;
    }

    private static DependencyObject? GetParentSmart(DependencyObject d)
    {
        try
        {
            // Visual parent first (more accurate), then logical parent.
            if (d is Visual || d is Visual3D)
            {
                var vp = VisualTreeHelper.GetParent(d);
                if (vp != null)
                    return vp;
            }

            return LogicalTreeHelper.GetParent(d);
        }
        catch
        {
            return null;
        }
    }
}

internal enum HotkeyInstallOutcome
{
    Added = 0,
    SkippedNoCommand = 1,
    SkippedDuplicate = 2,
    Error = 3,
}

internal sealed record HotkeyInstallRecord(
    string ScopeWindow,
    string Id,
    string GestureText,
    HotkeyInstallOutcome Outcome);
