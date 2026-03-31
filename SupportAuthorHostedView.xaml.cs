using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PassNotes;

public partial class SupportAuthorHostedView : UserControl
{
    private readonly PopupToastController _copyToast = new(1100);
    private Popup? _copyToastPopup;
    private TextBlock? _copyToastText;

    public SupportAuthorHostedView()
    {
        InitializeComponent();
        DataContext = BuildViewModel(SupportAuthorInfo.Current);
    }

    private static SupportAuthorHostedViewModel BuildViewModel(SupportAuthorInfo info)
    {
        var loc = Loc.Instance;

        return new SupportAuthorHostedViewModel(
            loc["SupportAuthorLinksHeader"],
            loc["SupportAuthorContactsHeader"],
            loc["SupportAuthorDescription"],
            loc["SupportAuthorBrowserHint"],
            info.Services
                .Select(service => new SupportAuthorCardViewModel(
                    service.LogoUri,
                    true,
                    string.Empty,
                    false,
                    service.ValueItems
                        .Select(value => new SupportAuthorValueViewModel(
                            value.UseTextResourceKey ? loc[value.TextOrResourceKey] : value.TextOrResourceKey,
                            value.CopyText,
                            value.ValueUrl,
                            value.CanCopyValue,
                            true,
                            value.CanOpenValueInBrowser && ExternalUrlService.CanOpenWebUrl(value.ValueUrl),
                            loc["Copy"],
                            loc["Open"]))
                        .ToArray()))
                .ToArray(),
            info.Contacts
                .Select(contact => new SupportAuthorCardViewModel(
                    contact.LogoUri ?? string.Empty,
                    !string.IsNullOrWhiteSpace(contact.LogoUri),
                    contact.VectorIconKey ?? string.Empty,
                    !string.IsNullOrWhiteSpace(contact.VectorIconKey),
                    contact.ValueItems
                        .Select(value => new SupportAuthorValueViewModel(
                            value.UseTextResourceKey ? loc[value.TextOrResourceKey] : value.TextOrResourceKey,
                            value.CopyText,
                            value.ValueUrl,
                            value.CanCopyValue,
                            true,
                            value.CanOpenValueInBrowser && ExternalUrlService.CanOpenWebUrl(value.ValueUrl),
                            loc["Copy"],
                            loc["Open"]))
                        .ToArray()))
                .ToArray());
    }

    private void SupportValueText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string url, DataContext: SupportAuthorValueViewModel { CanOpenValueText: true } })
            return;

        ExternalUrlService.TryOpen(url, "SUPPORT_AUTHOR_VALUE_OPEN");
    }

    private void SupportCopyValueButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value })
            return;

        if (ClipboardSecurity.TryCopyText(value, out _))
        {
            ShowCopyToast(Loc.Instance["Copied"], sender as UIElement);
            return;
        }

        try { DiagnosticsLog.EnsureExists(); } catch { }
        try { DiagnosticsLog.AppendLine("SUPPORT_AUTHOR_VALUE_COPY", "result=error reason=clipboard_busy_or_unavailable"); } catch { }
        ShowCopyToast(Loc.Instance["CopyFailed"], sender as UIElement);
    }

    private void SupportOpenValueButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string url })
            return;

        ExternalUrlService.TryOpen(url, "SUPPORT_AUTHOR_INLINE_OPEN");
    }

    private void EnsureCopyToast()
    {
        if (_copyToastPopup != null)
            return;

        try
        {
            _copyToastText = new TextBlock();
            _copyToastText.SetResourceReference(FrameworkElement.StyleProperty, "BaselineToastText");

            var border = new Border
            {
                Child = _copyToastText
            };
            border.SetResourceReference(FrameworkElement.StyleProperty, "BaselineToastBorder");

            _copyToastPopup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = false,
                Placement = PlacementMode.Bottom,
                PlacementTarget = this,
                Child = border
            };
        }
        catch
        {
            _copyToastPopup = null;
            _copyToastText = null;
        }
    }

    private void ShowCopyToast(string message, UIElement? placementTarget = null, int? durationMs = null)
    {
        try
        {
            EnsureCopyToast();
            if (_copyToastPopup == null || _copyToastText == null)
                return;

            _copyToastText.Text = message ?? "";
            _copyToastPopup.PlacementTarget = placementTarget ?? this;
            _copyToast.Show(_copyToastPopup, durationMs);
        }
        catch
        {
            // ignore
        }
    }

    private sealed record SupportAuthorHostedViewModel(
        string LinksHeader,
        string ContactsHeader,
        string Description,
        string BrowserHint,
        IReadOnlyList<SupportAuthorCardViewModel> Services,
        IReadOnlyList<SupportAuthorCardViewModel> Contacts);

    private sealed record SupportAuthorCardViewModel(
        string LogoUri,
        bool HasLogoUri,
        string VectorIconKey,
        bool HasVectorIconKey,
        IReadOnlyList<SupportAuthorValueViewModel> ValueItems)
    {
        public bool HasValueItems => ValueItems.Count > 0;
    }

    private sealed record SupportAuthorValueViewModel(
        string Text,
        string CopyText,
        string ValueUrl,
        bool CanCopyValue,
        bool CanOpenValueText,
        bool CanOpenValueInBrowser,
        string CopyValueToolTip,
        string OpenValueToolTip);
}
