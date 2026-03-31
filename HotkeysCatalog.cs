using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace PassNotes;

internal static class HotkeysCatalog
{
    // NOTE: This catalog is the single source of truth for application-level hotkeys.
    // It will be used later for the help hotkeys section (I2.3).

    private static readonly HotkeyDefinition[] _all =
    [
        // MainWindow
        new HotkeyDefinition(
            Id: "main.search.focusEntries",
            TitleRu: "Фокус в поиск записей",
            TitleEn: "Focus entries search",
            Key: Key.F,
            Modifiers: ModifierKeys.Control,
            ScopeWindow: "MainWindow",
            Category: "Main",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

        new HotkeyDefinition(
            Id: "main.search.focusFolders",
            TitleRu: "Фокус в поиск папок",
            TitleEn: "Focus folders search",
            Key: Key.F,
            Modifiers: ModifierKeys.Control | ModifierKeys.Shift,
            ScopeWindow: "MainWindow",
            Category: "Main",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

        new HotkeyDefinition(
            Id: "main.entry.add",
            TitleRu: "Создать запись",
            TitleEn: "Create entry",
            Key: Key.N,
            Modifiers: ModifierKeys.Control,
            ScopeWindow: "MainWindow",
            Category: "Main",
            InputPolicy: HotkeyInputPolicy.BlockInTextInput),

        new HotkeyDefinition(
            Id: "main.folder.add",
            TitleRu: "Создать папку",
            TitleEn: "Create folder",
            Key: Key.N,
            Modifiers: ModifierKeys.Control | ModifierKeys.Shift,
            ScopeWindow: "MainWindow",
            Category: "Main",
            InputPolicy: HotkeyInputPolicy.BlockInTextInput),

        new HotkeyDefinition(
            Id: "main.lock.toggle",
            TitleRu: "Заблокировать/разблокировать",
            TitleEn: "Lock/Unlock toggle",
            Key: Key.L,
            Modifiers: ModifierKeys.Control,
            ScopeWindow: "MainWindow",
            Category: "Main",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

        new HotkeyDefinition(
            Id: "help.open",
            TitleRu: "Справка",
            TitleEn: "Help",
            Key: Key.F1,
            Modifiers: ModifierKeys.None,
            ScopeWindow: "MainWindow",
            Category: "Help",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),


        // Entry editor
        new HotkeyDefinition(
            Id: "entry.save.ctrlS",
            TitleRu: "Сохранить запись",
            TitleEn: "Save entry",
            Key: Key.S,
            Modifiers: ModifierKeys.Control,
            ScopeWindow: "EntryEditor",
            Category: "Entry",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

        new HotkeyDefinition(
            Id: "help.open",
            TitleRu: "Справка",
            TitleEn: "Help",
            Key: Key.F1,
            Modifiers: ModifierKeys.None,
            ScopeWindow: "EntryEditor",
            Category: "Help",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),


        // Settings
        new HotkeyDefinition(
            Id: "settings.save.ctrlS",
            TitleRu: "Сохранить настройки",
            TitleEn: "Save settings",
            Key: Key.S,
            Modifiers: ModifierKeys.Control,
            ScopeWindow: "SettingsDialog",
            Category: "Settings",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

        new HotkeyDefinition(
            Id: "help.open",
            TitleRu: "Справка",
            TitleEn: "Help",
            Key: Key.F1,
            Modifiers: ModifierKeys.None,
            ScopeWindow: "SettingsDialog",
            Category: "Help",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

        // Password generator
        new HotkeyDefinition(
            Id: "help.open",
            TitleRu: "Справка",
            TitleEn: "Help",
            Key: Key.F1,
            Modifiers: ModifierKeys.None,
            ScopeWindow: "PasswordGeneratorDialog",
            Category: "Help",
            InputPolicy: HotkeyInputPolicy.AllowEverywhere),

    ];

    public static IReadOnlyList<HotkeyDefinition> All => _all;

    public static IReadOnlyList<HotkeyDefinition> ForMainWindow => _all.Where(x => x.ScopeWindow == "MainWindow").ToArray();
    public static IReadOnlyList<HotkeyDefinition> ForEntryEditor => _all.Where(x => x.ScopeWindow == "EntryEditor").ToArray();
    public static IReadOnlyList<HotkeyDefinition> ForSettingsDialog => _all.Where(x => x.ScopeWindow == "SettingsDialog").ToArray();
    public static IReadOnlyList<HotkeyDefinition> ForPasswordGeneratorDialog => _all.Where(x => x.ScopeWindow == "PasswordGeneratorDialog").ToArray();
}

