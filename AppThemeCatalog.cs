using System;
using System.Collections.Generic;
using System.Linq;

namespace PassNotes;

public static class AppThemeCatalog
{
    public const string StandardThemeId = "standard";
    public const string StandardThemeResourceDictionaryPath = "Themes/Theme.Standard.xaml";
    // Official user-facing name is "Sage Light"; runtime id stays "light" for settings compatibility.
    public const string LightThemeId = "light";
    public const string LightThemeResourceDictionaryPath = "Themes/Theme.Light.xaml";
    public const string ArcticWhiteThemeId = "arctic-white";
    public const string ArcticWhiteThemeResourceDictionaryPath = "Themes/Theme.ArcticWhite.xaml";
    public const string MidnightSlateThemeId = "midnight-slate";
    public const string MidnightSlateThemeResourceDictionaryPath = "Themes/Theme.MidnightSlate.xaml";
    public const string AmberCircuitThemeId = "amber-circuit";
    public const string AmberCircuitThemeResourceDictionaryPath = "Themes/Theme.AmberCircuit.xaml";

    public sealed class ThemeDefinition
    {
        public string Id { get; init; } = StandardThemeId;
        public string DisplayNameKey { get; init; } = "ThemeStandard";
        public string ResourceDictionaryPath { get; init; } = StandardThemeResourceDictionaryPath;
    }

    private static readonly ThemeDefinition[] _all =
    {
        new()
        {
            Id = StandardThemeId,
            DisplayNameKey = "ThemeStandard",
            ResourceDictionaryPath = StandardThemeResourceDictionaryPath
        },
        new()
        {
            Id = LightThemeId,
            DisplayNameKey = "ThemeLight",
            ResourceDictionaryPath = LightThemeResourceDictionaryPath
        },
        new()
        {
            Id = ArcticWhiteThemeId,
            DisplayNameKey = "ThemeArcticWhite",
            ResourceDictionaryPath = ArcticWhiteThemeResourceDictionaryPath
        },
        new()
        {
            Id = MidnightSlateThemeId,
            DisplayNameKey = "ThemeMidnightSlate",
            ResourceDictionaryPath = MidnightSlateThemeResourceDictionaryPath
        },
        new()
        {
            Id = AmberCircuitThemeId,
            DisplayNameKey = "ThemeAmberCircuit",
            ResourceDictionaryPath = AmberCircuitThemeResourceDictionaryPath
        }
    };

    public static IReadOnlyList<ThemeDefinition> All => _all;

    public static ThemeDefinition GetDefinition(string? themeId)
        => _all.FirstOrDefault(x => string.Equals(x.Id, themeId?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? _all[0];

    public static bool IsKnownTheme(string? themeId)
        => !string.IsNullOrWhiteSpace(themeId)
           && _all.Any(x => string.Equals(x.Id, themeId, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeThemeId(string? themeId)
        => GetDefinition(themeId).Id;
}
