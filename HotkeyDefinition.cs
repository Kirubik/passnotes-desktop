using System.Windows.Input;

namespace PassNotes;

internal enum HotkeyInputPolicy
{
    /// <summary>
    /// Hotkey is allowed regardless of focused element (subject to the command's own CanExecute).
    /// </summary>
    AllowEverywhere = 0,

    /// <summary>
    /// Hotkey is blocked when keyboard focus is in any text input (TextBox/RichTextBox/PasswordBox/etc.).
    /// </summary>
    BlockInTextInput = 1,

    /// <summary>
    /// Hotkey is blocked only when focus is in a multiline text input (AcceptsReturn=true / RichTextBox).
    /// </summary>
    BlockInMultilineTextInput = 2,
}

internal sealed record HotkeyDefinition(
    string Id,
    string TitleRu,
    string TitleEn,
    Key Key,
    ModifierKeys Modifiers,
    string ScopeWindow,
    string Category,
    HotkeyInputPolicy InputPolicy = HotkeyInputPolicy.AllowEverywhere)
{
    public string GestureText => HotkeyGestureUtils.ToText(Key, Modifiers);

    public KeyGesture ToKeyGesture() => new(Key, Modifiers);
}
