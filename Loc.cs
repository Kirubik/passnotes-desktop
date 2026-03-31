using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace PassNotes;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private static readonly ResourceManager Rm =
        new("PassNotes.Resources.Strings", typeof(Loc).Assembly);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Localized string by key. Never throws.
    /// </summary>
    public string this[string key]
    {
        get
        {
            try
            {
                return Rm.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";
            }
            catch
            {
                // MissingManifestResourceException and others should not crash the UI.
                return $"!{key}!";
            }
        }
    }

    // Strongly-typed convenience properties for XAML bindings.
    // We intentionally prefer properties over indexer-path bindings to avoid
    // XAML parsing quirks around "Path=[Key]" in some scenarios.
    public string EntriesCountLabel => this["EntriesCountLabel"];
    public string SelectedCountLabel => this["SelectedCountLabel"];
    public string FolderLabel => this["FolderLabel"];
    public string ContextLabel => this["ContextLabel"];
    public string NotSelectedLabel => this["NotSelectedLabel"];
    public string NoFolderLabel => this["NoFolderLabel"];
    public string Error => this["Error"];
    public string BadPassword => this["BadPassword"];

    public void SetCulture(string cultureName)
    {
        var ci = new CultureInfo(cultureName);
        Thread.CurrentThread.CurrentUICulture = ci;

        // IMPORTANT: keep CurrentCulture (formats) aligned with the OS, not the UI language.
        // We only change UI strings culture (CurrentUICulture).

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
