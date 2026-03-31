using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PassNotes;

public static class ThemeRuntimeManager
{
    private static readonly HashSet<string> KnownThemePaths = new(
        AppThemeCatalog.All.Select(x => x.ResourceDictionaryPath),
        StringComparer.OrdinalIgnoreCase);

    public static string CurrentThemeId { get; private set; } = AppThemeCatalog.StandardThemeId;

    public static string GetActiveThemeId(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        return TryGetActiveThemeDefinition(application.Resources.MergedDictionaries)?.Id
               ?? CurrentThemeId;
    }

    public static string ApplyTheme(Application application, string? requestedThemeId)
    {
        ArgumentNullException.ThrowIfNull(application);

        var normalizedThemeId = AppThemeCatalog.NormalizeThemeId(requestedThemeId);
        var definition = AppThemeCatalog.GetDefinition(normalizedThemeId);
        var resources = application.Resources.MergedDictionaries;

        var activeTheme = TryGetActiveThemeDefinition(resources);
        if (activeTheme != null && string.Equals(activeTheme.Id, normalizedThemeId, StringComparison.OrdinalIgnoreCase))
        {
            CurrentThemeId = activeTheme.Id;
            WindowTitleBarThemeManager.RefreshAllOpenWindows(application);
            return activeTheme.Id;
        }

        for (int i = resources.Count - 1; i >= 0; i--)
        {
            var source = resources[i].Source?.OriginalString;
            if (string.IsNullOrWhiteSpace(source))
                continue;

            if (KnownThemePaths.Contains(source))
                resources.RemoveAt(i);
        }

        resources.Add(new ResourceDictionary
        {
            Source = new Uri(definition.ResourceDictionaryPath, UriKind.Relative)
        });

        CurrentThemeId = normalizedThemeId;
        WindowTitleBarThemeManager.RefreshAllOpenWindows(application);

        return normalizedThemeId;
    }

    private static AppThemeCatalog.ThemeDefinition? TryGetActiveThemeDefinition(IEnumerable<ResourceDictionary> resources)
    {
        foreach (var resource in resources)
        {
            var source = resource.Source?.OriginalString;
            if (string.IsNullOrWhiteSpace(source) || !KnownThemePaths.Contains(source))
                continue;

            return AppThemeCatalog.All.FirstOrDefault(x =>
                string.Equals(x.ResourceDictionaryPath, source, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
