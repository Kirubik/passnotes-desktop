using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace PassNotes;

internal static class HotkeyGestureUtils
{
    public static string ToText(Key key, ModifierKeys modifiers)
    {
        try
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            // Normalize common keys
            var k = key switch
            {
                Key.Return => "Enter",
                Key.Escape => "Esc",
                Key.Delete => "Delete",
                _ => key.ToString()
            };

            parts.Add(k);

            return string.Join("+", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
        catch
        {
            return key.ToString();
        }
    }

    public static bool IsSameGesture(KeyGesture? a, KeyGesture? b)
    {
        if (a == null || b == null) return false;
        return a.Key == b.Key && a.Modifiers == b.Modifiers;
    }
}
